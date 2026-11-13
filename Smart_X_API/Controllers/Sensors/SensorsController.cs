using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Models.Sensors;
using Smart_X_API.Services;
using Smart_X_API.Services.Location;
using Smart_X_API.Services.Sensors;

namespace Smart_X_API.Controllers.Sensors;

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
    private readonly LocationService _locationService;

    public SensorsController(
        IWebHostEnvironment environment,
        LocationValidationService locationValidationService,
        SensorCategoryStore categoryStore,
        DeviceLookupService deviceLookup,
        LocationService locationService)
    {
        _locationService = locationService;
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

    // Bulk import: categories first, then locations, then sensors.
    // Existing categories/locations/sensors are skipped, not overwritten.
    [HttpPost("import")]
    public IActionResult ImportSensors([FromBody] SensorImportRequest? request)
    {
        if (request == null)
            return BadRequest("Import data is required.");

        var result = new SensorImportResult();

        // 1. Categories
        foreach (var item in request.Categories ?? new())
        {
            var name = item.Name?.Trim() ?? string.Empty;
            var description = item.Description?.Trim() ?? string.Empty;

            if (name.Length == 0)
            {
                result.Messages.Add("Category skipped: name is required.");
                result.CategoriesSkipped++;
                continue;
            }

            if (name.Length > 80 || description.Length > 250)
            {
                result.Messages.Add(
                    $"Category '{name}' skipped: name max 80 / description max 250 characters.");
                result.CategoriesSkipped++;
                continue;
            }

            if (_categoryStore.NameExists(name))
            {
                result.CategoriesSkipped++;
                continue;
            }

            _categoryStore.Add(name, description);
            result.CategoriesCreated++;
        }

        // 2. Locations (explicit ones plus any a sensor refers to)
        var locationPaths = (request.Locations ?? new())
            .Concat((request.Sensors ?? new()).Select(x => x.Location))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var path in locationPaths)
            result.LocationsCreated += _locationService.EnsurePath(path);

        // 3. Sensors
        foreach (var item in request.Sensors ?? new())
        {
            var label = string.IsNullOrWhiteSpace(item.NodeId)
                ? "(no node ID)"
                : item.NodeId.Trim();

            if (string.IsNullOrWhiteSpace(item.NodeId) ||
                string.IsNullOrWhiteSpace(item.MacAddress) ||
                string.IsNullOrWhiteSpace(item.Location) ||
                string.IsNullOrWhiteSpace(item.Category))
            {
                result.Messages.Add(
                    $"Sensor {label} skipped: nodeId, macAddress, category and location are required.");
                result.SensorsSkipped++;
                continue;
            }

            var nodeId = item.NodeId.Trim();
            var mac = item.MacAddress.Trim();

            var category = _categoryStore.GetAll().FirstOrDefault(x =>
                x.Name.Equals(item.Category.Trim(),
                    StringComparison.OrdinalIgnoreCase));

            // A sensor may reference a category that wasn't listed - create it.
            if (category == null)
            {
                category = _categoryStore.Add(item.Category.Trim(), string.Empty);
                result.CategoriesCreated++;
            }

            lock (SensorLock)
            {
                if (Sensors.Any(x => string.Equals(
                        x.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Messages.Add($"Sensor {nodeId} skipped: node ID already exists.");
                    result.SensorsSkipped++;
                    continue;
                }

                if (Sensors.Any(x => string.Equals(
                        x.MacAddress, mac, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Messages.Add($"Sensor {nodeId} skipped: MAC {mac} already registered.");
                    result.SensorsSkipped++;
                    continue;
                }

                Sensors.Add(new SensorRegistration
                {
                    MacAddress = mac,
                    NodeId = nodeId,
                    Category = category.Name,
                    Location = item.Location.Trim(),
                    IsEnabled = item.IsEnabled
                });

                result.SensorsImported++;
            }
        }

        _deviceLookup.Refresh();

        return Ok(result);
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