using Smart_X_UI.Models;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Smart_X_UI.Pages;

public partial class SensorPayloadManagement
{
    private async void SimulatePayloadButton_Click(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (SimulationSensorComboBox.SelectedItem
            is not SensorRegistration sensor)
        {
            await ShowMessage(
                "Please select a sensor to simulate.");

            return;
        }

        try
        {
            using HttpResponseMessage response =
                await SendTelemetryRequestAsync(sensor);

            string responseBody =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                await ShowMessage(
                    "The Smart X API rejected the telemetry.\n\n" +
                    $"HTTP {(int)response.StatusCode} " +
                    $"{response.ReasonPhrase}\n\n" +
                    responseBody);

                return;
            }

            UpdateTelemetryFromApiResponse(
                responseBody,
                sensor);

            await ShowMessage(
                $"Telemetry sent successfully for " +
                $"'{sensor.NodeId}'.");
        }
        catch (HttpRequestException ex)
        {
            await ShowMessage(
                "Could not connect to the Smart X API.\n\n" +
                "Make sure the Docker container is running " +
                "on port 8080.\n\n" +
                ex.Message);
        }
        catch (TaskCanceledException)
        {
            await ShowMessage(
                "The telemetry request timed out.");
        }
        catch (Exception ex)
        {
            await ShowMessage(
                "An unexpected error occurred.\n\n" +
                ex.Message);
        }
    }

    private static Task<HttpResponseMessage>
        SendTelemetryRequestAsync(
            SensorRegistration sensor)
    {
        return sensor.Category switch
        {
            "Environmental" =>
                HttpClient.PostAsJsonAsync(
                    "api/Telemetry/temperature",
                    new TelemetryPacket<float>
                    {
                        DeviceId = sensor.NodeId,
                        Timestamp = DateTime.UtcNow,
                        SensorCategory = sensor.Category,
                        Value = 22.5f
                    }),

            "Power Consumption" =>
                HttpClient.PostAsJsonAsync(
                    "api/Telemetry/power",
                    new TelemetryPacket<int>
                    {
                        DeviceId = sensor.NodeId,
                        Timestamp = DateTime.UtcNow,
                        SensorCategory = sensor.Category,
                        Value = 450
                    }),

            "Actuator" =>
                HttpClient.PostAsJsonAsync(
                    "api/Telemetry/switch",
                    new TelemetryPacket<bool>
                    {
                        DeviceId = sensor.NodeId,
                        Timestamp = DateTime.UtcNow,
                        SensorCategory = sensor.Category,
                        Value = true
                    }),

            _ => throw new InvalidOperationException(
                $"Unsupported sensor category: " +
                $"{sensor.Category}")
        };
    }

    private void UpdateTelemetryFromApiResponse(
        string responseBody,
        SensorRegistration sensor)
    {
        try
        {
            using JsonDocument document =
                JsonDocument.Parse(responseBody);

            JsonElement root = document.RootElement;

            string deviceId =
                GetJsonString(root, "deviceId")
                ?? sensor.NodeId;

            string category =
                GetJsonString(root, "sensorCategory")
                ?? sensor.Category;

            string value =
                GetJsonValue(root, "value")
                ?? "N/A";

            DateTime timestamp =
                GetJsonDateTime(root, "timestamp")
                ?? DateTime.UtcNow;

            string endpoint = sensor.Category switch
            {
                "Environmental" =>
                    "POST /api/Telemetry/temperature",

                "Power Consumption" =>
                    "POST /api/Telemetry/power",

                "Actuator" =>
                    "POST /api/Telemetry/switch",

                _ => "POST /api/Telemetry"
            };

            TelemetryStatusText.Text = "● RECEIVED";
            TelemetryStatusText.Foreground =
                Avalonia.Media.Brushes.LimeGreen;

            TelemetryDeviceIdText.Text = deviceId;
            TelemetryCategoryText.Text = category;
            TelemetryValueText.Text = value;

            TelemetryTimestampText.Text =
                timestamp
                    .ToLocalTime()
                    .ToString("yyyy-MM-dd HH:mm:ss");

            TelemetryEndpointText.Text = endpoint;
        }
        catch (JsonException)
        {
            TelemetryStatusText.Text = "● RECEIVED";
            TelemetryStatusText.Foreground =
                Avalonia.Media.Brushes.LimeGreen;

            TelemetryDeviceIdText.Text = sensor.NodeId;
            TelemetryCategoryText.Text = sensor.Category;
            TelemetryValueText.Text = responseBody;

            TelemetryTimestampText.Text =
                DateTime.Now.ToString(
                    "yyyy-MM-dd HH:mm:ss");

            TelemetryEndpointText.Text =
                "API response";
        }
    }

    private static string? GetJsonString(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out JsonElement property))
        {
            return null;
        }

        return property.ValueKind ==
               JsonValueKind.String
            ? property.GetString()
            : property.ToString();
    }

    private static string? GetJsonValue(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out JsonElement property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String =>
                property.GetString(),

            JsonValueKind.True =>
                "ON",

            JsonValueKind.False =>
                "OFF",

            _ => property.ToString()
        };
    }

    private static DateTime? GetJsonDateTime(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out JsonElement property))
        {
            return null;
        }

        if (property.ValueKind !=
            JsonValueKind.String)
        {
            return null;
        }

        return DateTime.TryParse(
            property.GetString(),
            out DateTime timestamp)
            ? timestamp
            : null;
    }
}