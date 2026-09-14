using Smart_X_API.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Smart_X_API.Services;

public class LocationService
{
    private static readonly List<DeploymentLocation> Locations = new();

    public List<DeploymentLocation> GetLocations()
    {
        return Locations;
    }

    public bool AddLocation(DeploymentLocation location)
    {
        if (location == null ||
            string.IsNullOrWhiteSpace(location.Name))
        {
            return false;
        }

        DeploymentLocation? existingRoot =
            Locations.FirstOrDefault(existing =>
                string.Equals(
                    existing.Name,
                    location.Name,
                    StringComparison.OrdinalIgnoreCase));

        if (existingRoot == null)
        {
            Locations.Add(location);
            return true;
        }

        existingRoot.Type = location.Type;
        existingRoot.IsConfigured = location.IsConfigured;

        foreach (var child in location.Children)
        {
            MergeChild(existingRoot, child);
        }

        return true;
    }

    public bool LocationExists(
        DeploymentLocation location)
    {
        return FindLocation(
            Locations,
            location) != null;
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

            if (target.Children.Count == 0)
            {
                return location;
            }

            foreach (var child in target.Children)
            {
                DeploymentLocation? found =
                    FindLocation(
                        location.Children,
                        child);

                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static void MergeChild(
        DeploymentLocation parent,
        DeploymentLocation child)
    {
        DeploymentLocation? existing =
            parent.Children.FirstOrDefault(existingChild =>
                string.Equals(
                    existingChild.Name,
                    child.Name,
                    StringComparison.OrdinalIgnoreCase));

        if (existing == null)
        {
            parent.Children.Add(child);
            return;
        }

        existing.Type = child.Type;
        existing.IsConfigured = child.IsConfigured;

        foreach (var grandChild in child.Children)
        {
            MergeChild(existing, grandChild);
        }
    }
}