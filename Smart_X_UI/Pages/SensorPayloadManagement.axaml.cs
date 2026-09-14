using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Smart_X_UI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Smart_X_UI.Pages;

public partial class SensorPayloadManagement : UserControl
{
    private static readonly string[] SupportedAttachmentPatterns =
    {
        "*.txt", "*.log", "*.cfg", "*.conf", "*.ini", "*.json", "*.xml", "*.yaml", "*.yml", "*.csv",
        "*.jpg", "*.jpeg", "*.png", "*.webp", "*.pdf"
    };

    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri("http://localhost:8080/"),
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly List<SensorRegistration> _sensors = new();

    public SensorPayloadManagement()
    {
        InitializeComponent();
        _ = LoadSensorsAsync();
    }

    private void BackToDashboardButton_Click(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow window)
        {
            window.ShowDashboard();
        }
    }

    private void ClearButton_Click(object? sender, RoutedEventArgs e)
    {
        MacAddressTextBox.Text = string.Empty;
        LocationTextBox.Text = string.Empty;
        NodeIdTextBox.Text = string.Empty;

        if (SensorCategoryComboBox.Items.Count > 0)
        {
            SensorCategoryComboBox.SelectedIndex = 0;
        }
    }

    private async Task LoadSensorsAsync()
    {
        try
        {
            List<SensorRegistration>? sensors =
                await HttpClient.GetFromJsonAsync<List<SensorRegistration>>("api/Sensors");

            _sensors.Clear();

            if (sensors != null)
            {
                _sensors.AddRange(sensors);
            }

            UpdateSensorList();
        }
        catch (HttpRequestException ex)
        {
            UpdateSensorList();

            await ShowMessage(
                "Could not connect to the Smart X API.\n\n" +
                "Make sure the API is running on port 8080.\n\n" +
                ex.Message);
        }
        catch (TaskCanceledException)
        {
            UpdateSensorList();

            await ShowMessage("The request to load sensors timed out.");
        }
        catch (Exception ex)
        {
            UpdateSensorList();

            await ShowMessage("Could not load sensors.\n\n" + ex.Message);
        }
    }

    private async void RegisterSensorButton_Click(object? sender, RoutedEventArgs e)
    {
        string macAddress = MacAddressTextBox.Text?.Trim() ?? string.Empty;
        string location = LocationTextBox.Text?.Trim() ?? string.Empty;
        string nodeId = NodeIdTextBox.Text?.Trim() ?? string.Empty;

        string category = (SensorCategoryComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
            ?? string.Empty;

        var sensor = new SensorRegistration
        {
            MacAddress = macAddress,
            Location = location,
            NodeId = nodeId,
            Category = category
        };

        try
        {
            using HttpResponseMessage response = await HttpClient.PostAsJsonAsync("api/Sensors", sensor);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                await ShowMessage(
                    "The Smart X API rejected the sensor registration.\n\n" +
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n\n" +
                    responseBody);

                return;
            }

            SensorRegistration registeredSensor = TryDeserializeSensor(responseBody) ?? sensor;

            _sensors.Add(registeredSensor);
            UpdateSensorList();
            ClearButton_Click(null, new RoutedEventArgs());

            await ShowMessage($"Sensor '{nodeId}' registered successfully.");
        }
        catch (HttpRequestException ex)
        {
            await ShowMessage(
                "Could not connect to the Smart X API.\n\n" +
                "Make sure the Docker container is running on port 8080.\n\n" +
                ex.Message);
        }
        catch (TaskCanceledException)
        {
            await ShowMessage("The sensor registration request timed out.");
        }
        catch (Exception ex)
        {
            await ShowMessage("An unexpected error occurred.\n\n" + ex.Message);
        }
    }

    private static SensorRegistration? TryDeserializeSensor(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SensorRegistration>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async void AttachFileButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SensorRegistration sensor })
        {
            return;
        }

        await AttachFileAsync(sensor);
    }

    private async Task AttachFileAsync(SensorRegistration sensor)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);

        if (topLevel == null)
        {
            await ShowMessage("Could not open the file picker.");
            return;
        }

        var options = new FilePickerOpenOptions
        {
            Title = $"Attach file to {sensor.NodeId}",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Supported files") { Patterns = SupportedAttachmentPatterns },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } }
            }
        };

        IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(options);

        if (files.Count == 0)
        {
            return;
        }

        string? filePath = files[0].TryGetLocalPath();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            await ShowMessage("The selected file could not be accessed.");
            return;
        }

        try
        {
            await UploadAttachmentAsync(sensor, filePath);

            await ShowMessage($"'{Path.GetFileName(filePath)}' was sent to the API for sensor '{sensor.NodeId}'.");
        }
        catch (HttpRequestException ex)
        {
            await ShowMessage("The Smart X API rejected the attachment.\n\n" + ex.Message);
        }
        catch (TaskCanceledException)
        {
            await ShowMessage("The attachment upload timed out.");
        }
        catch (Exception ex)
        {
            await ShowMessage("Could not upload the attachment.\n\n" + ex.Message);
        }
    }

    private static async Task UploadAttachmentAsync(SensorRegistration sensor, string filePath)
    {
        await using FileStream stream = File.OpenRead(filePath);

        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);

        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        using HttpResponseMessage response = await HttpClient.PostAsync(
            $"api/Sensors/{Uri.EscapeDataString(sensor.NodeId)}/attachments",
            content);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string error = await response.Content.ReadAsStringAsync();

        throw new HttpRequestException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n\n{error}");
    }

    private async void SimulatePayloadButton_Click(object? sender, RoutedEventArgs e)
    {
        if (SimulationSensorComboBox.SelectedItem is not SensorRegistration sensor)
        {
            await ShowMessage("Please select a sensor to simulate.");
            return;
        }

        try
        {
            using HttpResponseMessage response = await SendTelemetryRequestAsync(sensor);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                await ShowMessage(
                    "The Smart X API rejected the telemetry.\n\n" +
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n\n" +
                    responseBody);

                return;
            }

            UpdateTelemetryFromApiResponse(responseBody, sensor);

            await ShowMessage($"Telemetry sent successfully for '{sensor.NodeId}'.");
        }
        catch (HttpRequestException ex)
        {
            await ShowMessage(
                "Could not connect to the Smart X API.\n\n" +
                "Make sure the Docker container is running on port 8080.\n\n" +
                ex.Message);
        }
        catch (TaskCanceledException)
        {
            await ShowMessage("The telemetry request timed out.");
        }
        catch (Exception ex)
        {
            await ShowMessage("An unexpected error occurred.\n\n" + ex.Message);
        }
    }

    private static Task<HttpResponseMessage> SendTelemetryRequestAsync(SensorRegistration sensor)
    {
        return sensor.Category switch
        {
            "Environmental" => HttpClient.PostAsJsonAsync("api/Telemetry/temperature", new TelemetryPacket<float>
            {
                DeviceId = sensor.NodeId,
                Timestamp = DateTime.UtcNow,
                SensorCategory = sensor.Category,
                Value = 22.5f
            }),

            "Power Consumption" => HttpClient.PostAsJsonAsync("api/Telemetry/power", new TelemetryPacket<int>
            {
                DeviceId = sensor.NodeId,
                Timestamp = DateTime.UtcNow,
                SensorCategory = sensor.Category,
                Value = 450
            }),

            "Actuator" => HttpClient.PostAsJsonAsync("api/Telemetry/switch", new TelemetryPacket<bool>
            {
                DeviceId = sensor.NodeId,
                Timestamp = DateTime.UtcNow,
                SensorCategory = sensor.Category,
                Value = true
            }),

            _ => throw new InvalidOperationException($"Unsupported sensor category: {sensor.Category}")
        };
    }

    private void UpdateTelemetryFromApiResponse(string responseBody, SensorRegistration sensor)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(responseBody);
            JsonElement root = document.RootElement;

            string deviceId = GetJsonString(root, "deviceId") ?? sensor.NodeId;
            string category = GetJsonString(root, "sensorCategory") ?? sensor.Category;
            string value = GetJsonValue(root, "value") ?? "N/A";
            DateTime timestamp = GetJsonDateTime(root, "timestamp") ?? DateTime.UtcNow;

            string endpoint = sensor.Category switch
            {
                "Environmental" => "POST /api/Telemetry/temperature",
                "Power Consumption" => "POST /api/Telemetry/power",
                "Actuator" => "POST /api/Telemetry/switch",
                _ => "POST /api/Telemetry"
            };

            TelemetryStatusText.Text = "● RECEIVED";
            TelemetryStatusText.Foreground = Avalonia.Media.Brushes.LimeGreen;
            TelemetryDeviceIdText.Text = deviceId;
            TelemetryCategoryText.Text = category;
            TelemetryValueText.Text = value;
            TelemetryTimestampText.Text = timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            TelemetryEndpointText.Text = endpoint;
        }
        catch (JsonException)
        {
            TelemetryStatusText.Text = "● RECEIVED";
            TelemetryStatusText.Foreground = Avalonia.Media.Brushes.LimeGreen;
            TelemetryDeviceIdText.Text = sensor.NodeId;
            TelemetryCategoryText.Text = sensor.Category;
            TelemetryValueText.Text = responseBody;
            TelemetryTimestampText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            TelemetryEndpointText.Text = "API response";
        }
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static string? GetJsonValue(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.True => "ON",
            JsonValueKind.False => "OFF",
            _ => property.ToString()
        };
    }

    private static DateTime? GetJsonDateTime(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTime.TryParse(property.GetString(), out DateTime timestamp) ? timestamp : null;
    }

    private void UpdateSensorList()
    {
        ActiveSensorsText.Text = $"{_sensors.Count} ACTIVE";

        RegisteredSensorsItemsControl.ItemsSource = null;
        RegisteredSensorsItemsControl.ItemsSource = _sensors;

        SimulationSensorComboBox.ItemsSource = null;
        SimulationSensorComboBox.ItemsSource = _sensors;

        bool hasSensors = _sensors.Count > 0;

        SimulationSensorComboBox.IsEnabled = hasSensors;
        SimulatePayloadButton.IsEnabled = hasSensors;
        SimulationSensorComboBox.SelectedItem = hasSensors ? _sensors[0] : null;
    }

    private async Task ShowMessage(string message)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            Console.WriteLine(message);
            return;
        }

        var textBlock = new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 90,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(20) };
        panel.Children.Add(textBlock);
        panel.Children.Add(okButton);

        var dialog = new Window
        {
            Title = "Smart X",
            Width = 420,
            MinHeight = 170,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };

        okButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(owner);
    }
}
