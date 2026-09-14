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
    private readonly SensorService _sensorService =
        new(ApiClient.HttpClient);

    private readonly ObservableCollection<SensorRegistration> _sensors =
        new();

    private readonly List<SensorReadingRecord> _telemetry =
        new();

    private readonly DispatcherTimer _refreshTimer;

    private bool _refreshing;

    public event EventHandler? BackRequested;

    public event EventHandler? AddSensorRequested;

    public event EventHandler? AddLocationRequested;

    public event EventHandler<SensorRegistration>?
        SensorDetailsRequested;

    public SensorDashboard()
    {
        InitializeComponent();

        SensorsItemsControl.ItemsSource =
            _sensors;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };

        _refreshTimer.Tick +=
            RefreshTimer_Tick;

        _refreshTimer.Start();

        _ = RefreshDashboardAsync();
    }

    private async void RefreshTimer_Tick(
        object? sender,
        EventArgs e)
    {
        await RefreshDashboardAsync();
    }

    private async Task RefreshDashboardAsync()
    {
        if (_refreshing)
        {
            return;
        }

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

    private async Task LoadSensorsAsync()
    {
        ConnectionStatusText.Text =
            "CONNECTING...";

        StatusText.Text =
            "Loading sensors...";

        try
        {
            var sensors =
                await _sensorService.GetSensorsAsync();

            _sensors.Clear();

            foreach (var sensor in sensors)
            {
                _sensors.Add(sensor);
            }

            ConnectionStatusText.Text =
                "CONNECTED";
        }
        catch (HttpRequestException)
        {
            ConnectionStatusText.Text =
                "DISCONNECTED";

            StatusText.Text =
                "Could not connect to the Smart X API.";

            UpdateDashboard();
        }
        catch (TaskCanceledException)
        {
            ConnectionStatusText.Text =
                "TIMEOUT";

            StatusText.Text =
                "The Smart X API request timed out.";

            UpdateDashboard();
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text =
                "ERROR";

            StatusText.Text =
                ex.Message;

            UpdateDashboard();
        }
    }

    private async Task LoadTelemetryAsync()
    {
        try
        {
            using HttpResponseMessage response =
                await ApiClient.HttpClient.GetAsync(
                    "api/Telemetry");

            response.EnsureSuccessStatusCode();

            var readings =
                await response.Content
                    .ReadFromJsonAsync<
                        List<SensorReadingRecord>>();

            _telemetry.Clear();

            if (readings != null)
            {
                _telemetry.AddRange(readings);
            }

            StatusText.Text =
                $"{_sensors.Count} sensor(s) registered | " +
                $"{_telemetry.Count} telemetry reading(s)";

            UpdateDashboard();
        }
        catch (HttpRequestException)
        {
            ConnectionStatusText.Text =
                "DISCONNECTED";

            StatusText.Text =
                "Could not retrieve telemetry from the Smart X API.";

            UpdateDashboard();
        }
        catch (TaskCanceledException)
        {
            ConnectionStatusText.Text =
                "TIMEOUT";

            StatusText.Text =
                "The telemetry request timed out.";

            UpdateDashboard();
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text =
                "ERROR";

            StatusText.Text =
                ex.Message;

            UpdateDashboard();
        }
    }

    private void UpdateDashboard()
    {
        int sensorCount =
            _sensors.Count;

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
        // MATCH TELEMETRY TO REGISTERED SENSORS
        // =========================================================

        var registeredMacs =
            _sensors
                .Where(sensor =>
                    !string.IsNullOrWhiteSpace(
                        sensor.MacAddress))
                .Select(sensor =>
                    sensor.MacAddress.Trim())
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        var validTelemetry =
            _telemetry
                .Where(reading =>
                    !string.IsNullOrWhiteSpace(
                        reading.DeviceId) &&
                    registeredMacs.Contains(
                        reading.DeviceId.Trim()))
                .ToList();

        // =========================================================
        // SENSOR ACTIVE STATUS + CURRENT DATA
        // =========================================================

        int activeSensorCount = 0;

        foreach (var sensor in _sensors)
        {
            string macAddress =
                sensor.MacAddress.Trim();

            var latestReading =
                validTelemetry
                    .Where(reading =>
                        string.Equals(
                            reading.DeviceId?.Trim(),
                            macAddress,
                            StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(reading =>
                        DateTimeService.EnsureUtc(
                            reading.Timestamp))
                    .FirstOrDefault();

            // -----------------------------------------------------
            // No telemetry has ever been received
            // -----------------------------------------------------

            if (latestReading == null)
            {
                sensor.IsActive = false;

                sensor.CurrentDataText =
                    "No data";

                sensor.CurrentDataTimestampText =
                    "--";

                continue;
            }

            // -----------------------------------------------------
            // Determine whether the sensor is currently active
            // -----------------------------------------------------

            bool isActive =
                DateTimeService.IsRecent(
                    latestReading.Timestamp);

            sensor.IsActive =
                isActive;

            if (isActive)
            {
                activeSensorCount++;
            }

            // -----------------------------------------------------
            // Display the sensor's own latest reading
            //
            // IMPORTANT:
            // This is not the dashboard average/total.
            // It belongs specifically to this sensor.
            // -----------------------------------------------------

            sensor.CurrentDataText =
                FormatSensorValue(
                    sensor.Category,
                    latestReading.Value);

            sensor.CurrentDataTimestampText =
                DateTimeService.FormatLocalTime(
                    latestReading.Timestamp);
        }

        // =========================================================
        // TOP STATISTICS
        // =========================================================

        RegisteredSensorsText.Text =
            sensorCount.ToString();

        ActiveSensorsText.Text =
            $"{activeSensorCount} ACTIVE";

        int issues =
            sensorCount - activeSensorCount;

        IssuesText.Text =
            issues.ToString();

        // =========================================================
        // NETWORK OVERVIEW
        // =========================================================

        NetworkActiveText.Text =
            activeSensorCount.ToString();

        if (sensorCount == 0)
        {
            NetworkStatusText.Text =
                "No sensors registered";
        }
        else if (activeSensorCount == sensorCount)
        {
            NetworkStatusText.Text =
                "All registered sensors active";
        }
        else
        {
            NetworkStatusText.Text =
                $"{activeSensorCount} of " +
                $"{sensorCount} sensors active";
        }

        // =========================================================
        // SENSOR CATEGORIES
        // =========================================================

        EnvironmentalSensorsText.Text =
            $"{environmentalCount} sensor" +
            (environmentalCount == 1
                ? ""
                : "s");

        PowerSensorsText.Text =
            $"{powerCount} sensor" +
            (powerCount == 1
                ? ""
                : "s");

        ActuatorSensorsText.Text =
            $"{actuatorCount} sensor" +
            (actuatorCount == 1
                ? ""
                : "s");

        // =========================================================
        // TEMPERATURE
        // =========================================================

        var temperatureReadings =
            validTelemetry
                .Where(reading =>
                    string.Equals(
                        reading.SensorCategory,
                        "Environmental",
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (temperatureReadings.Count > 0)
        {
            double averageTemperature =
                temperatureReadings.Average(
                    reading => reading.Value);

            SensorReadingRecord latestTemperature =
                temperatureReadings
                    .OrderByDescending(reading =>
                        DateTimeService.EnsureUtc(
                            reading.Timestamp))
                    .First();

            AverageTemperatureText.Text =
                $"{averageTemperature:F1}°C";

            TemperatureStatusText.Text =
                $"{temperatureReadings.Count} reading(s)";

            LastUpdateText.Text =
                FormatTimestamp(
                    latestTemperature.Timestamp);
        }
        else
        {
            AverageTemperatureText.Text =
                "--";

            TemperatureStatusText.Text =
                environmentalCount == 0
                    ? "No environmental sensors"
                    : "Waiting for readings";
        }

        // =========================================================
        // POWER
        // =========================================================

        var powerReadings =
            validTelemetry
                .Where(reading =>
                    string.Equals(
                        reading.SensorCategory,
                        "Power Consumption",
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (powerReadings.Count > 0)
        {
            var latestPowerPerSensor =
                powerReadings
                    .GroupBy(
                        reading =>
                            reading.DeviceId.Trim(),
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group =>
                        group
                            .OrderByDescending(reading =>
                                DateTimeService.EnsureUtc(
                                    reading.Timestamp))
                            .First())
                    .ToList();

            double totalPower =
                latestPowerPerSensor.Sum(
                    reading => reading.Value);

            SensorReadingRecord latestPower =
                powerReadings
                    .OrderByDescending(reading =>
                        DateTimeService.EnsureUtc(
                            reading.Timestamp))
                    .First();

            TotalPowerText.Text =
                $"{totalPower:F0} W";

            PowerStatusText.Text =
                $"{latestPowerPerSensor.Count} " +
                "active power sensor(s)";

            LastUpdateText.Text =
                FormatTimestamp(
                    latestPower.Timestamp);
        }
        else
        {
            TotalPowerText.Text =
                "--";

            PowerStatusText.Text =
                powerCount == 0
                    ? "No power sensors"
                    : "Waiting for readings";
        }

        // =========================================================
        // LATEST SENSOR ACTIVITY
        // =========================================================

        if (validTelemetry.Count == 0)
        {
            LatestSensorActivityText.Text =
                sensorCount == 0
                    ? "No sensors registered"
                    : "Waiting for sensor telemetry...";

            LastUpdateText.Text =
                "--";
        }
        else
        {
            SensorReadingRecord latest =
                validTelemetry
                    .OrderByDescending(reading =>
                        DateTimeService.EnsureUtc(
                            reading.Timestamp))
                    .First();

            SensorRegistration? sensor =
                _sensors.FirstOrDefault(item =>
                    string.Equals(
                        item.MacAddress?.Trim(),
                        latest.DeviceId?.Trim(),
                        StringComparison.OrdinalIgnoreCase));

            string sensorName =
                sensor?.NodeId ??
                latest.DeviceId;

            LatestSensorActivityText.Text =
                $"{sensorName} | " +
                $"{latest.SensorCategory} | " +
                $"{FormatSensorValue(
                    latest.SensorCategory,
                    latest.Value)}";

            LastUpdateText.Text =
                FormatTimestamp(
                    latest.Timestamp);
        }

        // =========================================================
        // SENSOR LIST
        // =========================================================

        SensorsItemsControl.IsVisible =
            sensorCount > 0;
    }

    // =============================================================
    // FORMAT SENSOR DATA
    // =============================================================

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
            return value != 0
                ? "ON"
                : "OFF";
        }

        return value.ToString("F2");
    }

    // =============================================================
    // FORMAT TIMESTAMP
    // =============================================================

    private static string FormatTimestamp(
        DateTime timestamp)
    {
        return DateTimeService.FormatLocalTime(
            timestamp);
    }

    // =============================================================
    // REFRESH
    // =============================================================

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
        }
        else
        {
            await RefreshDashboardAsync();
        }
    }

    // =============================================================
    // NAVIGATION
    // =============================================================

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

    // =============================================================
    // SENSOR DETAILS
    // =============================================================

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