using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Services;

namespace Smart_X_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlertQueueController : ControllerBase
{
    private readonly TelemetryQueueService _queue;

    public AlertQueueController(TelemetryQueueService queue)
    {
        _queue = queue;
    }

    [HttpPost]
    public IActionResult EnqueueAlert([FromBody] CreatePriorityAlertRequest request)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.DeviceId) ||
            string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest("Device ID and alert message are required.");
        }

        if (request.Priority < 1 || request.Priority > 5)
            return BadRequest("Priority must be between 1 and 5.");

        var alert = new PriorityAlert(
            Guid.NewGuid(),
            request.DeviceId.Trim(),
            request.Message.Trim(),
            request.Priority,
            DateTime.UtcNow);

        _queue.EnqueueAlert(alert);

        return Accepted(alert);
    }

    [HttpGet("next")]
    public IActionResult DequeueNextAlert()
    {
        var alert = _queue.DequeueNextAlert();

        if (alert == null)
            return NoContent();

        return Ok(alert);
    }
}

public class CreatePriorityAlertRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Priority { get; set; }
}