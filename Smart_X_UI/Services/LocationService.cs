
using Smart_X_UI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Smart_X_UI.Services;

public class LocationService
{
    private readonly HttpClient _httpClient;
    private bool _isAddingLocation;

    public LocationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<string>> GetLocationsAsync()
    {
        var locations = await _httpClient.GetFromJsonAsync<List<DeploymentLocation>>(
            "api/Locations") ?? new List<DeploymentLocation>();

        var result = new List<string>();

        foreach (var location in locations)
        {
            AddLocationNames(location, string.Empty, result);
        }

        return result;
    }

    private void AddLocationNames(
        DeploymentLocation location,
        string parentPath,
        List<string> result)
    {
        var currentPath = string.IsNullOrWhiteSpace(parentPath)
            ? location.Name
            : $"{parentPath} / {location.Name}";

        if (!string.IsNullOrWhiteSpace(currentPath))
            result.Add(currentPath);

        if (location.Children == null)
            return;

        foreach (var child in location.Children)
        {
            AddLocationNames(child, currentPath, result);
        }
    }

    public async Task AddLocationAsync(string location)
    {
        if (_isAddingLocation)
            throw new InvalidOperationException(
                "A location is already being added.");

        _isAddingLocation = true;

        try
        {
            var root = BuildLocationTree(location);

            if (!await ValidateLocationAsync(location))
                throw new InvalidOperationException(
                    "The location already exists or its hierarchy is invalid.");

            using var response = await _httpClient.PostAsJsonAsync(
                "api/Locations",
                root);

            response.EnsureSuccessStatusCode();
        }
        finally
        {
            _isAddingLocation = false;
        }
    }

    public async Task<bool> ValidateLocationAsync(string location)
    {
        DeploymentLocation root;

        try
        {
            root = BuildLocationTree(location);
        }
        catch (ArgumentException)
        {
            return false;
        }

        using var response = await _httpClient.PostAsJsonAsync(
            "api/Locations/validate",
            root);

        if (!response.IsSuccessStatusCode)
            return false;

        var result = await response.Content
            .ReadFromJsonAsync<LocationValidationResponse>();

        return result?.Valid ?? false;
    }

    private static DeploymentLocation BuildLocationTree(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("Location cannot be empty.");

        var parts = location.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        if (parts.Length == 0 || parts.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Invalid location.");

        var root = new DeploymentLocation
        {
            Name = parts[0],
            Type = "Location",
            IsConfigured = false
        };

        var current = root;

        for (var i = 1; i < parts.Length; i++)
        {
            var child = new DeploymentLocation
            {
                Name = parts[i],
                Type = "Location",
                IsConfigured = false
            };

            current.Children.Add(child);
            current = child;
        }

        return root;
    }

    private class LocationValidationResponse
    {
        public bool Valid { get; set; }
    }
}
