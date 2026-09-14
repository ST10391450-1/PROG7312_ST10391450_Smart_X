using Smart_X_API.Models;

namespace Smart_X_API.Services;

public class LocationValidationService
{
    public bool Validate(DeploymentLocation? location)
    {
        return location != null && ValidateRecursive(location);
    }

    private bool ValidateRecursive(DeploymentLocation location)
    {
        if (string.IsNullOrWhiteSpace(location.Name))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(location.Type))
        {
            return false;
        }

        if (!location.IsConfigured)
        {
            return false;
        }

        return location.Children.All(ValidateRecursive);
    }
}
