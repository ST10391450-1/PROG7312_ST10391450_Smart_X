using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Models;
using Smart_X_API.Services;

namespace Smart_X_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SensorsController : ControllerBase
{
    private static readonly string[] ValidCategories = { "Environmental", "Power Consumption", "Actuator" };

    private static readonly string[] AllowedAttachmentExtensions =
    {
        ".txt", ".log", ".cfg", ".conf", ".ini", ".json", ".xml",
        ".yaml", ".yml", ".csv", ".jpg", ".jpeg", ".png", ".webp", ".pdf"
    };

    private const long MaxAttachmentSizeBytes = 50 * 1024 * 1024;

    // In-memory 
    public static readonly List<SensorRegistration> Sensors = new();
    private static readonly List<SensorAttachment> Attachments = new();

    private readonly IWebHostEnvironment _environment;
    private readonly LocationValidationService _locationValidationService;

    public SensorsController(IWebHostEnvironment environment, LocationValidationService locationValidationService)
    {
        _environment = environment;
        _locationValidationService = locationValidationService;
    }

    [HttpGet]
    public IActionResult GetSensors() => Ok(Sensors);

    [HttpGet("{nodeId}")]
    public IActionResult GetSensor(string nodeId)
    {
        var sensor = FindSensor(nodeId);
        return sensor == null
            ? NotFound($"No sensor found with node ID '{nodeId}'.")
            : Ok(sensor);
    }

    [HttpPost]
    public IActionResult RegisterSensor([FromBody] SensorRegistration sensor)
    {
        if (string.IsNullOrWhiteSpace(sensor.MacAddress))
            return BadRequest("Need a MAC address (or some other identifier) to register a sensor.");

        if (string.IsNullOrWhiteSpace(sensor.Location))
            return BadRequest("Deployment location is missing.");

        if (string.IsNullOrWhiteSpace(sensor.NodeId))
            return BadRequest("Node ID is missing.");

        if (string.IsNullOrWhiteSpace(sensor.Category))
            return BadRequest("Sensor category is missing.");

        if (!ValidCategories.Contains(sensor.Category, StringComparer.OrdinalIgnoreCase))
            return BadRequest($"'{sensor.Category}' isn't a category we support.");

        if (Sensors.Any(x => x.NodeId.Equals(sensor.NodeId, StringComparison.OrdinalIgnoreCase)))
            return Conflict($"Node ID '{sensor.NodeId}' is already taken.");

        if (Sensors.Any(x => x.MacAddress.Equals(sensor.MacAddress, StringComparison.OrdinalIgnoreCase)))
            return Conflict("A sensor with that MAC address is already registered.");

        var registeredSensor = new SensorRegistration
        {
            MacAddress = sensor.MacAddress.Trim(),
            Location = sensor.Location.Trim(),
            NodeId = sensor.NodeId.Trim(),
            Category = ValidCategories.First(x => x.Equals(sensor.Category, StringComparison.OrdinalIgnoreCase))
        };

        Sensors.Add(registeredSensor);

        return CreatedAtAction(nameof(GetSensor), new { nodeId = registeredSensor.NodeId }, registeredSensor);
    }

    [HttpDelete("{nodeId}")]
    public IActionResult DeleteSensor(string nodeId)
    {
        var sensor = FindSensor(nodeId);
        if (sensor == null)
            return NotFound($"No sensor found with node ID '{nodeId}'.");

        Sensors.Remove(sensor);

        var sensorAttachments = Attachments
            .Where(x => x.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var attachment in sensorAttachments)
        {
            DeleteAttachmentFile(attachment);
            Attachments.Remove(attachment);
        }

        return NoContent();
    }

    [HttpGet("{nodeId}/attachments")]
    public IActionResult GetAttachments(string nodeId)
    {
        if (!Sensors.Any(x => x.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase)))
            return NotFound($"No sensor found with node ID '{nodeId}'.");

        var attachments = Attachments
            .Where(x => x.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.UploadedAt)
            .ToList();

        return Ok(attachments);
    }

    [HttpPost("{nodeId}/attachments")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(string nodeId, IFormFile? file)
    {
        var sensor = FindSensor(nodeId);
        if (sensor == null)
            return NotFound($"No sensor found with node ID '{nodeId}'.");

        if (file == null || file.Length == 0)
            return BadRequest("No file was attached to the request.");

        if (file.Length > MaxAttachmentSizeBytes)
            return BadRequest("50 MB is the limit for attachments.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedAttachmentExtensions.Contains(extension))
            return BadRequest($"'{extension}' files aren't allowed.");

        var attachmentDirectory = Path.Combine(_environment.ContentRootPath, "attachments", SanitizePathPart(sensor.NodeId));
        Directory.CreateDirectory(attachmentDirectory);

        var attachmentId = Guid.NewGuid();
        var safeFileName = Path.GetFileName(file.FileName);
        var filePath = Path.Combine(attachmentDirectory, $"{attachmentId}_{safeFileName}");

        await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream);
        }

        var attachment = new SensorAttachment
        {
            Id = attachmentId,
            NodeId = sensor.NodeId,
            FileName = safeFileName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileSize = file.Length,
            UploadedAt = DateTime.UtcNow
        };

        Attachments.Add(attachment);
        return Ok(attachment);
    }

    [HttpGet("{nodeId}/attachments/{attachmentId:guid}")]
    public IActionResult DownloadAttachment(string nodeId, Guid attachmentId)
    {
        var attachment = FindAttachment(nodeId, attachmentId);
        if (attachment == null)
            return NotFound("Couldn't find that attachment.");

        var filePath = GetAttachmentPath(attachment);
        if (!System.IO.File.Exists(filePath))
            return NotFound("The file used to exist but isn't on disk anymore.");

        return PhysicalFile(filePath, attachment.ContentType, attachment.FileName);
    }

    [HttpDelete("{nodeId}/attachments/{attachmentId:guid}")]
    public IActionResult DeleteAttachment(string nodeId, Guid attachmentId)
    {
        var attachment = FindAttachment(nodeId, attachmentId);
        if (attachment == null)
            return NotFound("Couldn't find that attachment.");

        DeleteAttachmentFile(attachment);
        Attachments.Remove(attachment);
        return NoContent();
    }

    private SensorRegistration? FindSensor(string nodeId) =>
        Sensors.FirstOrDefault(x => x.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase));

    private SensorAttachment? FindAttachment(string nodeId, Guid attachmentId) =>
        Attachments.FirstOrDefault(x => x.Id == attachmentId && x.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase));

    private string GetAttachmentPath(SensorAttachment attachment) =>
        Path.Combine(_environment.ContentRootPath, "attachments", SanitizePathPart(attachment.NodeId), $"{attachment.Id}_{attachment.FileName}");

    private void DeleteAttachmentFile(SensorAttachment attachment)
    {
        var filePath = GetAttachmentPath(attachment);
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);
    }

    // Node IDs can contain pretty much anything
    private static string SanitizePathPart(string value)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidChar, '_');

        return value;
    }
}
