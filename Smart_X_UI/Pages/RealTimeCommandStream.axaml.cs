using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Smart_X_UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    private readonly DispatcherTimer _refreshTimer = new()
    {
        Interval = TimeSpan.FromSeconds(3)
    };

    private bool _isLoading;
    private bool _isFiltering;
    private bool _categoriesLoaded;

    public RealTimeCommandStream()
    {
        InitializeComponent();

        RecommendationsList.ItemsSource = _recommendations;
        AlertsList.ItemsSource = _alerts;
        DisconnectedList.ItemsSource = _disconnected;
        DevicesItemsControl.ItemsSource = _filteredDevices;
        HistoryItemsControl.ItemsSource = _filteredHistory;

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
                DateTime.TryParse(x.CreatedAt, out var createdAt) &&
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

                var nodeId = ReadString(sensor, "nodeId", "NodeId");
                var macAddress = ReadString(sensor, "macAddress", "MacAddress");
                var category = ReadString(sensor, "category", "Category");
                var location = ReadString(sensor, "location", "Location");

                var hasEnabledValue =
                    sensor.ContainsKey("isEnabled") ||
                    sensor.ContainsKey("IsEnabled");

                var isEnabled = ReadBool(sensor, "isEnabled", "IsEnabled");

                var displayName = !string.IsNullOrWhiteSpace(nodeId)
                    ? nodeId
                    : !string.IsNullOrWhiteSpace(macAddress)
                        ? macAddress
                        : "Unnamed device";

                var state = !hasEnabledValue
                    ? "Status unavailable"
                    : isEnabled ? "Enabled" : "Disabled";

                _devices.Add(new DeviceRow
                {
                    NodeId = nodeId,
                    DisplayName = displayName,
                    MacAddress = macAddress,
                    Category = string.IsNullOrWhiteSpace(category) ? "Uncategorised" : category,
                    Location = string.IsNullOrWhiteSpace(location) ? "No location" : location,
                    IsEnabled = hasEnabledValue && isEnabled,
                    StateText = state,
                    StateBackground = !hasEnabledValue
                        ? "#183044"
                        : isEnabled ? "#183D2B" : "#40252B",
                    StateForeground = !hasEnabledValue
                        ? "#DCE7ED"
                        : isEnabled ? "#8BF0B0" : "#FF9CA5",
                    LatestValue = "No telemetry available",
                    LatestTimestamp = "No reading recorded",
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
            DeviceMessageText.Text = "The sensor API returned invalid JSON.";
            DeviceMessageText.IsVisible = true;
        }
        catch (HttpRequestException ex)
        {
            DeviceMessageText.Text = $"Could not reach the sensor API: {ex.Message}";
            DeviceMessageText.IsVisible = true;
        }
        catch (TaskCanceledException)
        {
            DeviceMessageText.Text = "The sensor API request timed out.";
            DeviceMessageText.IsVisible = true;
        }
        catch (Exception ex)
        {
            DeviceMessageText.Text = $"Could not load sensors: {ex.Message}";
            DeviceMessageText.IsVisible = true;
        }
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
                string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
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

                var nodeId = ReadString(command, "nodeId", "NodeId");
                var category = ReadString(command, "category", "Category");

                if (string.IsNullOrWhiteSpace(category))
                {
                    category = _devices.FirstOrDefault(
                        x => string.Equals(x.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))
                        ?.Category ?? "Unknown";
                }

                var status = ReadString(command, "status", "Status");
                var result = ReadString(command, "result", "Result");
                var commandName = ReadString(command, "command", "Command");
                var createdAt = ReadString(command, "createdAt", "CreatedAt");

                _history.Add(new HistoryRow
                {
                    Command = string.IsNullOrWhiteSpace(commandName) ? "Unknown command" : commandName,
                    NodeId = nodeId,
                    Category = category,
                    Result = !string.IsNullOrWhiteSpace(result) ? result : status,
                    CreatedAt = FormatTimestamp(createdAt)
                });
            }

            var sortedHistory = _history
                .OrderByDescending(x =>
                    DateTime.TryParse(x.CreatedAt, out var timestamp)
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
        if (DateTimeOffset.TryParse(value, out var timestamp))
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
                return value.ToString();
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
                return result;
        }

        return false;
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
                    device.Location.Contains(search, StringComparison.OrdinalIgnoreCase);

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
                $"Manual override is not supported for category '{device.Category}'.";
            return;
        }

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

            var message = "Command accepted by the API.";

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
                DateTime.TryParse(x.CreatedAt, out var createdAt) &&
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
            await using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(true)))
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

public class HistoryRow
{
    public string Command { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string ResultForeground { get; set; } = "#DCE7ED";
    public string CreatedAt { get; set; } = string.Empty;
}