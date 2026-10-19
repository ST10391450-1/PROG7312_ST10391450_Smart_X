
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Smart_X_UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Smart_X_UI.Pages;

public partial class RealTimeCommandStream : UserControl
{
    private readonly ObservableCollection<string> _recommendations = new();
    private readonly ObservableCollection<string> _alerts = new();
    private readonly ObservableCollection<string> _disconnected = new();
    private readonly ObservableCollection<DeviceRow> _devices = new();
    private readonly ObservableCollection<DeviceRow> _filteredDevices = new();
    private readonly ObservableCollection<HistoryRow> _history = new();
    private readonly ObservableCollection<HistoryRow> _filteredHistory = new();
    private readonly Stack<CommandUndoEntry> _commandUndoStack = new();

    private readonly DispatcherTimer _refreshTimer = new()
    {
        Interval = TimeSpan.FromSeconds(3)
    };

    private bool _isLoading;
    private bool _isFiltering;
    private bool _categoriesLoaded;
    private bool _isUndoing;

    public RealTimeCommandStream()
    {
        InitializeComponent();

        RecommendationsList.ItemsSource = _recommendations;
        AlertsList.ItemsSource = _alerts;
        DisconnectedList.ItemsSource = _disconnected;
        DevicesItemsControl.ItemsSource = _filteredDevices;
        HistoryItemsControl.ItemsSource = _filteredHistory;

        UpdateUndoButtonState();

        _refreshTimer.Tick += RefreshTimer_Tick;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;

        DeviceCategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = "All Categories" });
        DeviceStateFilterComboBox.Items.Add(new ComboBoxItem { Content = "All States" });
        HistoryCategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = "All Categories" });

        DeviceCategoryFilterComboBox.SelectedIndex = 0;
        DeviceStateFilterComboBox.SelectedIndex = 0;
        HistoryCategoryFilterComboBox.SelectedIndex = 0;
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _refreshTimer.Start();
        await LoadInsightsAsync();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _refreshTimer.Stop();
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isLoading)
            await LoadInsightsAsync();
    }

    private async Task LoadInsightsAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        ConnectionStatusText.Text = "Connecting to API...";

        try
        {
            await LoadDevicesAsync();
            await LoadHistoryAsync();
            await LoadRecommendationsAsync();
            await LoadAlertsAsync();
            await LoadDisconnectedAsync();

            RegisteredDevicesValue.Text = _devices.Count.ToString();
            OnlineDevicesValue.Text = _devices.Count(x => x.IsEnabled).ToString();

            CommandsTodayValue.Text = _history.Count(x =>
                DateTime.TryParse(x.CreatedAt, CultureInfo.CurrentCulture, out var createdAt) &&
                createdAt.Date == DateTime.Today).ToString();

            DeviceCountText.Text = $"{_filteredDevices.Count} devices";

            ConnectionStatusText.Text =
                $"API connected · {_devices.Count} devices · " +
                $"{_history.Count} commands · Updated {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = $"API connection failed: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadDevicesAsync()
    {
        try
        {
            var telemetryByDevice = await LoadLatestTelemetryAsync();

            using var response = await ApiClient.HttpClient.GetAsync("api/Sensors");

            if (!response.IsSuccessStatusCode)
            {
                DeviceMessageText.Text =
                    $"Sensor request failed: HTTP {(int)response.StatusCode}";
                DeviceMessageText.IsVisible = true;
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var root = JsonNode.Parse(json);
            var items = GetArray(root, "sensors", "items", "data", "results", "value");

            if (items == null)
            {
                DeviceMessageText.Text =
                    "The sensor API returned an unrecognised JSON format.";
                DeviceMessageText.IsVisible = true;
                return;
            }

            _devices.Clear();

            foreach (var item in items)
            {
                if (item is not JsonObject sensor)
                    continue;

                var nodeId = ReadString(sensor, "nodeId", "NodeId", "deviceId", "DeviceId");
                var macAddress = ReadString(sensor, "macAddress", "MacAddress");
                var category = ReadString(sensor, "category", "Category", "sensorCategory", "SensorCategory");
                var location = ReadString(sensor, "location", "Location");

                var hasEnabledValue =
                    HasProperty(sensor, "isEnabled", "IsEnabled") ||
                    HasProperty(sensor, "isActive", "IsActive");

                var isEnabled = ReadBool(sensor, "isEnabled", "IsEnabled", "isActive", "IsActive");

                var displayName = !string.IsNullOrWhiteSpace(nodeId)
                    ? nodeId
                    : !string.IsNullOrWhiteSpace(macAddress)
                        ? macAddress
                        : "Unnamed device";

                var state = !hasEnabledValue
                    ? "Status unavailable"
                    : isEnabled ? "Enabled" : "Disabled";

                var telemetry = FindTelemetry(telemetryByDevice, nodeId, macAddress);

                var latestValue = telemetry?.Value ?? string.Empty;
                var latestTimestamp = telemetry?.Timestamp ?? string.Empty;
                var unit = telemetry?.Unit ?? string.Empty;

                if (string.IsNullOrWhiteSpace(latestValue))
                {
                    latestValue = ReadString(
                        sensor,
                        "latestValue", "LatestValue",
                        "sensorValue", "SensorValue",
                        "readingValue", "ReadingValue",
                        "lastReading", "LastReading",
                        "value", "Value",
                        "temperature", "Temperature",
                        "moisture", "Moisture",
                        "humidity", "Humidity");
                }

                if (string.IsNullOrWhiteSpace(latestTimestamp))
                {
                    latestTimestamp = ReadString(
                        sensor,
                        "latestTimestamp", "LatestTimestamp",
                        "lastSeen", "LastSeen",
                        "lastReadingAt", "LastReadingAt",
                        "timestamp", "Timestamp",
                        "recordedAt", "RecordedAt",
                        "createdAt", "CreatedAt",
                        "receivedAt", "ReceivedAt");
                }

                if (string.IsNullOrWhiteSpace(unit))
                {
                    unit = ReadString(sensor, "unit", "Unit", "measurementUnit", "MeasurementUnit");
                }

                if (string.IsNullOrWhiteSpace(latestValue))
                {
                    latestValue = "No telemetry available";
                }
                else if (!string.IsNullOrWhiteSpace(unit) &&
                         !latestValue.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
                {
                    latestValue = $"{latestValue} {unit}";
                }

                latestTimestamp = FormatTelemetryTimestamp(latestTimestamp);

                _devices.Add(new DeviceRow
                {
                    NodeId = nodeId,
                    DisplayName = displayName,
                    MacAddress = macAddress,
                    Category = string.IsNullOrWhiteSpace(category)
                        ? "Uncategorised"
                        : category,
                    Location = string.IsNullOrWhiteSpace(location)
                        ? "No location"
                        : location,
                    IsEnabled = hasEnabledValue && isEnabled,
                    StateText = state,
                    StateBackground = !hasEnabledValue
                        ? "#183044"
                        : isEnabled ? "#183D2B" : "#40252B",
                    StateForeground = !hasEnabledValue
                        ? "#DCE7ED"
                        : isEnabled ? "#8BF0B0" : "#FF9CA5",
                    LatestValue = latestValue,
                    LatestTimestamp = latestTimestamp,
                    EnableDisableLabel = !hasEnabledValue
                        ? "Status unavailable"
                        : isEnabled ? "Disable" : "Enable"
                });
            }

            DeviceMessageText.IsVisible = false;

            UpdateCategoryFilters();
            ApplyDeviceFilters();

            DeviceEmptyText.Text = _devices.Count == 0
                ? "No registered devices found."
                : "No devices match your search or filters.";

            DeviceEmptyText.IsVisible = _filteredDevices.Count == 0;
            DevicesItemsControl.IsVisible = _filteredDevices.Count > 0;
            DeviceCountText.Text = $"{_filteredDevices.Count} devices";
        }
        catch (JsonException)
        {
            DeviceMessageText.Text = "The sensor or telemetry API returned invalid JSON.";
            DeviceMessageText.IsVisible = true;
        }
        catch (HttpRequestException ex)
        {
            DeviceMessageText.Text = $"Could not reach the sensor or telemetry API: {ex.Message}";
            DeviceMessageText.IsVisible = true;
        }
        catch (TaskCanceledException)
        {
            DeviceMessageText.Text = "The sensor or telemetry API request timed out.";
            DeviceMessageText.IsVisible = true;
        }
        catch (Exception ex)
        {
            DeviceMessageText.Text = $"Could not load devices: {ex.Message}";
            DeviceMessageText.IsVisible = true;
        }
    }

    private async Task<Dictionary<string, TelemetryRow>> LoadLatestTelemetryAsync()
    {
        var latestByDevice = new Dictionary<string, TelemetryRow>(
            StringComparer.OrdinalIgnoreCase);

        try
        {
            using var response = await ApiClient.HttpClient.GetAsync("api/Telemetry");

            if (!response.IsSuccessStatusCode)
            {
                DeviceMessageText.Text =
                    $"Telemetry request failed: HTTP {(int)response.StatusCode}. Device registrations will still be loaded.";
                DeviceMessageText.IsVisible = true;
                return latestByDevice;
            }

            var json = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(json))
                return latestByDevice;

            var root = JsonNode.Parse(json);
            var items = GetArray(
                root,
                "telemetry",
                "readings",
                "measurements",
                "items",
                "data",
                "results",
                "value");

            if (items == null)
            {
                if (root is JsonObject singleReading &&
                    HasProperty(singleReading, "deviceId", "DeviceId", "nodeId", "NodeId"))
                {
                    AddTelemetryReading(latestByDevice, singleReading);
                }

                return latestByDevice;
            }

            foreach (var item in items)
            {
                if (item is JsonObject reading)
                    AddTelemetryReading(latestByDevice, reading);
            }
        }
        catch (HttpRequestException)
        {
            DeviceMessageText.Text =
                "Telemetry could not be reached. Registered devices will still be loaded.";
            DeviceMessageText.IsVisible = true;
        }
        catch (TaskCanceledException)
        {
            DeviceMessageText.Text =
                "The telemetry request timed out. Registered devices will still be loaded.";
            DeviceMessageText.IsVisible = true;
        }
        catch (JsonException)
        {
            DeviceMessageText.Text =
                "The telemetry API returned invalid JSON. Registered devices will still be loaded.";
            DeviceMessageText.IsVisible = true;
        }

        return latestByDevice;
    }

    private static void AddTelemetryReading(
        Dictionary<string, TelemetryRow> latestByDevice,
        JsonObject reading)
    {
        var deviceId = ReadString(
            reading,
            "deviceId", "DeviceId",
            "nodeId", "NodeId",
            "macAddress", "MacAddress");

        if (string.IsNullOrWhiteSpace(deviceId))
            return;

        var value = ReadString(
            reading,
            "value", "Value",
            "reading", "Reading",
            "sensorValue", "SensorValue",
            "measurement", "Measurement",
            "temperature", "Temperature",
            "moisture", "Moisture",
            "humidity", "Humidity");

        var timestamp = ReadString(
            reading,
            "timestamp", "Timestamp",
            "recordedAt", "RecordedAt",
            "createdAt", "CreatedAt",
            "receivedAt", "ReceivedAt",
            "time", "Time");

        var category = ReadString(
            reading,
            "sensorCategory", "SensorCategory",
            "category", "Category",
            "type", "Type");

        var unit = ReadString(
            reading,
            "unit", "Unit",
            "measurementUnit", "MeasurementUnit");

        var candidate = new TelemetryRow
        {
            DeviceId = deviceId.Trim(),
            Value = value,
            Timestamp = timestamp,
            Category = category,
            Unit = unit
        };

        if (!latestByDevice.TryGetValue(candidate.DeviceId, out var existing))
        {
            latestByDevice[candidate.DeviceId] = candidate;
            return;
        }

        var candidateTime = ParseTimestamp(candidate.Timestamp);
        var existingTime = ParseTimestamp(existing.Timestamp);

        if (candidateTime >= existingTime)
            latestByDevice[candidate.DeviceId] = candidate;
    }

    private static TelemetryRow? FindTelemetry(
        Dictionary<string, TelemetryRow> telemetryByDevice,
        string nodeId,
        string macAddress)
    {
        if (!string.IsNullOrWhiteSpace(nodeId) &&
            telemetryByDevice.TryGetValue(nodeId.Trim(), out var byNodeId))
        {
            return byNodeId;
        }

        if (!string.IsNullOrWhiteSpace(macAddress) &&
            telemetryByDevice.TryGetValue(macAddress.Trim(), out var byMac))
        {
            return byMac;
        }

        return null;
    }

    private static DateTimeOffset ParseTimestamp(string? value)
    {
        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var timestamp))
        {
            return timestamp;
        }

        return DateTimeOffset.MinValue;
    }

    private static string FormatTelemetryTimestamp(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "No reading recorded";

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var timestamp))
        {
            return timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        return value;
    }

    private void UpdateCategoryFilters()
    {
        var selectedDeviceCategory = GetSelectedText(DeviceCategoryFilterComboBox);
        var selectedState = GetSelectedText(DeviceStateFilterComboBox);
        var selectedHistoryCategory = GetSelectedText(HistoryCategoryFilterComboBox);

        DeviceCategoryFilterComboBox.Items.Clear();
        DeviceStateFilterComboBox.Items.Clear();
        HistoryCategoryFilterComboBox.Items.Clear();

        DeviceCategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = "All Categories" });
        DeviceStateFilterComboBox.Items.Add(new ComboBoxItem { Content = "All States" });
        HistoryCategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = "All Categories" });

        foreach (var category in _devices
                     .Select(x => x.Category)
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x))
        {
            DeviceCategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = category });
            HistoryCategoryFilterComboBox.Items.Add(new ComboBoxItem { Content = category });
        }

        foreach (var state in _devices
                     .Select(x => x.StateText)
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x))
        {
            DeviceStateFilterComboBox.Items.Add(new ComboBoxItem { Content = state });
        }

        SelectComboItem(DeviceCategoryFilterComboBox, selectedDeviceCategory);
        SelectComboItem(DeviceStateFilterComboBox, selectedState);
        SelectComboItem(HistoryCategoryFilterComboBox, selectedHistoryCategory);

        if (DeviceCategoryFilterComboBox.SelectedItem == null)
            DeviceCategoryFilterComboBox.SelectedIndex = 0;

        if (DeviceStateFilterComboBox.SelectedItem == null)
            DeviceStateFilterComboBox.SelectedIndex = 0;

        if (HistoryCategoryFilterComboBox.SelectedItem == null)
            HistoryCategoryFilterComboBox.SelectedIndex = 0;

        _categoriesLoaded = true;
    }

    private static void SelectComboItem(ComboBox comboBox, string value)
    {
        for (var i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is ComboBoxItem item &&
                string.Equals(
                    item.Content?.ToString(),
                    value,
                    StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            using var response = await ApiClient.HttpClient.GetAsync("api/Commands");

            if (!response.IsSuccessStatusCode)
            {
                HistoryMessageText.Text =
                    $"Command history request failed: HTTP {(int)response.StatusCode}";
                HistoryMessageText.IsVisible = true;
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var root = JsonNode.Parse(json);
            var items = GetArray(root, "commands", "items", "data", "results", "value");

            if (items == null)
            {
                HistoryMessageText.Text =
                    "Command history response format was not recognised.";
                HistoryMessageText.IsVisible = true;
                return;
            }

            _history.Clear();

            foreach (var item in items)
            {
                if (item is not JsonObject command)
                    continue;

                var nodeId = ReadString(command, "nodeId", "NodeId", "deviceId", "DeviceId");
                var category = ReadString(command, "category", "Category");

                if (string.IsNullOrWhiteSpace(category))
                {
                    category = _devices.FirstOrDefault(
                        x => string.Equals(
                            x.NodeId,
                            nodeId,
                            StringComparison.OrdinalIgnoreCase))
                        ?.Category ?? "Unknown";
                }

                var status = ReadString(command, "status", "Status");
                var result = ReadString(command, "result", "Result");
                var commandName = ReadString(command, "command", "Command");
                var createdAt = ReadString(command, "createdAt", "CreatedAt", "timestamp", "Timestamp");

                _history.Add(new HistoryRow
                {
                    Command = string.IsNullOrWhiteSpace(commandName)
                        ? "Unknown command"
                        : commandName,
                    NodeId = nodeId,
                    Category = category,
                    Result = !string.IsNullOrWhiteSpace(result) ? result : status,
                    CreatedAt = FormatTimestamp(createdAt)
                });
            }

            var sortedHistory = _history
                .OrderByDescending(x =>
                    DateTime.TryParse(x.CreatedAt, CultureInfo.CurrentCulture, out var timestamp)
                        ? timestamp
                        : DateTime.MinValue)
                .ToList();

            _history.Clear();

            foreach (var row in sortedHistory)
                _history.Add(row);

            HistoryMessageText.IsVisible = false;
            ApplyHistoryFilters();

            HistoryEmptyText.Text = _history.Count == 0
                ? "No command history available. Commands will appear here when recorded by the API."
                : "No commands match your search or category filter.";

            HistoryEmptyText.IsVisible = _filteredHistory.Count == 0;
            HistoryItemsControl.IsVisible = _filteredHistory.Count > 0;
        }
        catch (JsonException)
        {
            HistoryMessageText.Text = "The command history API returned invalid JSON.";
            HistoryMessageText.IsVisible = true;
        }
        catch (HttpRequestException ex)
        {
            HistoryMessageText.Text = $"Could not reach command history: {ex.Message}";
            HistoryMessageText.IsVisible = true;
        }
        catch (TaskCanceledException)
        {
            HistoryMessageText.Text = "The command history request timed out.";
            HistoryMessageText.IsVisible = true;
        }
        catch (Exception ex)
        {
            HistoryMessageText.Text = $"Could not load command history: {ex.Message}";
            HistoryMessageText.IsVisible = true;
        }
    }

    private static string FormatTimestamp(string value)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
            return timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");

        return value;
    }

    private async Task LoadRecommendationsAsync()
    {
        RecommendationsStatus.Text = "Loading recommendations...";

        try
        {
            using var response = await ApiClient.HttpClient.GetAsync("api/Insights/recommendations");

            if (!response.IsSuccessStatusCode)
            {
                RecommendationsStatus.Text = $"Request failed: {(int)response.StatusCode}";
                return;
            }

            ReplaceItems(_recommendations, await response.Content.ReadAsStringAsync());

            RecommendationsStatus.Text = _recommendations.Count == 0
                ? "No recommendations available."
                : $"{_recommendations.Count} recommendation(s) loaded.";
        }
        catch (Exception ex)
        {
            RecommendationsStatus.Text = $"Could not load recommendations: {ex.Message}";
        }
    }

    private async Task LoadAlertsAsync()
    {
        AlertsStatus.Text = "Loading active alerts...";

        try
        {
            using var response = await ApiClient.HttpClient.GetAsync("api/Insights/alerts");

            if (!response.IsSuccessStatusCode)
            {
                AlertsStatus.Text = $"Request failed: {(int)response.StatusCode}";
                return;
            }

            ReplaceItems(_alerts, await response.Content.ReadAsStringAsync());
            AddLocalDeviceWarnings();

            AlertsCount.Text = _alerts.Count.ToString();
            ActiveAlertsValue.Text = _alerts.Count.ToString();

            AlertsStatus.Text = _alerts.Count == 0
                ? "No active alerts."
                : $"{_alerts.Count} active alert(s).";
        }
        catch (Exception ex)
        {
            AlertsStatus.Text = $"Could not load alerts: {ex.Message}";
        }
    }

    private void AddLocalDeviceWarnings()
    {
        foreach (var device in _devices)
        {
            var valueText = device.LatestValue ?? string.Empty;
            var combined = $"{device.DisplayName} {device.Category} {valueText}";

            if (valueText.Contains("offline", StringComparison.OrdinalIgnoreCase) ||
                valueText.Contains("disconnected", StringComparison.OrdinalIgnoreCase) ||
                valueText.Contains("fault", StringComparison.OrdinalIgnoreCase) ||
                valueText.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                AddWarningIfMissing(
                    $"DEVICE WARNING · {device.DisplayName} ({device.NodeId}): latest device status or reading indicates offline, disconnected, fault, or error.");
            }

            if (IsMoistureDevice(combined) &&
                TryExtractNumber(valueText, out var moisture) &&
                moisture < 20)
            {
                AddWarningIfMissing(
                    $"CRITICAL MOISTURE · {device.DisplayName} ({device.NodeId}): reading {moisture:F1} is below 20%. Check irrigation requirements.");
            }

            if (IsTemperatureDevice(combined) &&
                TryExtractNumber(valueText, out var temperature) &&
                (temperature < 0 || temperature > 45))
            {
                AddWarningIfMissing(
                    $"TEMPERATURE WARNING · {device.DisplayName} ({device.NodeId}): reading {temperature:F1} is outside the configured 0–45 range.");
            }

            if (!device.IsEnabled)
            {
                AddWarningIfMissing(
                    $"DEVICE DISABLED · {device.DisplayName} ({device.NodeId}): this device is disabled and may not respond to commands.");
            }

            if (string.IsNullOrWhiteSpace(valueText) ||
                valueText.Equals("Unknown", StringComparison.OrdinalIgnoreCase) ||
                valueText.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                valueText.Equals("--", StringComparison.OrdinalIgnoreCase) ||
                valueText.Equals("No telemetry available", StringComparison.OrdinalIgnoreCase))
            {
                AddWarningIfMissing(
                    $"MISSING READING · {device.DisplayName} ({device.NodeId}): no usable latest reading is available.");
            }
        }
    }

    private void AddWarningIfMissing(string warning)
    {
        if (!_alerts.Any(x => x.Equals(warning, StringComparison.OrdinalIgnoreCase)))
            _alerts.Add(warning);
    }

    private static bool TryExtractNumber(string value, out double number)
    {
        var match = Regex.Match(value, @"[-+]?(?:\d+\.?\d*|\.\d+)");

        return double.TryParse(
            match.Success ? match.Value : null,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out number);
    }

    private static bool IsMoistureDevice(string value)
    {
        return value.Contains("moisture", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("soil", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTemperatureDevice(string value)
    {
        return value.Contains("temperature", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("temp", StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadDisconnectedAsync()
    {
        DisconnectedStatus.Text = "Loading disconnected devices...";

        try
        {
            using var response = await ApiClient.HttpClient.GetAsync("api/Insights/disconnected");

            if (!response.IsSuccessStatusCode)
            {
                DisconnectedStatus.Text = $"Request failed: {(int)response.StatusCode}";
                return;
            }

            ReplaceItems(_disconnected, await response.Content.ReadAsStringAsync());

            DisconnectedCount.Text = _disconnected.Count.ToString();

            DisconnectedStatus.Text = _disconnected.Count == 0
                ? "No disconnected devices reported."
                : $"{_disconnected.Count} disconnected device(s).";
        }
        catch (Exception ex)
        {
            DisconnectedStatus.Text = $"Could not load disconnected devices: {ex.Message}";
        }
    }

    private async Task SearchInsightsAsync()
    {
        var query = InsightsSearchTextBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            InsightsSearchStatus.Text = "Enter a search query.";
            return;
        }

        InsightsSearchButton.IsEnabled = false;
        InsightsSearchStatus.Text = "Submitting search...";

        try
        {
            var payload = JsonSerializer.Serialize(new { query });

            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await ApiClient.HttpClient.PostAsync("api/Insights/search", content);

            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                InsightsSearchStatus.Text =
                    $"Search failed ({(int)response.StatusCode}): {responseBody}";
                return;
            }

            var message = "Search recorded successfully.";

            try
            {
                var result = JsonNode.Parse(responseBody);
                message = result?["message"]?.ToString() ?? message;
            }
            catch (JsonException)
            {
                if (!string.IsNullOrWhiteSpace(responseBody))
                    message = responseBody;
            }

            InsightsSearchStatus.Text = $"{message} Query: {query}";
            await LoadRecommendationsAsync();
        }
        catch (Exception ex)
        {
            InsightsSearchStatus.Text = $"Could not submit search: {ex.Message}";
        }
        finally
        {
            InsightsSearchButton.IsEnabled = true;
        }
    }

    private static JsonArray? GetArray(JsonNode? root, params string[] propertyNames)
    {
        if (root is JsonArray array)
            return array;

        if (root is not JsonObject obj)
            return null;

        foreach (var propertyName in propertyNames)
        {
            if (obj.TryGetPropertyValue(propertyName, out var value) &&
                value is JsonArray result)
                return result;
        }

        return null;
    }

    private static string ReadString(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetPropertyValue(name, out var value) && value != null)
            {
                if (value is JsonObject or JsonArray)
                    continue;

                return value.ToString();
            }
        }

        return string.Empty;
    }

    private static bool ReadBool(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetPropertyValue(name, out var value) &&
                value != null &&
                bool.TryParse(value.ToString(), out var result))
            {
                return result;
            }
        }

        return false;
    }

    private static bool HasProperty(JsonObject obj, params string[] names)
    {
        return names.Any(name => obj.ContainsKey(name));
    }

    private static void ReplaceItems(ObservableCollection<string> target, string json)
    {
        target.Clear();

        if (string.IsNullOrWhiteSpace(json))
            return;

        JsonNode? root;

        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            target.Add(json);
            return;
        }

        if (root is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item != null)
                    target.Add(FormatItem(item));
            }

            return;
        }

        if (root is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (property.Value is JsonArray nestedArray)
                {
                    foreach (var item in nestedArray)
                    {
                        if (item != null)
                            target.Add(FormatItem(item));
                    }
                }
                else if (property.Value is not JsonObject && property.Value != null)
                {
                    target.Add($"{property.Key}: {FormatValue(property.Value)}");
                }
            }

            return;
        }

        if (root != null)
            target.Add(FormatItem(root));
    }

    private static string FormatItem(JsonNode item)
    {
        if (item is JsonObject obj)
        {
            var parts = new List<string>();

            foreach (var property in obj)
            {
                if (property.Value is JsonObject or JsonArray)
                    continue;

                parts.Add($"{property.Key}: {FormatValue(property.Value)}");
            }

            return parts.Count > 0 ? string.Join(" | ", parts) : obj.ToJsonString();
        }

        return FormatValue(item);
    }

    private static string FormatValue(JsonNode? value)
    {
        if (value == null)
            return "—";

        return value is JsonValue ? value.ToString() : value.ToJsonString();
    }

    private async void InsightsSearchButton_Click(object? sender, RoutedEventArgs e)
        => await SearchInsightsAsync();

    private async void InsightsSearchTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        await SearchInsightsAsync();
    }

    private async void RefreshInsightsButton_Click(object? sender, RoutedEventArgs e)
        => await LoadInsightsAsync();

    private async void RefreshButton_Click(object? sender, RoutedEventArgs e)
        => await LoadInsightsAsync();

    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();

        if (TopLevel.GetTopLevel(this) is MainWindow mainWindow)
            mainWindow.ShowHome();
    }

    private void DeviceSearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        => ApplyDeviceFilters();

    private void DeviceFilter_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_categoriesLoaded)
            ApplyDeviceFilters();
    }

    private void ApplyDeviceFilters()
    {
        if (_isFiltering)
            return;

        _isFiltering = true;

        try
        {
            _filteredDevices.Clear();

            var search = DeviceSearchTextBox.Text?.Trim() ?? string.Empty;
            var category = GetSelectedText(DeviceCategoryFilterComboBox);
            var state = GetSelectedText(DeviceStateFilterComboBox);

            foreach (var device in _devices)
            {
                var matchesSearch =
                    string.IsNullOrWhiteSpace(search) ||
                    device.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    device.NodeId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    device.MacAddress.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    device.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    device.Location.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    device.LatestValue.Contains(search, StringComparison.OrdinalIgnoreCase);

                var matchesCategory =
                    IsAllFilter(category) ||
                    device.Category.Equals(category, StringComparison.OrdinalIgnoreCase);

                var matchesState =
                    IsAllFilter(state) ||
                    device.StateText.Equals(state, StringComparison.OrdinalIgnoreCase);

                if (matchesSearch && matchesCategory && matchesState)
                    _filteredDevices.Add(device);
            }
        }
        finally
        {
            _isFiltering = false;
        }

        DeviceCountText.Text = $"{_filteredDevices.Count} devices";
        DeviceEmptyText.IsVisible = _filteredDevices.Count == 0;
        DevicesItemsControl.IsVisible = _filteredDevices.Count > 0;

        if (_filteredDevices.Count == 0)
        {
            DeviceEmptyText.Text = _devices.Count == 0
                ? "No registered devices found."
                : "No devices match your search or filters.";
        }
    }


    private async void DeviceOverride_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not DeviceRow device)
        {
            ConnectionStatusText.Text = "Could not identify the selected device.";
            return;
        }

        if (string.IsNullOrWhiteSpace(device.NodeId))
        {
            ConnectionStatusText.Text = "The selected device has no Node ID.";
            return;
        }

        if (!device.IsEnabled)
        {
            ConnectionStatusText.Text = "Enable the device before using manual override.";
            return;
        }

        if (!device.Category.Contains("actuator", StringComparison.OrdinalIgnoreCase) &&
            !device.Category.Contains("relay", StringComparison.OrdinalIgnoreCase) &&
            !device.Category.Contains("pump", StringComparison.OrdinalIgnoreCase))
        {
            ConnectionStatusText.Text =
                $"Manual override is only supported for ON/OFF functions. '{device.Category}' does not support this operation.";
            return;
        }

        ConnectionStatusText.Text =
            "WARNING: Manual Override only works for ON/OFF functions.";

        await SendCommandAsync(device, "ManualOverride");
    }


    private async void DeviceEnableDisable_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not DeviceRow device)
        {
            ConnectionStatusText.Text = "Could not identify the selected device.";
            return;
        }

        if (string.IsNullOrWhiteSpace(device.NodeId))
        {
            ConnectionStatusText.Text = "The selected device has no Node ID.";
            return;
        }

        if (device.StateText == "Status unavailable")
        {
            ConnectionStatusText.Text =
                "Cannot change this device because its enabled status is unavailable.";
            return;
        }

        var command = device.IsEnabled ? "DisableSensor" : "EnableSensor";
        await SendCommandAsync(device, command);
    }

    private async Task SendCommandAsync(DeviceRow device, string command)
    {
        try
        {
            ConnectionStatusText.Text = $"Sending {command} to {device.NodeId}...";

            var payload = JsonSerializer.Serialize(new
            {
                NodeId = device.NodeId,
                Command = command
            });

            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await ApiClient.HttpClient.PostAsync("api/Commands", content);

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                ConnectionStatusText.Text =
                    $"Command failed: HTTP {(int)response.StatusCode}. {body}";
                return;
            }

            var message = string.Equals(command, "ManualOverride", StringComparison.OrdinalIgnoreCase)
                ? "Manual override accepted. It cannot be undone because the API does not expose the previous actuator state."
                : "Command accepted by the API.";

            if (string.Equals(command, "EnableSensor", StringComparison.OrdinalIgnoreCase))
            {
                _commandUndoStack.Push(new CommandUndoEntry
                {
                    NodeId = device.NodeId,
                    OriginalCommand = command,
                    UndoCommand = "DisableSensor"
                });
                UpdateUndoButtonState();
            }
            else if (string.Equals(command, "DisableSensor", StringComparison.OrdinalIgnoreCase))
            {
                _commandUndoStack.Push(new CommandUndoEntry
                {
                    NodeId = device.NodeId,
                    OriginalCommand = command,
                    UndoCommand = "EnableSensor"
                });
                UpdateUndoButtonState();
            }

            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    var result = JsonNode.Parse(body);
                    var status = result?["status"]?.ToString() ?? result?["Status"]?.ToString();
                    var resultText = result?["result"]?.ToString() ?? result?["Result"]?.ToString();

                    if (!string.IsNullOrWhiteSpace(status))
                        message = status;

                    if (!string.IsNullOrWhiteSpace(resultText))
                        message += $": {resultText}";
                }
                catch (JsonException)
                {
                    message += $" Response: {body}";
                }
            }

            await LoadDevicesAsync();
            await LoadHistoryAsync();

            RegisteredDevicesValue.Text = _devices.Count.ToString();
            OnlineDevicesValue.Text = _devices.Count(x => x.IsEnabled).ToString();

            CommandsTodayValue.Text = _history.Count(x =>
                DateTime.TryParse(x.CreatedAt, CultureInfo.CurrentCulture, out var createdAt) &&
                createdAt.Date == DateTime.Today).ToString();

            ConnectionStatusText.Text = $"{command} for {device.NodeId}: {message}";
        }
        catch (HttpRequestException ex)
        {
            ConnectionStatusText.Text =
                $"Cannot reach the API. Check ApiClient's BaseAddress: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            ConnectionStatusText.Text = "The command request timed out.";
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = $"Command request failed: {ex.Message}";
        }
    }

    private async void UndoCommandButton_Click(object? sender, RoutedEventArgs e)
        => await UndoLastCommandAsync();

    private async Task UndoLastCommandAsync()
    {
        if (_isUndoing || _commandUndoStack.Count == 0)
            return;

        var entry = _commandUndoStack.Peek();
        _isUndoing = true;
        UpdateUndoButtonState();

        try
        {
            ConnectionStatusText.Text = $"Undoing {entry.OriginalCommand} for {entry.NodeId}...";

            var payload = JsonSerializer.Serialize(new
            {
                NodeId = entry.NodeId,
                Command = entry.UndoCommand
            });

            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await ApiClient.HttpClient.PostAsync("api/Commands", content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                ConnectionStatusText.Text =
                    $"Undo failed: HTTP {(int)response.StatusCode}. {body}";
                return;
            }

            _commandUndoStack.Pop();
            UpdateUndoButtonState();

            await LoadDevicesAsync();
            await LoadHistoryAsync();

            RegisteredDevicesValue.Text = _devices.Count.ToString();
            OnlineDevicesValue.Text = _devices.Count(x => x.IsEnabled).ToString();

            CommandsTodayValue.Text = _history.Count(x =>
                DateTime.TryParse(x.CreatedAt, CultureInfo.CurrentCulture, out var createdAt) &&
                createdAt.Date == DateTime.Today).ToString();

            var resultMessage = string.IsNullOrWhiteSpace(body)
                ? "The API accepted the inverse command."
                : body;

            try
            {
                var result = JsonNode.Parse(body);
                var status = result?["status"]?.ToString() ?? result?["Status"]?.ToString();
                var resultText = result?["result"]?.ToString() ?? result?["Result"]?.ToString();

                if (!string.IsNullOrWhiteSpace(status))
                    resultMessage = status;

                if (!string.IsNullOrWhiteSpace(resultText))
                    resultMessage += $": {resultText}";
            }
            catch (JsonException)
            {
                if (string.IsNullOrWhiteSpace(body))
                    resultMessage = "The API accepted the inverse command.";
            }

            ConnectionStatusText.Text =
                $"Undo successful: {entry.OriginalCommand} for {entry.NodeId} reversed. {resultMessage}";
        }
        catch (HttpRequestException ex)
        {
            ConnectionStatusText.Text = $"Cannot reach the API to undo the command: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            ConnectionStatusText.Text = "Undo request timed out. The command remains in undo history.";
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = $"Undo failed: {ex.Message}";
        }
        finally
        {
            _isUndoing = false;
            UpdateUndoButtonState();
        }
    }

    private void UpdateUndoButtonState()
    {
        if (UndoCommandButton != null)
        {
            UndoCommandButton.IsEnabled = _commandUndoStack.Count > 0 && !_isUndoing;
            UndoCommandButton.Content = _commandUndoStack.Count > 0
                ? $"Undo Last Command ({_commandUndoStack.Count})"
                : "Undo Last Command";
        }
    }

    private void CommandHistorySearchTextBox_TextChanged(object? sender, TextChangedEventArgs e)
        => ApplyHistoryFilters();

    private void HistoryFilter_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (_categoriesLoaded)
            ApplyHistoryFilters();
    }

    private void ApplyHistoryFilters()
    {
        _filteredHistory.Clear();

        var search = CommandHistorySearchTextBox.Text?.Trim() ?? string.Empty;
        var category = GetSelectedText(HistoryCategoryFilterComboBox);

        foreach (var item in _history)
        {
            var matchesSearch =
                string.IsNullOrWhiteSpace(search) ||
                item.Command.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.NodeId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Result.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Category.Contains(search, StringComparison.OrdinalIgnoreCase);

            var matchesCategory =
                IsAllFilter(category) ||
                item.Category.Equals(category, StringComparison.OrdinalIgnoreCase);

            if (matchesSearch && matchesCategory)
                _filteredHistory.Add(item);
        }

        HistoryCountText.Text = $"{_filteredHistory.Count} commands";

        HistoryEmptyText.Text = _history.Count == 0
            ? "No command history available. Commands will appear here when recorded by the API."
            : "No commands match your search or category filter.";

        HistoryEmptyText.IsVisible = _filteredHistory.Count == 0;
        HistoryItemsControl.IsVisible = _filteredHistory.Count > 0;
    }

    private async void ExportHistoryButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);

            if (topLevel?.StorageProvider is not { CanSave: true } storageProvider)
            {
                ConnectionStatusText.Text = "File saving is unavailable.";
                return;
            }

            var lines = new List<string>
            {
                "Command,NodeId,Category,Result,CreatedAt"
            };

            foreach (var item in _filteredHistory)
            {
                lines.Add(string.Join(",",
                    Csv(item.Command),
                    Csv(item.NodeId),
                    Csv(item.Category),
                    Csv(item.Result),
                    Csv(item.CreatedAt)));
            }

            var file = await storageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Export Command History",
                    SuggestedFileName = $"CommandHistory_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    DefaultExtension = "csv",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("CSV file")
                        {
                            Patterns = new[] { "*.csv" }
                        }
                    }
                });

            if (file == null)
            {
                ConnectionStatusText.Text = "CSV export cancelled.";
                return;
            }

            await using (var stream = await file.OpenWriteAsync())
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(true)))
            {
                await writer.WriteAsync(string.Join(Environment.NewLine, lines));
            }

            ConnectionStatusText.Text =
                $"CSV exported successfully: {_filteredHistory.Count} records.";
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = $"CSV export failed: {ex.Message}";
        }
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string GetSelectedText(ComboBox comboBox)
    {
        if (comboBox.SelectedItem is ComboBoxItem item)
            return item.Content?.ToString()?.Trim() ?? string.Empty;

        return comboBox.SelectedItem?.ToString()?.Trim() ?? string.Empty;
    }

    private static bool IsAllFilter(string value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               value.Equals("All", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("All Categories", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("All States", StringComparison.OrdinalIgnoreCase);
    }
}

public class DeviceRow
{
    public string DisplayName { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string StateText { get; set; } = string.Empty;
    public string StateBackground { get; set; } = "#183044";
    public string StateForeground { get; set; } = "#DCE7ED";
    public string LatestValue { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string LatestTimestamp { get; set; } = string.Empty;
    public string EnableDisableLabel { get; set; } = "Enable";
    public bool IsEnabled { get; set; }
}

public class CommandUndoEntry
{
    public string NodeId { get; set; } = string.Empty;
    public string OriginalCommand { get; set; } = string.Empty;
    public string UndoCommand { get; set; } = string.Empty;
}

public class HistoryRow
{
    public string Command { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string ResultForeground { get; set; } = "#DCE7ED";
    public string CreatedAt { get; set; } = string.Empty;
}

internal sealed class TelemetryRow
{
    public string DeviceId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
}
