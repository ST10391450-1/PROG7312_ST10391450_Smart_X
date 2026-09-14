using Avalonia.Controls;
using Avalonia.Interactivity;
using Smart_X_UI.Models;
using Smart_X_UI.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Smart_X_UI.Pages;

public partial class SensorDashboard : UserControl
{
    private readonly SensorService _sensorService =
        new(ApiClient.HttpClient);

    private readonly ObservableCollection<SensorRegistration> _sensors =
        new();

    public event EventHandler? BackRequested;

    public event EventHandler? AddSensorRequested;

    public event EventHandler? AddLocationRequested;

    public event EventHandler<SensorRegistration>?
        SensorDetailsRequested;

    public SensorDashboard()
    {
        InitializeComponent();

        SensorsItemsControl.ItemsSource = _sensors;

        _ = LoadSensorsAsync();
    }

    private async Task LoadSensorsAsync()
    {
        ConnectionStatusText.Text = "CONNECTING...";
        StatusText.Text = "Loading sensors...";

        try
        {
            var sensors =
                await _sensorService.GetSensorsAsync();

            _sensors.Clear();

            foreach (var sensor in sensors)
            {
                _sensors.Add(sensor);
            }

            ConnectionStatusText.Text = "CONNECTED";

            StatusText.Text =
                $"{_sensors.Count} sensor(s) registered";
        }
        catch (HttpRequestException)
        {
            ConnectionStatusText.Text = "DISCONNECTED";

            StatusText.Text =
                "Could not connect to the Smart X API.";
        }
        catch (TaskCanceledException)
        {
            ConnectionStatusText.Text = "TIMEOUT";

            StatusText.Text =
                "The Smart X API request timed out.";
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = "ERROR";

            StatusText.Text =
                ex.Message;
        }

        UpdateDashboard();
    }

    private void UpdateDashboard()
    {
        int sensorCount = _sensors.Count;

        int environmentalCount =
            _sensors.Count(sensor =>
                string.Equals(
                    sensor.Category,
                    "Environmental",
                    StringComparison.OrdinalIgnoreCase));

        int powerCount =
            _sensors.Count(sensor =>
                string.Equals(
                    sensor.Category,
                    "Power Consumption",
                    StringComparison.OrdinalIgnoreCase));

        int actuatorCount =
            _sensors.Count(sensor =>
                string.Equals(
                    sensor.Category,
                    "Actuator",
                    StringComparison.OrdinalIgnoreCase));


        // =========================================================
        // TOP STATISTICS
        // =========================================================

        RegisteredSensorsText.Text =
            sensorCount.ToString();

        ActiveSensorsText.Text =
            $"{sensorCount} ACTIVE";

        IssuesText.Text = "0";


        // =========================================================
        // NETWORK OVERVIEW
        // =========================================================

        NetworkActiveText.Text =
            sensorCount.ToString();

        if (sensorCount == 0)
        {
            NetworkStatusText.Text =
                "No sensors registered";
        }
        else
        {
            NetworkStatusText.Text =
                "All registered sensors available";
        }


        // =========================================================
        // SENSOR CATEGORIES
        // =========================================================

        EnvironmentalSensorsText.Text =
            $"{environmentalCount} sensor" +
            (environmentalCount == 1 ? "" : "s");

        PowerSensorsText.Text =
            $"{powerCount} sensor" +
            (powerCount == 1 ? "" : "s");

        ActuatorSensorsText.Text =
            $"{actuatorCount} sensor" +
            (actuatorCount == 1 ? "" : "s");


        // =========================================================
        // SENSOR OVERVIEW
        // =========================================================

        if (sensorCount == 0)
        {
            AverageTemperatureText.Text = "--";
            TemperatureStatusText.Text = "No readings";

            TotalPowerText.Text = "--";
            PowerStatusText.Text = "No readings";

            LatestSensorActivityText.Text =
                "Waiting for sensor telemetry...";

            LastUpdateText.Text = "--";
        }
        else
        {
            AverageTemperatureText.Text = "--";
            TemperatureStatusText.Text =
                $"{environmentalCount} environmental sensor(s)";

            TotalPowerText.Text = "--";
            PowerStatusText.Text =
                $"{powerCount} power sensor(s)";

            LatestSensorActivityText.Text =
                "Telemetry available from registered sensors";

            LastUpdateText.Text =
                "Connected";
        }


        // =========================================================
        // SENSOR LIST
        // =========================================================

        SensorsItemsControl.IsVisible =
            sensorCount > 0;
    }

    private async void RefreshButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.IsEnabled = false;

            try
            {
                await LoadSensorsAsync();
            }
            finally
            {
                button.IsEnabled = true;
            }
        }
        else
        {
            await LoadSensorsAsync();
        }
    }

    private void BackButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void AddLocationButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        AddLocationRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void AddSensorButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        AddSensorRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void SensorDetailsButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is Button button &&
            button.Tag is SensorRegistration sensor)
        {
            SensorDetailsRequested?.Invoke(
                this,
                sensor);
        }
    }
}