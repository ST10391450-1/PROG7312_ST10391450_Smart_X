
using Smart_X_UI.Models;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Smart_X_UI.Services;

public class TelemetryService
{
    private readonly HttpClient _httpClient;

    public TelemetryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<HttpResponseMessage> SendTelemetryAsync(SensorRegistration sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        if (string.IsNullOrWhiteSpace(sensor.NodeId))
            throw new ArgumentException("Sensor Node ID is required.", nameof(sensor));

        var category = sensor.Category?.Trim();

        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Sensor category is required.", nameof(sensor));

        if (!sensor.IsActive)
            throw new InvalidOperationException(
                $"Sensor '{sensor.NodeId}' is inactive.");

        if (category.Equals("Environmental", StringComparison.OrdinalIgnoreCase))
        {
            return _httpClient.PostAsJsonAsync(
                "api/Telemetry/temperature",
                new TelemetryPacket<float>
                {
                    DeviceId = sensor.NodeId,
                    Timestamp = DateTime.UtcNow,
                    SensorCategory = category,
                    Value = 22.5f
                });
        }

        if (category.Equals("Power Consumption", StringComparison.OrdinalIgnoreCase))
        {
            return _httpClient.PostAsJsonAsync(
                "api/Telemetry/power",
                new TelemetryPacket<int>
                {
                    DeviceId = sensor.NodeId,
                    Timestamp = DateTime.UtcNow,
                    SensorCategory = category,
                    Value = 450
                });
        }

        if (category.Equals("Actuator", StringComparison.OrdinalIgnoreCase))
        {
            return _httpClient.PostAsJsonAsync(
                "api/Telemetry/switch",
                new TelemetryPacket<bool>
                {
                    DeviceId = sensor.NodeId,
                    Timestamp = DateTime.UtcNow,
                    SensorCategory = category,
                    Value = true
                });
        }

        throw new InvalidOperationException(
            $"Unsupported sensor category: {category}");
    }

    public static string GetEndpoint(SensorRegistration sensor)
    {
        ArgumentNullException.ThrowIfNull(sensor);

        var category = sensor.Category?.Trim();

        if (string.Equals(
            category,
            "Environmental",
            StringComparison.OrdinalIgnoreCase))
            return "POST /api/Telemetry/temperature";

        if (string.Equals(
            category,
            "Power Consumption",
            StringComparison.OrdinalIgnoreCase))
            return "POST /api/Telemetry/power";

        if (string.Equals(
            category,
            "Actuator",
            StringComparison.OrdinalIgnoreCase))
            return "POST /api/Telemetry/switch";

        return "POST /api/Telemetry";
    }
}
