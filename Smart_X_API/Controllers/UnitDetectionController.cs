
using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Services;

namespace Smart_X_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class UnitDetectionController : ControllerBase
{
    private readonly SensorUnitDetectionService _unitDetectionService;

    public UnitDetectionController(
        SensorUnitDetectionService unitDetectionService)
    {
        _unitDetectionService = unitDetectionService;
    }

    [HttpPost("detect")]
    public ActionResult<SensorUnitResult> Detect(
        [FromBody] DetectSensorUnitRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SensorName) &&
            string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest(new
            {
                message = "Provide a sensor name or category."
            });
        }

        var result = _unitDetectionService.Detect(
            request.SensorName,
            request.Category);

        return Ok(result);
    }
}

public sealed record DetectSensorUnitRequest(
    string? SensorName,
    string? Category);
