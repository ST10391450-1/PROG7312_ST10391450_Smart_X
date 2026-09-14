using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Models;
using Smart_X_API.Services;

namespace Smart_X_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocationsController : ControllerBase
{
    private static readonly List<DeploymentLocation> Locations = new();

    private readonly LocationValidationService _validationService;

    public LocationsController(LocationValidationService validationService)
    {
        _validationService = validationService;
    }

    [HttpGet]
    public IActionResult GetLocations()
    {
        return Ok(Locations);
    }

    [HttpPost]
    public IActionResult AddLocation([FromBody] DeploymentLocation location)
    {
        if (!_validationService.Validate(location))
        {
            return BadRequest("The deployment location tree is invalid.");
        }

        Locations.Add(location);

        return Created("api/Locations", location);
    }

    [HttpPost("validate")]
    public IActionResult ValidateLocation([FromBody] DeploymentLocation location)
    {
        bool valid = _validationService.Validate(location);

        return Ok(new { Valid = valid });
    }
}
