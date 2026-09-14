using Smart_X_UI.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Smart_X_UI.Services;

public class LocationService
{
    private readonly HttpClient _httpClient;

    public LocationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // Gets all locations from the API as full location path
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

    // Adds a location and its children to the result list.
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

        foreach (var child in location.Children)
        {
            AddLocationNames(child, currentPath, result);
        }
    }

    // Builds a location tree and sends it to the API.
    public async Task AddLocationAsync(string location)
    {
        var parts = location.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            throw new ArgumentException(
                "Location cannot be empty.",
                nameof(location));
        }

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

        using var response = await _httpClient.PostAsJsonAsync(
            "api/Locations",
            root);

        response.EnsureSuccessStatusCode();
    }

    // Checks whether a location is valid.
    public async Task<bool> ValidateLocationAsync(string  location)
    {
        var parts = location.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
            return false;

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

        using var response = await _httpClient.PostAsJsonAsync(
            "api/Locations/validate",
            root);

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<LocationValidationResponse>();

        return result?.Valid ?? false;
    }

    private class LocationValidationResponse
    {
        public bool Valid { get; set; }
    }
}

