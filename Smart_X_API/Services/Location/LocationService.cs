using Smart_X_API.Models.Other;

namespace Smart_X_API.Services.Location;

public class LocationService
{
    private static readonly List<DeploymentLocation> Locations = new();
    private static readonly object LocationLock = new();

    public List<DeploymentLocation> GetLocations()
    {
        lock (LocationLock)
        {
            return Locations.Select(CloneLocation).ToList();
        }
    }

    public bool AddLocation(DeploymentLocation location)
    {
        if (location == null ||
            string.IsNullOrWhiteSpace(location.Name) ||
            string.IsNullOrWhiteSpace(location.Type) ||
            location.Children == null)
        {
            return false;
        }

        lock (LocationLock)
        {
            if (FindLocation(Locations, location) != null)
                return false;

            Locations.Add(CloneLocation(location));
            return true;
        }
    }

    // Makes sure every level of a "A / B / C" path exists, merging into
    // existing nodes. Returns how many new nodes were created.
    public int EnsurePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return 0;

        var parts = path.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
            return 0;

        var created = 0;

        lock (LocationLock)
        {
            var level = Locations;

            foreach (var part in parts)
            {
                var node = level.FirstOrDefault(x => string.Equals(
                    x.Name, part, StringComparison.OrdinalIgnoreCase));

                if (node == null)
                {
                    node = new DeploymentLocation
                    {
                        Name = part,
                        Type = "Location",
                        IsConfigured = false
                    };

                    level.Add(node);
                    created++;
                }

                node.Children ??= new();
                level = node.Children;
            }
        }

        return created;
    }

    public bool PathExists(string path)
    {
        var parts = path.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
            return false;

        lock (LocationLock)
        {
            var level = Locations;

            foreach (var part in parts)
            {
                var node = level.FirstOrDefault(x => string.Equals(
                    x.Name, part, StringComparison.OrdinalIgnoreCase));

                if (node == null)
                    return false;

                level = node.Children ?? new();
            }
        }

        return true;
    }

    public bool LocationExists(DeploymentLocation location)
    {
        if (location == null ||
            string.IsNullOrWhiteSpace(location.Name))
        {
            return false;
        }

        lock (LocationLock)
        {
            return FindLocation(Locations, location) != null;
        }
    }

    private static DeploymentLocation? FindLocation(
        IEnumerable<DeploymentLocation> locations,
        DeploymentLocation target)
    {
        foreach (var location in locations)
        {
            if (!string.Equals(
                    location.Name,
                    target.Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (target.Children == null || target.Children.Count == 0)
                return location;

            if (location.Children == null)
                continue;

            var allChildrenExist = target.Children.All(targetChild =>
                FindLocation(location.Children, targetChild) != null);

            if (allChildrenExist)
                return location;
        }

        return null;
    }

    private static DeploymentLocation CloneLocation(
        DeploymentLocation location)
    {
        return new DeploymentLocation
        {
            Name = location.Name,
            Type = location.Type,
            IsConfigured = location.IsConfigured,
            Children = location.Children?
                .Select(CloneLocation)
                .ToList() ?? new()
        };
    }
}
