using Smart_X_UI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Smart_X_UI.Services;

// Reads a JSON file and imports its categories, locations and sensors.
//
// Accepted shapes:
//   1. Bundle:  { "categories": [...], "locations": [...], "sensors": [...] }
//   2. A bare array of sensors: [ { "nodeId": ..., ... } ]
//
// categories: "Name" or { "name": "...", "description": "..." }
// locations : "A / B / C" or { "name": "A", "children": [ ... ] }
// sensors   : { "macAddress", "nodeId", "category", "location", "isEnabled"? }
// Property names are case-insensitive.
public class SensorImportService
{
    private const long MaxFileBytes = 10 * 1024 * 1024;

    private readonly HttpClient _httpClient;

    public SensorImportService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<(SensorImportRequest Request, List<string> Warnings)>
        ParseFileAsync(string filePath)
    {
        var info = new FileInfo(filePath);

        if (!info.Exists)
            throw new FileNotFoundException("The selected file was not found.");

        if (info.Length > MaxFileBytes)
            throw new InvalidOperationException("The file is larger than 10 MB.");

        await using var stream = File.OpenRead(filePath);
        return await ParseStreamAsync(stream);
    }

    public async Task<(SensorImportRequest Request, List<string> Warnings)>
        ParseStreamAsync(Stream stream)
    {
        JsonDocument document;

        try
        {
            document = await JsonDocument.ParseAsync(
                stream,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"The file is not valid JSON: {ex.Message}");
        }

        using (document)
        {
            var warnings = new List<string>();
            var request = new SensorImportRequest();
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                ReadSensors(root, request, warnings);
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                if (TryGet(root, "categories", out var categories))
                    ReadCategories(categories, request, warnings);

                if (TryGet(root, "locations", out var locations))
                    ReadLocations(locations, request, warnings);

                if (TryGet(root, "sensors", out var sensors))
                    ReadSensors(sensors, request, warnings);
            }
            else
            {
                throw new InvalidOperationException(
                    "JSON must be an object with categories/locations/sensors, " +
                    "or an array of sensors.");
            }

            if (request.Categories.Count == 0 &&
                request.Locations.Count == 0 &&
                request.Sensors.Count == 0)
            {
                throw new InvalidOperationException(
                    "No categories, locations or sensors were found in the file.");
            }

            return (request, warnings);
        }
    }

    public async Task<SensorImportResult> ImportAsync(
        SensorImportRequest request)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/Sensors/import",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();

            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(body)
                    ? $"API request failed: {(int)response.StatusCode} {response.ReasonPhrase}"
                    : body,
                null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<SensorImportResult>()
               ?? new SensorImportResult();
    }

    // ---------- readers ----------

    private static void ReadCategories(
        JsonElement array,
        SensorImportRequest request,
        List<string> warnings)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            warnings.Add("'categories' must be an array and was ignored.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in array.EnumerateArray())
        {
            string name;
            string description = string.Empty;

            if (item.ValueKind == JsonValueKind.String)
            {
                name = item.GetString()?.Trim() ?? string.Empty;
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                name = GetString(item, "name") ?? string.Empty;
                description = GetString(item, "description") ?? string.Empty;
            }
            else
            {
                warnings.Add("A category entry was not text or an object and was skipped.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                warnings.Add("A category without a name was skipped.");
                continue;
            }

            if (!seen.Add(name))
                continue;

            request.Categories.Add(new SensorCategory
            {
                Name = name,
                Description = description
            });
        }
    }

    private static void ReadLocations(
        JsonElement array,
        SensorImportRequest request,
        List<string> warnings)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            warnings.Add("'locations' must be an array and was ignored.");
            return;
        }

        var paths = new List<string>();

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var path = item.GetString()?.Trim();

                if (!string.IsNullOrWhiteSpace(path))
                    paths.Add(path);
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                FlattenLocation(item, string.Empty, paths);
            }
            else
            {
                warnings.Add("A location entry was not text or an object and was skipped.");
            }
        }

        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            request.Locations.Add(path);
    }

    private static void FlattenLocation(
        JsonElement node,
        string parentPath,
        List<string> paths)
    {
        var name = GetString(node, "name");

        if (string.IsNullOrWhiteSpace(name))
            return;

        var path = string.IsNullOrEmpty(parentPath)
            ? name
            : $"{parentPath} / {name}";

        paths.Add(path);

        if (TryGet(node, "children", out var children) &&
            children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
            {
                if (child.ValueKind == JsonValueKind.Object)
                    FlattenLocation(child, path, paths);
            }
        }
    }

    private static void ReadSensors(
        JsonElement array,
        SensorImportRequest request,
        List<string> warnings)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            warnings.Add("'sensors' must be an array and was ignored.");
            return;
        }

        var index = 0;

        foreach (var item in array.EnumerateArray())
        {
            index++;

            if (item.ValueKind != JsonValueKind.Object)
            {
                warnings.Add($"Sensor #{index} is not an object and was skipped.");
                continue;
            }

            var sensor = new SensorRegistration
            {
                NodeId = GetString(item, "nodeId") ?? string.Empty,
                MacAddress = GetString(item, "macAddress", "mac") ?? string.Empty,
                Category = GetString(item, "category") ?? string.Empty,
                Location = GetString(item, "location") ?? string.Empty
            };

            if (TryGet(item, "isEnabled", out var enabled) &&
                (enabled.ValueKind == JsonValueKind.True ||
                 enabled.ValueKind == JsonValueKind.False))
            {
                sensor.IsEnabled = enabled.GetBoolean();
            }

            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(sensor.NodeId)) missing.Add("nodeId");
            if (string.IsNullOrWhiteSpace(sensor.MacAddress)) missing.Add("macAddress");
            if (string.IsNullOrWhiteSpace(sensor.Category)) missing.Add("category");
            if (string.IsNullOrWhiteSpace(sensor.Location)) missing.Add("location");

            if (missing.Count > 0)
            {
                warnings.Add(
                    $"Sensor #{index} skipped: missing {string.Join(", ", missing)}.");
                continue;
            }

            request.Sensors.Add(sensor);
        }
    }

    // ---------- helpers ----------

    private static bool TryGet(
        JsonElement obj,
        string name,
        out JsonElement value)
    {
        if (obj.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in obj.EnumerateObject())
            {
                if (property.Name.Equals(
                        name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? GetString(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGet(obj, name, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString()?.Trim();

                if (!string.IsNullOrEmpty(text))
                    return text;
            }
        }

        return null;
    }
}
