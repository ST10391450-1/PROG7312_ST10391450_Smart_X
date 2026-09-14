using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Smart_X_UI.Models;
using Smart_X_UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Smart_X_UI.Pages;

public partial class SensorDashboard : UserControl
{
    #region Fields

    private readonly SensorService _sensorService =
        new(ApiClient.HttpClient);

    private readonly ObservableCollection<SensorRegistration> _sensors = new();
    private readonly List<SensorReadingRecord> _telemetry = new();
    private readonly DispatcherTimer _refreshTimer;

    private bool _refreshing;

    #endregion

    #region Events

    public event EventHandler? BackRequested;
    public event EventHandler? AddSensorRequested;
    public event EventHandler? AddLocationRequested;
    public event EventHandler<SensorRegistration>? SensorDetailsRequested;

    #endregion

    #region Constructor

    public SensorDashboard()
    {
        InitializeComponent();

        SensorsItemsControl.ItemsSource = _sensors;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };

        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();

        _ = RefreshDashboardAsync();
    }

    #endregion

    #region Dashboard Refresh

    // Refreshes the dashboard every few seconds.
    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        await RefreshDashboardAsync();
    }

    // Loads the latest sensor and telemetry information.
    private async Task RefreshDashboardAsync()
    {
        if (_refreshing)
            return;

        _refreshing = true;

        try
        {
            await LoadSensorsAsync();
            await LoadTelemetryAsync();
        }
        finally
        {
            _refreshing = false;
        }
    }

    // Gets the registered sensors from the API.
    private async Task LoadSensorsAsync()
    {
        ConnectionStatusText.Text = "CONNECTING...";
        StatusText.Text = "Loading sensors...";

        try
        {
            var sensors = await _sensorService.GetSensorsAsync();

            _sensors.Clear();

            foreach (var sensor in sensors)
                _sensors.Add(sensor);

            ConnectionStatusText.Text = "CONNECTED";
        }
        catch (HttpRequestException)
        {
            ConnectionStatusText.Text = "DISCONNECTED";
            StatusText.Text = "Could not connect to the Smart X API.";
            UpdateDashboard();
        }
        catch (TaskCanceledException)
        {
            ConnectionStatusText.Text = "TIMEOUT";
            StatusText.Text = "The Smart X API request timed out.";
            UpdateDashboard();
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = "ERROR";
            StatusText.Text = ex.Message;
            UpdateDashboard();
        }
    }

    // Gets the latest telemetry readings from the API.
    private async Task LoadTelemetryAsync()
    {
        try
        {
            using var response = await ApiClient.HttpClient.GetAsync(
                "api/Telemetry");

            response.EnsureSuccessStatusCode();

            var readings = await response.Content
                .ReadFromJsonAsync<List<SensorReadingRecord>>();

            _telemetry.Clear();

            if (readings != null)
                _telemetry.AddRange(readings);

            StatusText.Text =
                $"{_sensors.Count} sensor(s) registered | " +
                $"{_telemetry.Count} telemetry reading(s)";

            UpdateDashboard();
        }
        catch (HttpRequestException)
        {
            ConnectionStatusText.Text = "DISCONNECTED";
            StatusText.Text =
                "Could not retrieve telemetry from the Smart X API.";

            UpdateDashboard();
        }
        catch (TaskCanceledException)
        {
            ConnectionStatusText.Text = "TIMEOUT";
            StatusText.Text =
                "The telemetry request timed out.";

            UpdateDashboard();
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = "ERROR";
            StatusText.Text = ex.Message;
            UpdateDashboard();
        }
    }

    #endregion

    #region Dashboard Data

    // Updates the statistics and current sensor information.
    private void UpdateDashboard()
    {
        var sensorCount = _sensors.Count;

        var environmentalCount = _sensors.Count(sensor =>
            string.Equals(
                sensor.Category,
                "Environmental",
                StringComparison.OrdinalIgnoreCase));

        var powerCount = _sensors.Count(sensor =>
            string.Equals(
                sensor.Category,
                "Power Consumption",
                StringComparison.OrdinalIgnoreCase));

        var actuatorCount = _sensors.Count(sensor =>
            string.Equals(
                sensor.Category,
                "Actuator",
                StringComparison.OrdinalIgnoreCase));

        // Only use telemetry belonging to registered sensors.
        var registeredMacs = _sensors
            .Where(sensor => !string.IsNullOrWhiteSpace(sensor.MacAddress))
            .Select(sensor => sensor.MacAddress.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var validTelemetry = _telemetry
            .Where(reading =>
                !string.IsNullOrWhiteSpace(reading.DeviceId) &&
                registeredMacs.Contains(reading.DeviceId.Trim()))
            .ToList();

        var activeSensorCount = 0;

        // Find the latest reading for each registered sensor.
        foreach (var sensor in _sensors)
        {
            var macAddress = sensor.MacAddress.Trim();

            var latestReading = validTelemetry
                .Where(reading =>
                    string.Equals(
                        reading.DeviceId?.Trim(),
                        macAddress,
                        StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(reading =>
                    DateTimeService.EnsureUtc(reading.Timestamp))
                .FirstOrDefault();

            if (latestReading == null)
            {
                sensor.IsActive = false;
                sensor.CurrentDataText = "No data";
                sensor.CurrentDataTimestampText = "--";
                continue;
            }

            var isActive = DateTimeService.IsRecent(
                latestReading.Timestamp);

            sensor.IsActive = isActive;

            if (isActive)
                activeSensorCount++;

            sensor.CurrentDataText = FormatSensorValue(
                sensor.Category,
                latestReading.Value);

            sensor.CurrentDataTimestampText =
                DateTimeService.FormatLocalTime(
                    latestReading.Timestamp);
        }

        RegisteredSensorsText.Text = sensorCount.ToString();
        ActiveSensorsText.Text = $"{activeSensorCount} ACTIVE";

        var issues = sensorCount - activeSensorCount;
        IssuesText.Text = issues.ToString();

        NetworkActiveText.Text = activeSensorCount.ToString();

        if (sensorCount == 0)
        {
            NetworkStatusText.Text = "No sensors registered";
        }
        else if (activeSensorCount == sensorCount)
        {
            NetworkStatusText.Text = "All registered sensors active";
        }
        else
        {
            NetworkStatusText.Text =
                $"{activeSensorCount} of {sensorCount} sensors active";
        }

        EnvironmentalSensorsText.Text =
            $"{environmentalCount} sensor" +
            (environmentalCount == 1 ? "" : "s");

        PowerSensorsText.Text =
            $"{powerCount} sensor" +
            (powerCount == 1 ? "" : "s");

        ActuatorSensorsText.Text =
            $"{actuatorCount} sensor" +
            (actuatorCount == 1 ? "" : "s");

        UpdateTemperature(validTelemetry, environmentalCount);
        UpdatePower(validTelemetry, powerCount);
        UpdateLatestActivity(validTelemetry, sensorCount);

        SensorsItemsControl.IsVisible = sensorCount > 0;
    }

    // Updates the temperature information shown on the dashboard.
    private void UpdateTemperature(
        List<SensorReadingRecord> telemetry,
        int environmentalCount)
    {
        var readings = telemetry
            .Where(reading =>
                string.Equals(
                    reading.SensorCategory,
                    "Environmental",
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (readings.Count == 0)
        {
            AverageTemperatureText.Text = "--";
            TemperatureStatusText.Text =
                environmentalCount == 0
                    ? "No environmental sensors"
                    : "Waiting for readings";

            return;
        }

        var averageTemperature = readings.Average(
            reading => reading.Value);

        var latest = readings
            .OrderByDescending(reading =>
                DateTimeService.EnsureUtc(reading.Timestamp))
            .First();

        AverageTemperatureText.Text =
            $"{averageTemperature:F1}°C";

        TemperatureStatusText.Text =
            $"{readings.Count} reading(s)";

        LastUpdateText.Text =
            FormatTimestamp(latest.Timestamp);
    }

    // Updates the total power usage shown on the dashboard.
    private void UpdatePower(
        List<SensorReadingRecord> telemetry,
        int powerCount)
    {
        var readings = telemetry
            .Where(reading =>
                string.Equals(
                    reading.SensorCategory,
                    "Power Consumption",
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (readings.Count == 0)
        {
            TotalPowerText.Text = "--";
            PowerStatusText.Text =
                powerCount == 0
                    ? "No power sensors"
                    : "Waiting for readings";

            return;
        }

        var latestPowerPerSensor = readings
            .GroupBy(
                reading => reading.DeviceId.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(reading =>
                    DateTimeService.EnsureUtc(reading.Timestamp))
                .First())
            .ToList();

        var totalPower = latestPowerPerSensor.Sum(
            reading => reading.Value);

        var latest = readings
            .OrderByDescending(reading =>
                DateTimeService.EnsureUtc(reading.Timestamp))
            .First();

        TotalPowerText.Text = $"{totalPower:F0} W";

        PowerStatusText.Text =
            $"{latestPowerPerSensor.Count} active power sensor(s)";

        LastUpdateText.Text =
            FormatTimestamp(latest.Timestamp);
    }

    // Shows the most recent telemetry activity.
    private void UpdateLatestActivity(
        List<SensorReadingRecord> telemetry,
        int sensorCount)
    {
        if (telemetry.Count == 0)
        {
            LatestSensorActivityText.Text =
                sensorCount == 0
                    ? "No sensors registered"
                    : "Waiting for sensor telemetry...";

            LastUpdateText.Text = "--";
            return;
        }

        var latest = telemetry
            .OrderByDescending(reading =>
                DateTimeService.EnsureUtc(reading.Timestamp))
            .First();

        var sensor = _sensors.FirstOrDefault(item =>
            string.Equals(
                item.MacAddress?.Trim(),
                latest.DeviceId?.Trim(),
                StringComparison.OrdinalIgnoreCase));

        var sensorName = sensor?.NodeId ?? latest.DeviceId;

        LatestSensorActivityText.Text =
            $"{sensorName} | " +
            $"{latest.SensorCategory} | " +
            $"{FormatSensorValue(
                latest.SensorCategory,
                latest.Value)}";

        LastUpdateText.Text =
            FormatTimestamp(latest.Timestamp);
    }

    #endregion

    #region Formatting

    // Formats a reading based on the sensor category.
    private static string FormatSensorValue(
        string category,
        double value)
    {
        if (string.Equals(
            category,
            "Environmental",
            StringComparison.OrdinalIgnoreCase))
        {
            return $"{value:F1} °C";
        }

        if (string.Equals(
            category,
            "Power Consumption",
            StringComparison.OrdinalIgnoreCase))
        {
            return $"{value:F0} W";
        }

        if (string.Equals(
            category,
            "Actuator",
            StringComparison.OrdinalIgnoreCase))
        {
            return value != 0 ? "ON" : "OFF";
        }

        return value.ToString("F2");
    }

    // Formats a timestamp for display.
    private static string FormatTimestamp(DateTime timestamp)
    {
        return DateTimeService.FormatLocalTime(timestamp);
    }

    #endregion

    #region Buttons

    // Manually refreshes the dashboard.
    private async void RefreshButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.IsEnabled = false;

            try
            {
                await RefreshDashboardAsync();
            }
            finally
            {
                button.IsEnabled = true;
            }

            return;
        }

        await RefreshDashboardAsync();
    }

    // Returns to the previous page.
    private void BackButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    // Opens the add location page.
    private void AddLocationButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        AddLocationRequested?.Invoke(this, EventArgs.Empty);
    }

    // Opens the add sensor page.
    private void AddSensorButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        AddSensorRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Sensor Details

    // Opens the details page for a sensor.
    private void SensorDetailsButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is Button button &&
            button.Tag is SensorRegistration sensor)
        {
            SensorDetailsRequested?.Invoke(this, sensor);
        }
    }

    #endregion
}

