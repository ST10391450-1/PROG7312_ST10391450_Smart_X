
using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Services;

namespace Smart_X_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class InsightsController : ControllerBase
{
    private readonly DeviceLookupService _devices;
    private readonly TelemetryQueueService _telemetry;
    private readonly AlertTrackingService _alerts;
    private readonly PredictiveActionService _predictive;

    public InsightsController(
        DeviceLookupService devices,
        TelemetryQueueService telemetry,
        AlertTrackingService alerts,
        PredictiveActionService predictive)
    {
        _devices = devices;
        _telemetry = telemetry;
        _alerts = alerts;
        _predictive = predictive;
    }

    [HttpGet("device/node/{nodeId}")]
    public IActionResult FindByNodeId(string nodeId)
    {
        var sensor = _devices.FindByNodeId(nodeId);

        return sensor == null
            ? NotFound("Device not found.")
            : Ok(sensor);
    }

    [HttpGet("device/mac/{macAddress}")]
    public IActionResult FindByMacAddress(string macAddress)
    {
        var sensor = _devices.FindByMacAddress(macAddress);

        return sensor == null
            ? NotFound("Device not found.")
            : Ok(sensor);
    }

    [HttpGet("telemetry/recent")]
    public IActionResult RecentTelemetry([FromQuery] int count = 100)
    {
        return Ok(_telemetry.GetRecent(count));
    }

    [HttpGet("telemetry/queue-status")]
    public IActionResult QueueStatus()
    {
        return Ok(new { QueuedReadings = _telemetry.Count });
    }

    [HttpPost("search")]
    public IActionResult RecordSearch([FromBody] SearchRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Query))
            return BadRequest("Search query is required.");

        _predictive.RecordSearch(request.Query);

        return Ok(new
        {
            Message = "Search recorded.",
            request.Query
        });
    }

    [HttpGet("search-history")]
    public IActionResult SearchHistory([FromQuery] int count = 50)
    {
        return Ok(_predictive.GetSearchHistory(count));
    }

    [HttpGet("recommendations")]
    public IActionResult Recommendations()
    {
        return Ok(_predictive.GetRecommendations());
    }

    [HttpGet("alerts")]
    public IActionResult ActiveAlerts()
    {
        return Ok(_alerts.GetActiveAlerts());
    }

    [HttpGet("disconnected")]
    public IActionResult DisconnectedDevices()
    {
        return Ok(_alerts.GetDisconnectedDevices());
    }

    [HttpPost("alerts")]
    public IActionResult ActivateAlert([FromBody] AlertRequest request)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.DeviceId) ||
            string.IsNullOrWhiteSpace(request.AlertType))
        {
            return BadRequest("Device ID and alert type are required.");
        }

        var added = _alerts.TryActivateAlert(
            request.DeviceId,
            request.AlertType);

        if (added)
        {
            _telemetry.EnqueueAlert(new PriorityAlert(
                Guid.NewGuid(),
                request.DeviceId,
                request.AlertType,
                Math.Clamp(request.Priority, 1, 5),
                DateTime.UtcNow));
        }

        return Ok(new { Added = added });
    }

    [HttpDelete("alerts")]
    public IActionResult ResolveAlert(
        [FromQuery] string deviceId,
        [FromQuery] string alertType)
    {
        if (string.IsNullOrWhiteSpace(deviceId) ||
            string.IsNullOrWhiteSpace(alertType))
        {
            return BadRequest("Device ID and alert type are required.");
        }

        return Ok(new
        {
            Resolved = _alerts.ResolveAlert(deviceId, alertType)
        });
    }

    [HttpPost("device/disconnected/{nodeId}")]
    public IActionResult MarkDisconnected(string nodeId)
    {
        if (_devices.FindByNodeId(nodeId) == null)
            return NotFound("Device not found.");

        _alerts.MarkDisconnected(nodeId);

        return Ok(new { DeviceId = nodeId, Disconnected = true });
    }

    [HttpPost("device/connected/{nodeId}")]
    public IActionResult MarkConnected(string nodeId)
    {
        _alerts.MarkConnected(nodeId);

        return Ok(new { DeviceId = nodeId, Disconnected = false });
    }
}

public sealed class SearchRequest
{
    public string Query { get; set; } = string.Empty;
}

public sealed class AlertRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public int Priority { get; set; } = 1;
}
