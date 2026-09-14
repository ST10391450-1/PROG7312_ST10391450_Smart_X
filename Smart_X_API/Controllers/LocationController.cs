using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Models;
using Smart_X_API.Services;

namespace Smart_X_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocationsController : ControllerBase
{
    private readonly LocationService _locationService;
    private readonly LocationValidationService _validationService;

    public LocationsController(
        LocationService locationService,
        LocationValidationService validationService)
    {
        _locationService = locationService;
        _validationService = validationService;
    }

    // Returns all deployment locations.
    [HttpGet]
    public IActionResult GetLocations()
    {
        return Ok(_locationService.GetLocations());
    }

    // Adds a new deployment location after validating it.
    [HttpPost]
    public IActionResult AddLocation(
        [FromBody] DeploymentLocation location)
    {
        if (location == null)
            return BadRequest("A deployment location is required.");

        if (!_validationService.Validate(location))
            return BadRequest("The deployment location tree is invalid.");

        if (_locationService.LocationExists(location))
            return Conflict("The deployment location already exists.");

        if (!_locationService.AddLocation(location))
            return BadRequest("The deployment location could not be added.");

        return Created("api/Locations", location);
    }

    // Checks whether a deployment location is valid.
    [HttpPost("validate")]
    public IActionResult ValidateLocation(
        [FromBody] DeploymentLocation location)
    {
        if (location == null)
            return BadRequest("A deployment location is required.");

        var isValid = _validationService.Validate(location);

        return Ok(new
        {
            Valid = isValid
        });
    }
}

