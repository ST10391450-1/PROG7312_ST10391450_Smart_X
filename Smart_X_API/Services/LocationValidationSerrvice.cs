using Smart_X_API.Models;

namespace Smart_X_API.Services;

public class LocationValidationService
{
    // Validates a deployment location and all of its children.
    public bool Validate(DeploymentLocation? location)
    {
        return location != null && ValidateRecursive(location);
    }

    // Checks that the location has a name, type, and valid children.
    private bool ValidateRecursive(DeploymentLocation location)
    {
        if (string.IsNullOrWhiteSpace(location.Name))
            return false;

        if (string.IsNullOrWhiteSpace(location.Type))
            return false;

        foreach (var child in location.Children)
        {
            if (!ValidateRecursive(child))
                return false;
        }

        return true;
    }
}