
using System;
using System.Collections.Generic;
using System.Linq;
using Smart_X_API.Models;

namespace Smart_X_API.Services;

public class LocationService
{
    private static readonly List<DeploymentLocation> Locations = new();

    // Returns all stored deployment locations.
    public List<DeploymentLocation> GetLocations()
    {
        return Locations;
    }

    // Adds a new location or updates an existing one.
    public bool AddLocation(DeploymentLocation location)
    {
        if (location == null || string.IsNullOrWhiteSpace(location.Name))
            return false;

        var existingLocation = Locations.FirstOrDefault(existing =>
            string.Equals(existing.Name, location.Name, StringComparison.OrdinalIgnoreCase));

        if (existingLocation == null)
        {
            Locations.Add(location);
            return true;
        }

        existingLocation.Type = location.Type;
        existingLocation.IsConfigured = location.IsConfigured;

        foreach (var child in location.Children)
        {
            MergeChild(existingLocation, child);
        }

        return true;
    }

    // Checks whether a location already exists.
    public bool LocationExists(DeploymentLocation location)
    {
        return FindLocation(Locations, location) != null;
    }

    // Searches through the location hierarchy for a matching location.
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

            if (target.Children.Count == 0)
                return location;

            foreach (var child in target.Children)
            {
                var found = FindLocation(location.Children, child);

                if (found != null)
                    return found;
            }
        }

        return null;
    }

    // Adds a child location or updates an existing child.
    private static void MergeChild(
        DeploymentLocation parent,
        DeploymentLocation child)
    {
        var existingChild = parent.Children.FirstOrDefault(existing =>
            string.Equals(
                existing.Name,
                child.Name,
                StringComparison.OrdinalIgnoreCase));

        if (existingChild == null)
        {
            parent.Children.Add(child);
            return;
        }

        existingChild.Type = child.Type;
        existingChild.IsConfigured = child.IsConfigured;

        foreach (var grandChild in child.Children)
        {
            MergeChild(existingChild, grandChild);
        }
    }
}