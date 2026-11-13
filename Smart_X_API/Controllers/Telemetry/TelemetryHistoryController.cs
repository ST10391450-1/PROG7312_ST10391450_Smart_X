
using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Models.Telemetry;
using Smart_X_API.Services.Telemetry;

namespace Smart_X_API.Controllers.Telemetry;

[ApiController]
[Route("api/[controller]")]
public class TelemetryHistoryController : ControllerBase
{
    private readonly TelemetryService _telemetryService;
    private readonly ILogger<TelemetryHistoryController> _logger;

    public TelemetryHistoryController(
        TelemetryService telemetryService,
        ILogger<TelemetryHistoryController> logger)
    {
        _telemetryService = telemetryService;
        _logger = logger;
    }

    [HttpPost("query")]
    [ProducesResponseType(
        typeof(TelemetryHistoryResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult GetHistoricalReadings(
        [FromBody] TelemetryHistoryRequest request)
    {
        try
        {
            if (request.StartTime == default ||
                request.EndTime == default)
            {
                return BadRequest(new
                {
                    message = "Start time and end time are required."
                });
            }

            if (request.StartTime > request.EndTime)
            {
                return BadRequest(new
                {
                    message = "Start time cannot be later than end time."
                });
            }

            var readings = _telemetryService.GetHistoricalReadings(
                request.StartTime,
                request.EndTime,
                request.DeviceId,
                request.SensorCategory);

            var response = new TelemetryHistoryResponse
            {
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                DeviceId = request.DeviceId,
                SensorCategory = request.SensorCategory,
                TotalReadings = readings.Count,
                Readings = readings.ToList()
            };

            return Ok(response);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to retrieve historical telemetry readings.");

            return Problem(
                title: "Historical telemetry retrieval failed.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
