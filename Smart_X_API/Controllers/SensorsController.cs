using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Models;
using Smart_X_API.Services;

namespace Smart_X_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SensorsController : ControllerBase
{
    private static readonly string[] ValidCategories =
    {
        "Environmental",
        "Power Consumption",
        "Actuator"
    };

    private static readonly string[] AllowedAttachmentExtensions =
    {
        ".txt", ".log", ".cfg", ".conf", ".ini", ".json", ".xml", ".yaml", ".yml", ".csv",
        ".jpg", ".jpeg", ".png", ".webp", ".pdf"
    };

    private const long MaxAttachmentSizeBytes = 50 * 1024 * 1024;

    public static readonly List<SensorRegistration>Sensors = new();
    private static readonly List<SensorAttachment> Attachments = new();

    private readonly IWebHostEnvironment _environment;
    private readonly LocationValidationService _locationValidationService;

    public SensorsController(IWebHostEnvironment environment, LocationValidationService locationValidationService)
    {
        _environment = environment;
        _locationValidationService = locationValidationService;
    }

    [HttpGet]
    public IActionResult GetSensors()
    {
        return Ok(Sensors);
    }

    [HttpGet("{nodeId}")]
    public IActionResult GetSensor(string nodeId)
    {
        SensorRegistration? sensor = FindSensor(nodeId);

        if (sensor == null)
        {
            return NotFound($"Sensor '{nodeId}' was not found.");
        }

        return Ok(sensor);
    }

    [HttpPost]
    public IActionResult RegisterSensor([FromBody] SensorRegistration sensor)
    {
        if (string.IsNullOrWhiteSpace(sensor.MacAddress))
        {
            return BadRequest("MAC address or unique identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(sensor.Location))
        {
            return BadRequest("Deployment location is required.");
        }

        if (string.IsNullOrWhiteSpace(sensor.NodeId))
        {
            return BadRequest("Node ID is required.");
        }

        if (string.IsNullOrWhiteSpace(sensor.Category))
        {
            return BadRequest("Sensor category is required.");
        }

        if (!ValidCategories.Contains(sensor.Category, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid sensor category.");
        }

        bool nodeIdTaken = Sensors.Any(x => x.NodeId.Equals(sensor.NodeId, StringComparison.OrdinalIgnoreCase));

        if (nodeIdTaken)
        {
            return Conflict($"A sensor with Node ID '{sensor.NodeId}' is already registered.");
        }

        bool macAddressTaken = Sensors.Any(x => x.MacAddress.Equals(sensor.MacAddress, StringComparison.OrdinalIgnoreCase));

        if (macAddressTaken)
        {
            return Conflict("A sensor with this MAC address or unique identifier is already registered.");
        }

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
        SensorRegistration? sensor = FindSensor(nodeId);

        if (sensor == null)
        {
            return NotFound($"Sensor '{nodeId}' was not found.");
        }

        Sensors.Remove(sensor);

        List<SensorAttachment> sensorAttachments = Attachments
            .Where(x => x.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (SensorAttachment attachment in sensorAttachments)
        {
            DeleteAttachmentFile(attachment);
            Attachments.Remove(attachment);
        }

        return NoContent();
    }

    [HttpGet("{nodeId}/attachments")]
    public IActionResult GetAttachments(string nodeId)
    {
        bool sensorExists = Sensors.Any(x => x.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase));

        if (!sensorExists)
        {
            return NotFound($"Sensor '{nodeId}' was not found.");
        }

        List<SensorAttachment> attachments = Attachments
            .Where(x => x.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.UploadedAt)
            .ToList();

        return Ok(attachments);
    }

    [HttpPost("{nodeId}/attachments")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(string nodeId, IFormFile? file)
    {
        SensorRegistration? sensor = FindSensor(nodeId);

        if (sensor == null)
        {
            return NotFound($"Sensor '{nodeId}' was not found.");
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest("Please provide a file.");
        }

        if (file.Length > MaxAttachmentSizeBytes)
        {
            return BadRequest("The maximum attachment size is 50 MB.");
        }

        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!AllowedAttachmentExtensions.Contains(extension))
        {
            return BadRequest($"The file type '{extension}' is not supported.");
        }

        string attachmentDirectory = Path.Combine(_environment.ContentRootPath, "attachments", SanitizePathPart(sensor.NodeId));
        Directory.CreateDirectory(attachmentDirectory);

        Guid attachmentId = Guid.NewGuid();
        string safeFileName = Path.GetFileName(file.FileName);
        string storedFileName = $"{attachmentId}_{safeFileName}";
        string filePath = Path.Combine(attachmentDirectory, storedFileName);

        await using (FileStream stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
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
        SensorAttachment? attachment = FindAttachment(nodeId, attachmentId);

        if (attachment == null)
        {
            return NotFound("Attachment was not found.");
        }

        string filePath = GetAttachmentPath(attachment);

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound("The attachment file no longer exists.");
        }

        return PhysicalFile(filePath, attachment.ContentType, attachment.FileName);
    }

    [HttpDelete("{nodeId}/attachments/{attachmentId:guid}")]
    public IActionResult DeleteAttachment(string nodeId, Guid attachmentId)
    {
        SensorAttachment? attachment = FindAttachment(nodeId, attachmentId);

        if (attachment == null)
        {
            return NotFound("Attachment was not found.");
        }

        DeleteAttachmentFile(attachment);
        Attachments.Remove(attachment);

        return NoContent();
    }

    private SensorRegistration? FindSensor(string nodeId)
    {
        return Sensors.FirstOrDefault(x => x.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
    }

    private SensorAttachment? FindAttachment(string nodeId, Guid attachmentId)
    {
        return Attachments.FirstOrDefault(x =>
            x.Id == attachmentId && x.NodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
    }

    private string GetAttachmentPath(SensorAttachment attachment)
    {
        return Path.Combine(
            _environment.ContentRootPath,
            "attachments",
            SanitizePathPart(attachment.NodeId),
            $"{attachment.Id}_{attachment.FileName}");
    }

    private void DeleteAttachmentFile(SensorAttachment attachment)
    {
        string filePath = GetAttachmentPath(attachment);

        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }

    private static string SanitizePathPart(string value)
    {
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidChar, '_');
        }

        return value;
    }
}
