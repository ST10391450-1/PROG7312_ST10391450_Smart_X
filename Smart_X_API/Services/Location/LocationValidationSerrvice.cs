
using Smart_X_API.Models.Other;

namespace Smart_X_API.Services.Location;

public class LocationValidationService
{
    private readonly LocationService _locationService;

    public LocationValidationService(LocationService locationService)
    {
        _locationService = locationService;
    }

    public bool Validate(DeploymentLocation? location)
    {
        if (location == null || !ValidateRecursive(location))
            return false;

        return !_locationService.LocationExists(location);
    }

    private bool ValidateRecursive(DeploymentLocation? location)
    {
        if (location == null ||
            string.IsNullOrWhiteSpace(location.Name) ||
            string.IsNullOrWhiteSpace(location.Type) ||
            location.Children == null)
        {
            return false;
        }

        foreach (var child in location.Children)
        {
            if (!ValidateRecursive(child))
                return false;
        }

        return true;
    }
}
