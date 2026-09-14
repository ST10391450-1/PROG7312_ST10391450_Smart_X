using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Smart_X_UI.Models;
using Smart_X_UI.Services;
using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Smart_X_UI.Pages;

public partial class SensorAdd : UserControl
{
    private readonly SensorService _sensorService =
        new(ApiClient.HttpClient);

    private readonly LocationService _locationService =
        new(ApiClient.HttpClient);

    private static readonly Regex MacAddressRegex =
        new(
            @"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$",
            RegexOptions.Compiled);

    public event EventHandler<SensorRegistration>? SensorRegistered;

    public event EventHandler? BackRequested;

    public SensorAdd()
    {
        InitializeComponent();

        _ = LoadLocationsAsync();
    }

    private async Task LoadLocationsAsync()
    {
        try
        {
            var locations =
                await _locationService.GetLocationsAsync();

            LocationComboBox.ItemsSource = locations;
        }
        catch
        {
            LocationComboBox.ItemsSource = Array.Empty<string>();
        }
    }

    private async void RegisterSensorButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (!ValidateForm())
        {
            return;
        }

        RegisterSensorButton.IsEnabled = false;

        try
        {
            var sensor = new SensorRegistration
            {
                MacAddress = MacAddressTextBox.Text!.Trim(),
                NodeId = NodeIdTextBox.Text!.Trim(),
                Category =
                    ((ComboBoxItem)CategoryComboBox.SelectedItem!)
                    .Content?.ToString() ?? string.Empty,
                Location =
                    LocationComboBox.SelectedItem?.ToString() ?? string.Empty
            };

            var registeredSensor =
                await _sensorService.RegisterSensorAsync(sensor);

            RegistrationStatusText.Text =
                $"Sensor '{registeredSensor.NodeId}' registered successfully.";

            RegistrationStatusText.Foreground =
                Brushes.LimeGreen;

            RegistrationStatusText.IsVisible = true;

            SensorRegistered?.Invoke(
                this,
                registeredSensor);

            ClearForm();
        }
        catch (HttpRequestException ex)
        {
            RegistrationStatusText.Text =
                $"Could not connect to the Smart X API.\n\n{ex.Message}";

            RegistrationStatusText.Foreground =
                Brushes.OrangeRed;

            RegistrationStatusText.IsVisible = true;
        }
        catch (Exception ex)
        {
            RegistrationStatusText.Text =
                ex.Message;

            RegistrationStatusText.Foreground =
                Brushes.OrangeRed;

            RegistrationStatusText.IsVisible = true;
        }
        finally
        {
            RegisterSensorButton.IsEnabled = true;
        }
    }

    private bool ValidateForm()
    {
        bool valid = true;

        string mac =
            MacAddressTextBox.Text?.Trim() ?? string.Empty;

        if (!MacAddressRegex.IsMatch(mac))
        {
            MacAddressTextBox.BorderBrush =
                Brushes.OrangeRed;

            MacAddressErrorText.IsVisible = true;
            valid = false;
        }
        else
        {
            MacAddressTextBox.ClearValue(
                Border.BorderBrushProperty);

            MacAddressErrorText.IsVisible = false;
        }

        string nodeId =
            NodeIdTextBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            NodeIdTextBox.BorderBrush =
                Brushes.OrangeRed;

            NodeIdErrorText.IsVisible = true;
            valid = false;
        }
        else
        {
            NodeIdTextBox.ClearValue(
                Border.BorderBrushProperty);

            NodeIdErrorText.IsVisible = false;
        }

        if (CategoryComboBox.SelectedItem is not ComboBoxItem)
        {
            CategoryErrorText.IsVisible = true;
            valid = false;
        }
        else
        {
            CategoryErrorText.IsVisible = false;
        }

        if (LocationComboBox.SelectedItem is null)
        {
            LocationErrorText.IsVisible = true;
            valid = false;
        }
        else
        {
            LocationErrorText.IsVisible = false;
        }

        return valid;
    }

    private void MacAddressTextBox_TextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        MacAddressErrorText.IsVisible =
            !string.IsNullOrWhiteSpace(MacAddressTextBox.Text) &&
            !MacAddressRegex.IsMatch(
                MacAddressTextBox.Text.Trim());
    }

    private void NodeIdTextBox_TextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        NodeIdErrorText.IsVisible =
            string.IsNullOrWhiteSpace(
                NodeIdTextBox.Text);
    }

    private void ClearButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        ClearForm();
    }

    private void ClearForm()
    {
        MacAddressTextBox.Text = string.Empty;
        NodeIdTextBox.Text = string.Empty;
        CategoryComboBox.SelectedItem = null;
        LocationComboBox.SelectedItem = null;

        MacAddressErrorText.IsVisible = false;
        NodeIdErrorText.IsVisible = false;
        CategoryErrorText.IsVisible = false;
        LocationErrorText.IsVisible = false;

        MacAddressTextBox.ClearValue(
            Border.BorderBrushProperty);

        NodeIdTextBox.ClearValue(
            Border.BorderBrushProperty);

        RegistrationStatusText.IsVisible = false;
        RegistrationStatusText.Text = string.Empty;
    }

    private void BackButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke(
            this,
            EventArgs.Empty);
    }
}