using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Models;
using Smart_X_API.Models.Other;
using Smart_X_API.Services.Location;


namespace Smart_X_API.Controllers.Locations;

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

    [HttpGet]
    public IActionResult GetLocations()
    {
        return Ok(_locationService.GetLocations());
    }

    [HttpPost]
    public IActionResult AddLocation([FromBody] DeploymentLocation location)
    {
        if (!_validationService.Validate(location))
            return Conflict("The location already exists or its hierarchy is invalid.");

        if (!_locationService.AddLocation(location))
            return Conflict("The location could not be added because it already exists.");

        return Created("api/Locations", location);
    }

    [HttpPost("validate")]
    public IActionResult ValidateLocation([FromBody] DeploymentLocation location)
    {
        var isValid = _validationService.Validate(location);

        return Ok(new
        {
            Valid = isValid,
            Message = isValid
                ? "The location is valid and can be added."
                : "The location already exists or its hierarchy is invalid."
        });
    }
}