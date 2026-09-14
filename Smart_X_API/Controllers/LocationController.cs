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

    public LocationsController(LocationService locationService, LocationValidationService validationService)
    {
        _locationService = locationService;
        _validationService = validationService;
    }

    [HttpGet]
    public IActionResult GetLocations() => Ok(_locationService.GetLocations());

    [HttpPost]
    public IActionResult AddLocation([FromBody] DeploymentLocation location)
    {
        if (location == null)
            return BadRequest("Location can't be null.");

        if (!_validationService.Validate(location))
            return BadRequest("That location tree doesn't look right - check the building/floor/room nesting.");

        if (_locationService.LocationExists(location))
            return Conflict("That location's already registered.");

        if (!_locationService.AddLocation(location))
            return BadRequest("Couldn't add the location.");

        return Created("api/Locations", location);
    }

    // Same validation the POST endpoint runs, but without actually saving anything -

    [HttpPost("validate")]
    public IActionResult ValidateLocation([FromBody] DeploymentLocation location)
    {
        if (location == null)
            return BadRequest("Location can't be null.");

        return Ok(new { Valid = _validationService.Validate(location) });
    }
}
