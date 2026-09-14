using Avalonia.Controls;
using Avalonia.Interactivity;
using Smart_X_UI.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Smart_X_UI.Pages;

public partial class SensorPayloadManagement
{
    private static readonly Regex MacAddressRegex = new(
        @"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$",
        RegexOptions.Compiled);

    private async Task LoadSensorsAsync()
    {
        try
        {
            List<SensorRegistration>? sensors =
                await HttpClient.GetFromJsonAsync<List<SensorRegistration>>("api/Sensors");

            _sensors.Clear();

            if (sensors != null)
            {
                foreach (SensorRegistration sensor in sensors)
                {
                    _sensors.Add(sensor);
                }
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

    private async void RegisterSensorButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        string macAddress = MacAddressTextBox.Text?.Trim() ?? string.Empty;
        string location = LocationTextBox.Text?.Trim() ?? string.Empty;
        string nodeId = NodeIdTextBox.Text?.Trim() ?? string.Empty;

        string category =
            (SensorCategoryComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(macAddress))
        {
            await ShowMessage("Please enter a MAC address.");
            return;
        }

        if (!MacAddressRegex.IsMatch(macAddress))
        {
            await ShowMessage(
                "Please enter a valid MAC address.\n\n" +
                "Example: 00:1A:2B:3C:4D:5E");
            return;
        }

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            await ShowMessage("Please enter a node ID.");
            return;
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            await ShowMessage("Please enter a deployment location.");
            return;
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            await ShowMessage("Please select a sensor category.");
            return;
        }

        var sensor = new SensorRegistration
        {
            MacAddress = macAddress,
            Location = location,
            NodeId = nodeId,
            Category = category
        };

        try
        {
            using HttpResponseMessage response =
                await HttpClient.PostAsJsonAsync("api/Sensors", sensor);

            string responseBody =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                await ShowMessage(
                    "The Smart X API rejected the sensor registration.\n\n" +
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}\n\n" +
                    responseBody);

                return;
            }

            SensorRegistration registeredSensor =
                TryDeserializeSensor(responseBody) ?? sensor;

            _sensors.Add(registeredSensor);

            UpdateSensorList();

            ClearButton_Click(null, new RoutedEventArgs());

            await ShowMessage(
                $"Sensor '{nodeId}' registered successfully.");
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
            await ShowMessage(
                "The sensor registration request timed out.");
        }
        catch (Exception ex)
        {
            await ShowMessage(
                "An unexpected error occurred.\n\n" +
                ex.Message);
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
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void ClearButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        MacAddressTextBox.Text = string.Empty;
        LocationTextBox.Text = string.Empty;
        NodeIdTextBox.Text = string.Empty;

        if (SensorCategoryComboBox.Items.Count > 0)
        {
            SensorCategoryComboBox.SelectedIndex = 0;
        }
    }

    private void UpdateSensorList()
    {
        ActiveSensorsText.Text = $"{_sensors.Count} ACTIVE";

        bool hasSensors = _sensors.Count > 0;

        SimulationSensorComboBox.IsEnabled = hasSensors;
        SimulatePayloadButton.IsEnabled = hasSensors;

        if (hasSensors && SimulationSensorComboBox.SelectedItem == null)
        {
            SimulationSensorComboBox.SelectedItem = _sensors[0];
        }
        else if (!hasSensors)
        {
            SimulationSensorComboBox.SelectedItem = null;
        }
    }
}