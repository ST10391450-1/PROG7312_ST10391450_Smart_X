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

    public async Task<List<string>> GetLocationsAsync()
    {
        var locations =
            await _httpClient.GetFromJsonAsync<List<DeploymentLocation>>(
                "api/Locations")
            ?? new List<DeploymentLocation>();

        var result = new List<string>();

        foreach (var location in locations)
        {
            AddLocationNames(
                location,
                string.Empty,
                result);
        }

        return result;
    }

    private void AddLocationNames(
        DeploymentLocation location,
        string parentPath,
        List<string> result)
    {
        string currentPath =
            string.IsNullOrWhiteSpace(parentPath)
                ? location.Name
                : $"{parentPath} / {location.Name}";

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            result.Add(currentPath);
        }

        foreach (var child in location.Children)
        {
            AddLocationNames(
                child,
                currentPath,
                result);
        }
    }

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

        DeploymentLocation root =
            new()
            {
                Name = parts[0],
                Type = "Location",
                IsConfigured = false
            };

        DeploymentLocation current = root;

        for (int i = 1; i < parts.Length; i++)
        {
            var child =
                new DeploymentLocation
                {
                    Name = parts[i],
                    Type = "Location",
                    IsConfigured = false
                };

            current.Children.Add(child);
            current = child;
        }

        using HttpResponseMessage response =
            await _httpClient.PostAsJsonAsync(
                "api/Locations",
                root);

        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> ValidateLocationAsync(
        string location)
    {
        var parts = location.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            return false;
        }

        DeploymentLocation root =
            new()
            {
                Name = parts[0],
                Type = "Location",
                IsConfigured = false
            };

        DeploymentLocation current = root;

        for (int i = 1; i < parts.Length; i++)
        {
            var child =
                new DeploymentLocation
                {
                    Name = parts[i],
                    Type = "Location",
                    IsConfigured = false
                };

            current.Children.Add(child);
            current = child;
        }

        using HttpResponseMessage response =
            await _httpClient.PostAsJsonAsync(
                "api/Locations/validate",
                root);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content
                .ReadFromJsonAsync<LocationValidationResponse>();

        return result?.Valid ?? false;
    }

    private class LocationValidationResponse
    {
        public bool Valid { get; set; }
    }
}