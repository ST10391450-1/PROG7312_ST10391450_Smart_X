using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Models;
using Smart_X_API.Services;

namespace Smart_X_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TelemetryController : ControllerBase
{
    private readonly TelemetryService _telemetryService;

    private static readonly HashSet<string> SupportedUnits =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "°C",
            "%RH",
            "%",
            "ppm",
            "hPa",
            "m/s",
            "mm",
            "lx",
            "V",
            "A",
            "W",
            "kWh",
            "L/min",
            "m",
            "cm"
        };

    public TelemetryController(TelemetryService telemetryService)
    {
        _telemetryService = telemetryService;
    }

    [HttpPost("temperature")]
    public IActionResult ReceiveTemperature(
        [FromBody] TelemetryPacket<float> packet)
    {
        if (string.IsNullOrWhiteSpace(packet.DeviceId))
            return BadRequest("Device ID is missing.");

        var validation = ValidateSensorEnabled(packet.DeviceId);

        if (validation != null)
            return validation;

        if (!float.IsFinite(packet.Value) ||
            packet.Value < -50 || packet.Value > 100)
        {
            return BadRequest("Temperature must be between -50 and 100 °C.");
        }

        var timestamp = GetTimestamp(packet.Timestamp);

        var reading = _telemetryService.AddReading(
            packet.DeviceId,
            packet.SensorCategory,
            packet.Value,
            timestamp);

        return Ok(new
        {
            Message = "Temperature telemetry received.",
            reading.DeviceId,
            reading.Timestamp,
            reading.SensorCategory,
            reading.Value,
            Unit = "°C"
        });
    }

    [HttpPost("power")]
    public IActionResult ReceivePower(
        [FromBody] TelemetryPacket<int> packet)
    {
        if (string.IsNullOrWhiteSpace(packet.DeviceId))
            return BadRequest("Device ID is missing.");

        var validation = ValidateSensorEnabled(packet.DeviceId);

        if (validation != null)
            return validation;

        if (packet.Value < 0)
            return BadRequest("Power draw cannot be negative.");

        var timestamp = GetTimestamp(packet.Timestamp);

        var reading = _telemetryService.AddReading(
            packet.DeviceId,
            packet.SensorCategory,
            packet.Value,
            timestamp);

        return Ok(new
        {
            Message = "Power telemetry received.",
            reading.DeviceId,
            reading.Timestamp,
            reading.SensorCategory,
            reading.Value,
            Unit = "W"
        });
    }

    [HttpPost("switch")]
    public IActionResult ReceiveSwitch(
        [FromBody] TelemetryPacket<bool> packet)
    {
        if (string.IsNullOrWhiteSpace(packet.DeviceId))
            return BadRequest("Device ID is missing.");

        var validation = ValidateSensorEnabled(packet.DeviceId);

        if (validation != null)
            return validation;

        var timestamp = GetTimestamp(packet.Timestamp);

        double numericValue = packet.Value ? 1 : 0;

        var reading = _telemetryService.AddReading(
            packet.DeviceId,
            packet.SensorCategory,
            numericValue,
            timestamp);

        return Ok(new
        {
            Message = "Smart switch telemetry received.",
            reading.DeviceId,
            reading.Timestamp,
            reading.SensorCategory,
            Value = packet.Value,
            Unit = "state"
        });
    }

    [HttpPost("measurement")]
    public IActionResult ReceiveMeasurement(
        [FromBody] MeasurementTelemetryPacket packet)
    {
        if (string.IsNullOrWhiteSpace(packet.DeviceId))
            return BadRequest("Device ID is missing.");

        var validation = ValidateSensorEnabled(packet.DeviceId);

        if (validation != null)
            return validation;

        if (string.IsNullOrWhiteSpace(packet.SensorCategory))
            return BadRequest("Sensor category is missing.");

        if (string.IsNullOrWhiteSpace(packet.Unit))
            return BadRequest("Measurement unit is required.");

        var unit = packet.Unit.Trim();

        if (!SupportedUnits.Contains(unit))
        {
            return BadRequest(new
            {
                Message = "Unsupported measurement unit.",
                packet.Unit,
                SupportedUnits = SupportedUnits.ToArray()
            });
        }

        if (!double.IsFinite(packet.Value))
            return BadRequest("Measurement value must be a finite number.");

        if (packet.Value < 0 &&
            !unit.Equals("°C", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(
                "Negative values are not allowed for this measurement.");
        }

        if (unit.Equals("°C", StringComparison.OrdinalIgnoreCase) &&
            (packet.Value < -50 || packet.Value > 100))
        {
            return BadRequest("Temperature must be between -50 and 100 °C.");
        }

        var timestamp = GetTimestamp(packet.Timestamp);

        var reading = _telemetryService.AddReading(
            packet.DeviceId,
            packet.SensorCategory,
            packet.Value,
            timestamp);

        return Ok(new
        {
            Message = "Measurement telemetry received.",
            reading.DeviceId,
            reading.Timestamp,
            reading.SensorCategory,
            reading.Value,
            Unit = unit
        });
    }

    [HttpGet]
    public IActionResult GetTelemetry(
        [FromQuery] string? deviceId = null)
    {
        return Ok(_telemetryService.GetReadings(deviceId));
    }

    [HttpGet("history/{deviceId}")]
    public IActionResult GetHistory(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return BadRequest("Device ID is missing.");

        return Ok(_telemetryService.GetReadings(deviceId));
    }

    [HttpPost("batch")]
    public IActionResult ProcessBatch(
        [FromBody] TelemetryBatch batch)
    {
        if (batch is null)
            return BadRequest("Batch is required.");

        if (string.IsNullOrWhiteSpace(batch.DeviceId))
            return BadRequest("Device ID is missing.");

        var validation = ValidateSensorEnabled(batch.DeviceId);

        if (validation != null)
            return validation;

        if (string.IsNullOrWhiteSpace(batch.SensorCategory))
            return BadRequest("Sensor category is missing.");

        if (batch.RawValues is null || batch.RawValues.Length == 0)
            return BadRequest("Cannot process an empty batch.");

        var processed = _telemetryService.ProcessRawBatch(batch.RawValues);
        var aggregate = _telemetryService.Aggregate(processed);

        return Ok(new
        {
            batch.DeviceId,
            batch.SensorCategory,
            RawRows = batch.RawValues.GetLength(0),
            RawColumns = batch.RawValues.GetLength(1),
            ProcessedValues = processed,
            Aggregate = aggregate.Value
        });
    }

    [HttpPost("jagged-batch")]
    public IActionResult ProcessJaggedBatch(
        [FromBody] double[][] batch)
    {
        if (batch is null || batch.Length == 0)
            return BadRequest("Cannot process an empty batch.");

        if (batch.Any(row => row is null))
            return BadRequest("Batch contains an invalid row.");

        var processed = _telemetryService.ProcessJaggedBatch(batch);
        var aggregate = _telemetryService.Aggregate(processed);

        return Ok(new
        {
            ProcessedValues = processed,
            Aggregate = aggregate.Value
        });
    }

    [HttpGet("delta")]
    public IActionResult GetDelta(
        [FromQuery] double previous,
        [FromQuery] double current)
    {
        if (!double.IsFinite(previous) || !double.IsFinite(current))
            return BadRequest("Values must be finite numbers.");

        var delta = _telemetryService.CalculateDelta(previous, current);

        return Ok(new
        {
            Previous = previous,
            Current = current,
            Delta = delta.Value
        });
    }

    [HttpGet("threshold")]
    public IActionResult CheckThreshold(
        [FromQuery] double value,
        [FromQuery] double threshold)
    {
        if (!double.IsFinite(value) || !double.IsFinite(threshold))
            return BadRequest("Value and threshold must be finite numbers.");

        var exceeded = _telemetryService.ExceedsThreshold(value, threshold);

        return Ok(new
        {
            Value = value,
            Threshold = threshold,
            Exceeded = exceeded
        });
    }

    private IActionResult? ValidateSensorEnabled(string deviceId)
    {
        var sensor = SensorsController.FindRegisteredSensor(deviceId);

        if (sensor == null)
            return NotFound(
                $"No registered sensor found for device '{deviceId}'.");

        if (!sensor.IsEnabled)
        {
            return Conflict(
                $"Sensor '{sensor.NodeId}' is disabled. Telemetry was rejected.");
        }

        return null;
    }

    private static DateTime GetTimestamp(DateTime timestamp)
    {
        return timestamp == default ? DateTime.UtcNow : timestamp;
    }
}

public sealed class MeasurementTelemetryPacket
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string SensorCategory { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
}