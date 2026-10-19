using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Models;
using Smart_X_API.Services;

namespace Smart_X_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SensorsController : ControllerBase
{
    private static readonly string[] AllowedAttachmentExtensions =
    {
        ".txt", ".log", ".cfg", ".conf", ".ini", ".json", ".xml",
        ".yaml", ".yml", ".csv", ".jpg", ".jpeg", ".png", ".webp", ".pdf"
    };

    private const long MaxAttachmentSizeBytes = 50 * 1024 * 1024;

    public static readonly List<SensorRegistration> Sensors = new();

    private static readonly List<SensorAttachment> Attachments = new();
    internal static readonly object SensorLock = new();
    private static readonly object AttachmentLock = new();

    private readonly IWebHostEnvironment _environment;
    private readonly LocationValidationService _locationValidationService;
    private readonly SensorCategoryStore _categoryStore;
    private readonly DeviceLookupService _deviceLookup;

    public SensorsController(
        IWebHostEnvironment environment,
        LocationValidationService locationValidationService,
        SensorCategoryStore categoryStore,
        DeviceLookupService deviceLookup)
    {
        _environment = environment;
        _locationValidationService = locationValidationService;
        _categoryStore = categoryStore;
        _deviceLookup = deviceLookup;
    }

    [HttpGet]
    public IActionResult GetSensors()
    {
        lock (SensorLock)
        {
            return Ok(Sensors.ToList());
        }
    }

    [HttpGet("{nodeId}")]
    public IActionResult GetSensor(string nodeId)
    {
        var sensor = _deviceLookup.FindByNodeId(nodeId);

        return sensor == null
            ? NotFound($"No sensor found with node ID '{nodeId}'.")
            : Ok(sensor);
    }

