using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Models;
using Smart_X_API.Services;

namespace Smart_X_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TelemetryController : ControllerBase
{
    private readonly TelemetryService _telemetryService;

    public TelemetryController(TelemetryService telemetryService)
    {
        _telemetryService = telemetryService;
    }

    [HttpPost("temperature")]
    public IActionResult ReceiveTemperature([FromBody] TelemetryPacket<float> packet)
    {
        if (string.IsNullOrWhiteSpace(packet.DeviceId))
        {
            return BadRequest("Device ID is required.");
        }

        if (packet.Value < -50 || packet.Value > 100)
        {
            return BadRequest("Invalid temperature value.");
        }

        DateTime timestamp = packet.Timestamp == default ? DateTime.UtcNow : packet.Timestamp;

        SensorReadingRecord reading = _telemetryService.AddReading(
            packet.DeviceId, packet.SensorCategory, packet.Value, timestamp);

        return Ok(new
        {
            Message = "Temperature telemetry received.",
            reading.DeviceId,
            reading.Timestamp,
            reading.SensorCategory,
            reading.Value
        });
    }

    [HttpPost("power")]
    public IActionResult ReceivePower([FromBody] TelemetryPacket<int> packet)
    {
        if (string.IsNullOrWhiteSpace(packet.DeviceId))
        {
            return BadRequest("Device ID is required.");
        }

        if (packet.Value < 0)
        {
            return BadRequest("Power value cannot be negative.");
        }

        DateTime timestamp = packet.Timestamp == default ? DateTime.UtcNow : packet.Timestamp;

        SensorReadingRecord reading = _telemetryService.AddReading(
            packet.DeviceId, packet.SensorCategory, packet.Value, timestamp);

        return Ok(new
        {
            Message = "Power telemetry received.",
            reading.DeviceId,
            reading.Timestamp,
            reading.SensorCategory,
            reading.Value
        });
    }

    [HttpPost("switch")]
    public IActionResult ReceiveSwitch([FromBody] TelemetryPacket<bool> packet)
    {
        if (string.IsNullOrWhiteSpace(packet.DeviceId))
        {
            return BadRequest("Device ID is required.");
        }

        DateTime timestamp = packet.Timestamp == default ? DateTime.UtcNow : packet.Timestamp;

        double numericValue = packet.Value ? 1 : 0;

        SensorReadingRecord reading = _telemetryService.AddReading(
            packet.DeviceId, packet.SensorCategory, numericValue, timestamp);

        return Ok(new
        {
            Message = "Smart switch telemetry received.",
            reading.DeviceId,
            reading.Timestamp,
            reading.SensorCategory,
            Value = packet.Value
        });
    }

    [HttpGet]
    public IActionResult GetTelemetry([FromQuery] string? deviceId = null)
    {
        return Ok(_telemetryService.GetReadings(deviceId));
    }

    [HttpGet("history/{deviceId}")]
    public IActionResult GetHistory(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return BadRequest("Device ID is required.");
        }

        return Ok(_telemetryService.GetReadings(deviceId));
    }

    [HttpPost("batch")]
    public IActionResult ProcessBatch([FromBody] TelemetryBatch batch)
    {
        if (string.IsNullOrWhiteSpace(batch.DeviceId))
        {
            return BadRequest("Device ID is required.");
        }

        if (string.IsNullOrWhiteSpace(batch.SensorCategory))
        {
            return BadRequest("Sensor category is required.");
        }

        if (batch.RawValues == null || batch.RawValues.Length == 0)
        {
            return BadRequest("Telemetry batch cannot be empty.");
        }

        IReadOnlyList<double> processed = _telemetryService.ProcessRawBatch(batch.RawValues);
        SensorReading aggregate = _telemetryService.Aggregate(processed);

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
    public IActionResult ProcessJaggedBatch([FromBody] double[][] batch)
    {
        if (batch == null || batch.Length == 0)
        {
            return BadRequest("Telemetry batch cannot be empty.");
        }

        IReadOnlyList<double> processed = _telemetryService.ProcessJaggedBatch(batch);
        SensorReading aggregate = _telemetryService.Aggregate(processed);

        return Ok(new
        {
            ProcessedValues = processed,
            Aggregate = aggregate.Value
        });
    }

    [HttpGet("delta")]
    public IActionResult GetDelta([FromQuery] double previous, [FromQuery] double current)
    {
        SensorReading delta = _telemetryService.CalculateDelta(previous, current);

        return Ok(new
        {
            Previous = previous,
            Current = current,
            Delta = delta.Value
        });
    }

    [HttpGet("threshold")]
    public IActionResult CheckThreshold([FromQuery] double value, [FromQuery] double threshold)
    {
        bool exceeded = _telemetryService.ExceedsThreshold(value, threshold);

        return Ok(new
        {
            Value = value,
            Threshold = threshold,
            Exceeded = exceeded
        });
    }
}
