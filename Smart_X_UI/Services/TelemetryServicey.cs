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

    public Task<HttpResponseMessage> SendTelemetryAsync(
        SensorRegistration sensor)
    {
        return sensor.Category switch
        {
            "Environmental" =>
                _httpClient.PostAsJsonAsync(
                    "api/Telemetry/temperature",
                    new TelemetryPacket<float>
                    {
                        DeviceId = sensor.NodeId,
                        Timestamp = DateTime.UtcNow,
                        SensorCategory = sensor.Category,
                        Value = 22.5f
                    }),

            "Power Consumption" =>
                _httpClient.PostAsJsonAsync(
                    "api/Telemetry/power",
                    new TelemetryPacket<int>
                    {
                        DeviceId = sensor.NodeId,
                        Timestamp = DateTime.UtcNow,
                        SensorCategory = sensor.Category,
                        Value = 450
                    }),

            "Actuator" =>
                _httpClient.PostAsJsonAsync(
                    "api/Telemetry/switch",
                    new TelemetryPacket<bool>
                    {
                        DeviceId = sensor.NodeId,
                        Timestamp = DateTime.UtcNow,
                        SensorCategory = sensor.Category,
                        Value = true
                    }),

            _ => throw new InvalidOperationException(
                $"Unsupported sensor category: {sensor.Category}")
        };
    }

    public static string GetEndpoint(
        SensorRegistration sensor)
    {
        return sensor.Category switch
        {
            "Environmental" =>
                "POST /api/Telemetry/temperature",

            "Power Consumption" =>
                "POST /api/Telemetry/power",

            "Actuator" =>
                "POST /api/Telemetry/switch",

            _ => "POST /api/Telemetry"
        };
    }
}