    [HttpPost]
    public IActionResult RegisterSensor([FromBody] SensorRegistration sensor)
    {
        if (sensor == null)
            return BadRequest("Sensor details are required.");

        if (string.IsNullOrWhiteSpace(sensor.MacAddress))
            return BadRequest("Need a MAC address (or some other identifier) to register a sensor.");

        if (string.IsNullOrWhiteSpace(sensor.Location))
            return BadRequest("Deployment location is missing.");

        if (string.IsNullOrWhiteSpace(sensor.NodeId))
            return BadRequest("Node ID is missing.");

        if (string.IsNullOrWhiteSpace(sensor.Category))
            return BadRequest("Sensor category is missing.");

        var nodeId = sensor.NodeId.Trim();
        var macAddress = sensor.MacAddress.Trim();

        var category = _categoryStore.GetAll()
            .FirstOrDefault(x => x.Name.Equals(
                sensor.Category.Trim(),
                StringComparison.OrdinalIgnoreCase));

        if (category == null)
        {
            return BadRequest(
                $"'{sensor.Category}' isn't a registered sensor category. Retrieve available categories from /api/SensorCategories.");
        }

        lock (SensorLock)
        {
            if (Sensors.Any(x => string.Equals(
                    x.NodeId,
                    nodeId,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return Conflict($"Node ID '{nodeId}' is already taken.");
            }

            if (Sensors.Any(x => string.Equals(
                    x.MacAddress,
                    macAddress,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return Conflict("A sensor with that MAC address is already registered.");
            }

            var registeredSensor = new SensorRegistration
            {
                MacAddress = macAddress,
                Location = sensor.Location.Trim(),
                NodeId = nodeId,
                Category = category.Name,
                IsEnabled = true
            };

            Sensors.Add(registeredSensor);
            _deviceLookup.Refresh();

            return CreatedAtAction(
                nameof(GetSensor),
                new { nodeId = registeredSensor.NodeId },
                registeredSensor);
        }
    }

    [HttpPut("{nodeId}/disable")]
    public IActionResult DisableSensor(string nodeId)
    {
        lock (SensorLock)
        {
            var sensor = FindSensor(nodeId);

            if (sensor == null)
                return NotFound($"No sensor found with node ID '{nodeId}'.");

            sensor.IsEnabled = false;

            return Ok(new
            {
                sensor.NodeId,
                sensor.IsEnabled,
                Message = "Sensor disabled."
            });
        }
    }

    [HttpPut("{nodeId}/enable")]
    public IActionResult EnableSensor(string nodeId)
    {
        lock (SensorLock)
        {
            var sensor = FindSensor(nodeId);

            if (sensor == null)
                return NotFound($"No sensor found with node ID '{nodeId}'.");

            sensor.IsEnabled = true;

            return Ok(new
            {
                sensor.NodeId,
                sensor.IsEnabled,
                Message = "Sensor enabled."
            });
        }
    }

    [HttpDelete("{nodeId}")]
    public IActionResult DeleteSensor(string nodeId)
    {
        SensorRegistration? sensor;

        lock (SensorLock)
        {
            sensor = FindSensor(nodeId);

            if (sensor == null)
                return NotFound($"No sensor found with node ID '{nodeId}'.");

            Sensors.Remove(sensor);
            _deviceLookup.Refresh();
        }

        lock (AttachmentLock)
        {
            var sensorAttachments = Attachments
                .Where(x => string.Equals(
                    x.NodeId,
                    sensor.NodeId,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var attachment in sensorAttachments)
            {
                DeleteAttachmentFile(attachment);
                Attachments.Remove(attachment);
            }
        }

        return NoContent();
    }

    [HttpGet("{nodeId}/attachments")]
    public IActionResult GetAttachments(string nodeId)
    {
        if (_deviceLookup.FindByNodeId(nodeId) == null)
            return NotFound($"No sensor found with node ID '{nodeId}'.");

        lock (AttachmentLock)
        {
            var attachments = Attachments
                .Where(x => string.Equals(
                    x.NodeId,
                    nodeId,
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.UploadedAt)
                .ToList();

            return Ok(attachments);
        }
    }

    [HttpPost("{nodeId}/attachments")]
    [RequestSizeLimit(MaxAttachmentSizeBytes)]
    public async Task<IActionResult> UploadAttachment(
        string nodeId,
        IFormFile? file)
    {
        var sensor = _deviceLookup.FindByNodeId(nodeId);

        if (sensor == null)
            return NotFound($"No sensor found with node ID '{nodeId}'.");

        if (file == null || file.Length == 0)
            return BadRequest("No file was attached to the request.");

        if (file.Length > MaxAttachmentSizeBytes)
            return BadRequest("50 MB is the limit for attachments.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!AllowedAttachmentExtensions.Contains(extension))
            return BadRequest($"'{extension}' files aren't allowed.");

        var attachmentDirectory = Path.Combine(
            _environment.ContentRootPath,
            "attachments",
            SanitizePathPart(sensor.NodeId));

        Directory.CreateDirectory(attachmentDirectory);

        var attachmentId = Guid.NewGuid();
        var safeFileName = Path.GetFileName(file.FileName);

        var filePath = Path.Combine(
            attachmentDirectory,
            $"{attachmentId}_{safeFileName}");

        await using (var stream = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
        {
            await file.CopyToAsync(stream);
        }

        var attachment = new SensorAttachment
        {
            Id = attachmentId,
            NodeId = sensor.NodeId,
            FileName = safeFileName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType,
            FileSize = file.Length,
            UploadedAt = DateTime.UtcNow
        };

        lock (AttachmentLock)
        {
            Attachments.Add(attachment);
        }

        return Ok(attachment);
    }

    [HttpGet("{nodeId}/attachments/{attachmentId:guid}")]
    public IActionResult DownloadAttachment(
        string nodeId,
        Guid attachmentId)
    {
        SensorAttachment? attachment;

        lock (AttachmentLock)
        {
            attachment = FindAttachment(nodeId, attachmentId);
        }

        if (attachment == null)
            return NotFound("Couldn't find that attachment.");

        var filePath = GetAttachmentPath(attachment);

        if (!System.IO.File.Exists(filePath))
            return NotFound("The file used to exist but isn't on disk anymore.");

        return PhysicalFile(
            filePath,
            attachment.ContentType,
            attachment.FileName);
    }

    [HttpDelete("{nodeId}/attachments/{attachmentId:guid}")]
    public IActionResult DeleteAttachment(
        string nodeId,
        Guid attachmentId)
    {
        lock (AttachmentLock)
        {
            var attachment = FindAttachment(nodeId, attachmentId);

            if (attachment == null)
                return NotFound("Couldn't find that attachment.");

            DeleteAttachmentFile(attachment);
            Attachments.Remove(attachment);
        }

        return NoContent();
    }

    public static SensorRegistration? FindRegisteredSensor(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return null;

        var identifier = deviceId.Trim();

        lock (SensorLock)
        {
            return Sensors.FirstOrDefault(sensor =>
                string.Equals(
                    sensor.NodeId,
                    identifier,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    sensor.MacAddress,
                    identifier,
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    private SensorRegistration? FindSensor(string nodeId)
    {
        return _deviceLookup.FindByNodeId(nodeId);
    }

    private SensorAttachment? FindAttachment(
        string nodeId,
        Guid attachmentId)
    {
        return Attachments.FirstOrDefault(x =>
            x.Id == attachmentId &&
            string.Equals(
                x.NodeId,
                nodeId,
                StringComparison.OrdinalIgnoreCase));
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
        var filePath = GetAttachmentPath(attachment);

        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);
    }

    private static string SanitizePathPart(string value)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidChar, '_');

        return value;
    }
}