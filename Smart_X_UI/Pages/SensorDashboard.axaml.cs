
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

    private readonly ObservableCollection<SensorRegistration> _sensors = new();
    private readonly ObservableCollection<SensorRegistration> _visibleSensors = new();
    private readonly List<SensorReadingRecord> _telemetry = new();
    private readonly DispatcherTimer _refreshTimer;

    private bool _refreshing;

    public event EventHandler? BackRequested;
    public event EventHandler? AddSensorRequested;
    public event EventHandler? AddLocationRequested;
    public event EventHandler? ManageCategoriesRequested;
    public event EventHandler<SensorRegistration>? SensorDetailsRequested;

    public SensorDashboard()
    {
        InitializeComponent();

        SensorsItemsControl.ItemsSource = _visibleSensors;

        SensorSearchBox.TextChanged += SensorSearchBox_TextChanged;
        CategoryFilter.SelectionChanged += Filter_SelectionChanged;
        StatusFilter.SelectionChanged += Filter_SelectionChanged;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };

        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();

        _ = RefreshDashboardAsync();
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        await RefreshDashboardAsync();
    }

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

    private async Task LoadSensorsAsync()
    {
        ConnectionStatusText.Text = "CONNECTING...";
        StatusText.Text = "Loading sensors...";

        try
        {
            var sensors = await _sensorService.GetSensorsAsync();

            _sensors.Clear();

            foreach (var sensor in sensors)
            {
                sensor.Unit = ResolveUnit(sensor.Category);
                _sensors.Add(sensor);
            }

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

    private async Task LoadTelemetryAsync()
    {
        try
        {
            using var response =
                await ApiClient.HttpClient.GetAsync("api/Telemetry");

            response.EnsureSuccessStatusCode();

            var readings = await response.Content
                .ReadFromJsonAsync<List<SensorReadingRecord>>();

            _telemetry.Clear();

            if (readings != null)
                _telemetry.AddRange(readings);

            UpdateDashboard();

            StatusText.Text =
                $"{_sensors.Count} sensor(s) registered | " +
                $"{_telemetry.Count} telemetry reading(s)";
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
            StatusText.Text = "The telemetry request timed out.";
            UpdateDashboard();
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = "ERROR";
            StatusText.Text = ex.Message;
            UpdateDashboard();
        }
    }

    private void UpdateDashboard()
    {
        var sensorCount = _sensors.Count;

        var environmentalCount = _sensors.Count(sensor =>
            IsEnvironmentalCategory(sensor.Category));

        var powerCount = _sensors.Count(sensor =>
            IsPowerCategory(sensor.Category));

        var actuatorCount = _sensors.Count(sensor =>
            IsActuatorCategory(sensor.Category));

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

        foreach (var sensor in _sensors)
        {
            sensor.Unit = ResolveUnit(sensor.Category);

            var macAddress = sensor.MacAddress?.Trim();

            var latestReading = validTelemetry
                .Where(reading =>
                    !string.IsNullOrWhiteSpace(macAddress) &&
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

            sensor.IsActive =
                DateTimeService.IsRecent(latestReading.Timestamp);

            if (sensor.IsActive)
                activeSensorCount++;

            sensor.CurrentDataText = FormatSensorValue(
                sensor.Category,
                latestReading.Value);

            sensor.CurrentDataTimestampText =
                DateTimeService.FormatLocalTime(
                    latestReading.Timestamp);
        }

        RegisteredSensorsText.Text = sensorCount.ToString();
        ActiveSensorsText.Text = activeSensorCount.ToString();
        IssuesText.Text = (sensorCount - activeSensorCount).ToString();
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
        ApplyFilters();

        SensorsItemsControl.IsVisible = _visibleSensors.Count > 0;
    }

    private void UpdateTemperature(
        List<SensorReadingRecord> telemetry,
        int environmentalCount)
    {
        var readings = telemetry
            .Where(reading =>
                ResolveUnit(GetRegisteredCategory(reading.DeviceId,
                    reading.SensorCategory)) == "°C")
            .ToList();

        if (readings.Count == 0)
        {
            AverageTemperatureText.Text = "--";
            TemperatureStatusText.Text =
                environmentalCount == 0
                    ? "No temperature sensors"
                    : "No readings for a Temperature category";

            return;
        }

        var averageTemperature = readings.Average(reading => reading.Value);

        AverageTemperatureText.Text = $"{averageTemperature:F1}";
        TemperatureStatusText.Text =
            $"{readings.Count} temperature reading(s)";
    }

    private void UpdatePower(
        List<SensorReadingRecord> telemetry,
        int powerCount)
    {
        var readings = telemetry
            .Where(reading =>
                ResolveUnit(GetRegisteredCategory(reading.DeviceId,
                    reading.SensorCategory)) == "W")
            .ToList();

        if (readings.Count == 0)
        {
            TotalPowerText.Text = "--";
            PowerStatusText.Text =
                powerCount == 0
                    ? "No power sensors"
                    : "No readings for a Power category";

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

        TotalPowerText.Text =
            $"{latestPowerPerSensor.Sum(reading => reading.Value):F0}";

        PowerStatusText.Text =
            $"{latestPowerPerSensor.Count} power sensor(s)";
    }

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

            LastUpdateText.Text = "Last update: --";
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

        var category = sensor?.Category ?? latest.SensorCategory;
        var sensorName = sensor?.NodeId ?? latest.DeviceId;
        var value = FormatSensorValue(category, latest.Value);
        var unit = ResolveUnit(category);

        LatestSensorActivityText.Text =
            $"{sensorName} | {category} | {value}" +
            (string.IsNullOrWhiteSpace(unit) ? "" : $" {unit}");

        LastUpdateText.Text =
            $"Last update: {FormatTimestamp(latest.Timestamp)}";
    }

    private string GetRegisteredCategory(
        string? deviceId,
        string? fallbackCategory)
    {
        var sensor = _sensors.FirstOrDefault(item =>
            string.Equals(
                item.MacAddress?.Trim(),
                deviceId?.Trim(),
                StringComparison.OrdinalIgnoreCase));

        return sensor?.Category ?? fallbackCategory ?? "";
    }

    private static string ResolveUnit(string? category)
    {
        var normalized = NormalizeCategory(category);

        return normalized switch
        {
            "temperature" or
            "temperature sensor" or
            "air temperature" or
            "water temperature" or
            "soil temperature" => "°C",

            "humidity" or
            "relative humidity" or
            "air humidity" => "%RH",

            "soil moisture" or
            "moisture" => "%",

            "co2" or
            "carbon dioxide" => "ppm",

            "pressure" or
            "air pressure" or
            "atmospheric pressure" => "hPa",

            "wind speed" => "m/s",
            "wind direction" => "°",

            "rainfall" or
            "rain gauge" => "mm",

            "light level" or
            "illuminance" => "lx",

            "voltage" => "V",
            "current" => "A",

            "power" or
            "power consumption" or
            "active power" => "W",

            "energy" or
            "energy consumption" => "kWh",

            "water flow" or
            "flow rate" => "L/min",

            "distance" => "m",
            "battery level" => "%",

            _ => ""
        };
    }

    private static string NormalizeCategory(string? category)
    {
        return (category ?? "")
            .Trim()
            .ToLowerInvariant()
            .Replace("_", " ")
            .Replace("-", " ")
            .Replace("  ", " ");
    }

    private static bool IsEnvironmentalCategory(string? category)
    {
        var normalized = NormalizeCategory(category);

        return normalized is
            "environmental" or
            "temperature" or
            "temperature sensor" or
            "air temperature" or
            "water temperature" or
            "soil temperature" or
            "humidity" or
            "relative humidity" or
            "air humidity" or
            "soil moisture" or
            "moisture" or
            "co2" or
            "carbon dioxide" or
            "pressure" or
            "air pressure" or
            "atmospheric pressure" or
            "wind speed" or
            "wind direction" or
            "rainfall" or
            "rain gauge" or
            "light level" or
            "illuminance";
    }

    private static bool IsPowerCategory(string? category)
    {
        var normalized = NormalizeCategory(category);

        return normalized is
            "power" or
            "power consumption" or
            "active power" or
            "voltage" or
            "current" or
            "energy" or
            "energy consumption";
    }

    private static bool IsActuatorCategory(string? category)
    {
        return NormalizeCategory(category) is
            "actuator" or
            "relay" or
            "smart relay" or
            "water pump" or
            "pump";
    }

    private static string FormatSensorValue(
        string? category,
        double value)
    {
        var normalized = NormalizeCategory(category);

        if (IsActuatorCategory(normalized))
            return value != 0 ? "ON" : "OFF";

        return ResolveUnit(category) switch
        {
            "°C" => value.ToString("F1"),
            "%RH" => value.ToString("F1"),
            "%" => value.ToString("F1"),
            "ppm" => value.ToString("F0"),
            "hPa" => value.ToString("F1"),
            "m/s" => value.ToString("F1"),
            "°" => value.ToString("F0"),
            "mm" => value.ToString("F1"),
            "lx" => value.ToString("F0"),
            "V" => value.ToString("F2"),
            "A" => value.ToString("F2"),
            "W" => value.ToString("F1"),
            "kWh" => value.ToString("F2"),
            "L/min" => value.ToString("F2"),
            "m" => value.ToString("F2"),
            _ => value.ToString("F2")
        };
    }

    private static string FormatTimestamp(DateTime timestamp)
    {
        return DateTimeService.FormatLocalTime(timestamp);
    }

    private void SensorSearchBox_TextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void Filter_SelectionChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var search = SensorSearchBox?.Text?.Trim() ?? "";
        var selectedCategory = GetSelectedContent(CategoryFilter);
        var selectedStatus = GetSelectedContent(StatusFilter);

        var filtered = _sensors.Where(sensor =>
        {
            var matchesSearch =
                string.IsNullOrWhiteSpace(search) ||
                Contains(sensor.NodeId, search) ||
                Contains(sensor.Location, search) ||
                Contains(sensor.Category, search) ||
                Contains(sensor.MacAddress, search);

            var matchesCategory =
                selectedCategory == "All categories" ||
                MatchesCategoryFilter(sensor.Category, selectedCategory);

            var matchesStatus = selectedStatus switch
            {
                "Online" => sensor.IsActive,
                "Offline" => !sensor.IsActive,
                "Issue" => !sensor.IsActive,
                _ => true
            };

            return matchesSearch && matchesCategory && matchesStatus;
        }).ToList();

        _visibleSensors.Clear();

        foreach (var sensor in filtered)
            _visibleSensors.Add(sensor);

        SensorsItemsControl.IsVisible = _visibleSensors.Count > 0;

        if (_sensors.Count == 0)
        {
            StatusText.Text = "No sensors registered.";
        }
        else
        {
            StatusText.Text =
                $"{_visibleSensors.Count} of {_sensors.Count} sensor(s) shown | " +
                $"{_telemetry.Count} telemetry reading(s)";
        }
    }

    private static string GetSelectedContent(ComboBox? comboBox)
    {
        if (comboBox?.SelectedItem is ComboBoxItem item)
            return item.Content?.ToString() ?? "";

        return "";
    }

    private static bool Contains(string? source, string search)
    {
        return !string.IsNullOrWhiteSpace(source) &&
               source.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesCategoryFilter(
        string? category,
        string selectedCategory)
    {
        return selectedCategory switch
        {
            "Environmental" => IsEnvironmentalCategory(category),
            "Power" => IsPowerCategory(category),
            "Actuator" => IsActuatorCategory(category),
            "Other" =>
                !IsEnvironmentalCategory(category) &&
                !IsPowerCategory(category) &&
                !IsActuatorCategory(category),
            _ => true
        };
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

    private void ManageCategoriesButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        ManageCategoriesRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BackButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void AddLocationButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        AddLocationRequested?.Invoke(this, EventArgs.Empty);
    }

    private void AddSensorButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        AddSensorRequested?.Invoke(this, EventArgs.Empty);
    }

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
}
