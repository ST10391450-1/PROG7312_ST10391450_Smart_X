
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Smart_X_UI.Models;
using Smart_X_UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Smart_X_UI.Pages;

public partial class RealTimeCommandStream : UserControl
{
    private readonly SensorService _sensorService;
    private readonly CommandStreamService _commandService;
    private readonly DispatcherTimer _refreshTimer;

    private readonly ObservableCollection<DeviceRow> _allDevices = new();
    private readonly ObservableCollection<DeviceRow> _visibleDevices = new();
    private readonly ObservableCollection<HistoryRow> _allHistory = new();
    private readonly ObservableCollection<HistoryRow> _visibleHistory = new();

    private readonly ObservableCollection<string> _deviceCategories = new()
    {
        "All categories"
    };

    private readonly ObservableCollection<string> _deviceStates = new()
    {
        "All states",
        "Active",
        "Disabled",
        "Alert"
    };

    private readonly ObservableCollection<string> _historyCategories = new()
    {
        "All categories"
    };

    private bool _isLoading;
    private bool _isSendingCommand;
    private bool _isAttached;
    private bool _filtersInitialized;
    private bool _deviceMessageIsError;

    private string? _deviceError;
    private string? _historyError;
    private string? _deviceSuccess;
    private string? _historySuccess;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

    public RealTimeCommandStream()
    {
        InitializeComponent();

        _sensorService = new SensorService(ApiClient.HttpClient);
        _commandService = new CommandStreamService(ApiClient.HttpClient);

        DevicesItemsControl.ItemsSource = _visibleDevices;
        HistoryItemsControl.ItemsSource = _visibleHistory;

        DeviceCategoryFilterComboBox.ItemsSource = _deviceCategories;
        DeviceStateFilterComboBox.ItemsSource = _deviceStates;
        HistoryCategoryFilterComboBox.ItemsSource = _historyCategories;

        _filtersInitialized = true;

        DeviceCategoryFilterComboBox.SelectedIndex = 0;
        DeviceStateFilterComboBox.SelectedIndex = 0;
        HistoryCategoryFilterComboBox.SelectedIndex = 0;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };

        _refreshTimer.Tick += RefreshTimer_Tick;

        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;

        ApplyDeviceFilters();
        ApplyHistoryFilters();
    }

    private void OnAttachedToVisualTree(
        object? sender,
        VisualTreeAttachmentEventArgs e)
    {
        if (_isAttached)
            return;

        _isAttached = true;
        _refreshTimer.Start();

        _ = LoadDataAsync();
    }

    private void OnDetachedFromVisualTree(
        object? sender,
        VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        _refreshTimer.Stop();
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        await LoadDataAsync();
    }

    private async void RefreshButton_Click(object? sender, RoutedEventArgs e)
    {
        await LoadDataAsync();
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
        _isAttached = false;

        if (TopLevel.GetTopLevel(this) is Smart_X_UI.MainWindow mainWindow)
            mainWindow.ShowHome();
    }

    private void DeviceSearchTextBox_TextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        ApplyDeviceFilters();
    }

    private void CommandHistorySearchTextBox_TextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        ApplyHistoryFilters();
    }

    private void DeviceFilter_Changed(
        object? sender,
        SelectionChangedEventArgs e)
    {
        if (_filtersInitialized)
            ApplyDeviceFilters();
    }

    private void HistoryFilter_Changed(
        object? sender,
        SelectionChangedEventArgs e)
    {
        if (_filtersInitialized)
            ApplyHistoryFilters();
    }

    private async Task LoadDataAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;

        try
        {
            ConnectionStatusText.Text = "Loading sensors...";
            ConnectionStatusText.Foreground = BrushFrom("#D6A84F");

            var sensors = await _sensorService.GetSensorsAsync();

            _allDevices.Clear();

            foreach (var sensor in sensors)
            {
                if (string.IsNullOrWhiteSpace(sensor.NodeId))
                    continue;

                var state = sensor.IsActive ? "Active" : "Disabled";

                _allDevices.Add(
                    BuildDeviceRow(sensor, null, state, null));
            }

            RefreshFilterOptions();
            ApplyDeviceFilters();
            UpdateSummaryCounters();

            List<SensorReadingRecord> readings = new();
            List<CommandRecord> commands = new();

            try
            {
                readings = await ApiClient.HttpClient
                    .GetFromJsonAsync<List<SensorReadingRecord>>(
                        "api/Telemetry",
                        JsonOptions) ?? new List<SensorReadingRecord>();

                if (_deviceError?.StartsWith(
                        "Sensors loaded, but telemetry failed:",
                        StringComparison.OrdinalIgnoreCase) == true)
                {
                    ClearDeviceError();
                }
            }
            catch (Exception ex)
            {
                ShowDeviceError(
                    $"Sensors loaded, but telemetry failed: {ex.Message}");
            }

            try
            {
                commands = await ApiClient.HttpClient
                    .GetFromJsonAsync<List<CommandRecord>>(
                        "api/Commands",
                        JsonOptions) ?? new List<CommandRecord>();

                if (_historyError?.StartsWith(
                        "Command history failed:",
                        StringComparison.OrdinalIgnoreCase) == true)
                {
                    ClearHistoryError();
                }
            }
            catch (Exception ex)
            {
                ShowHistoryError(
                    $"Command history failed: {ex.Message}");
            }

            var states = new Dictionary<string, SimulatedDeviceState>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var sensor in sensors)
            {
                if (string.IsNullOrWhiteSpace(sensor.NodeId))
                    continue;

                try
                {
                    var simulatedState =
                        await GetSimulatedDeviceStateAsync(sensor.NodeId);

                    if (simulatedState != null)
                    {
                        states[sensor.NodeId] = simulatedState;
                        sensor.IsActive = simulatedState.IsEnabled;
                    }
                }
                catch
                {
                }
            }

            _allDevices.Clear();

            foreach (var sensor in sensors)
            {
                if (string.IsNullOrWhiteSpace(sensor.NodeId))
                    continue;

                var reading = FindLatestReading(sensor, readings);

                states.TryGetValue(sensor.NodeId, out var simulatedState);

                var state = GetDeviceState(sensor, reading);

                _allDevices.Add(
                    BuildDeviceRow(sensor, reading, state, simulatedState));
            }

            var sensorLookup = new Dictionary<string, SensorRegistration>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var sensor in sensors)
            {
                if (!string.IsNullOrWhiteSpace(sensor.NodeId))
                    sensorLookup.TryAdd(sensor.NodeId.Trim(), sensor);

                if (!string.IsNullOrWhiteSpace(sensor.MacAddress))
                    sensorLookup.TryAdd(sensor.MacAddress.Trim(), sensor);
            }

            _allHistory.Clear();

            foreach (var command in commands.OrderByDescending(x => x.CreatedAt))
            {
                var nodeId = command.NodeId ?? string.Empty;

                sensorLookup.TryGetValue(nodeId.Trim(), out var sensor);

                var category = sensor == null
                    ? "Unknown"
                    : GetCategory(sensor);

                var result = string.IsNullOrWhiteSpace(command.Status)
                    ? "Unknown"
                    : command.Status;

                _allHistory.Add(new HistoryRow
                {
                    Command = command.Command ?? "Unknown",
                    NodeId = nodeId,
                    Device = sensor == null
                        ? nodeId
                        : GetDisplayName(sensor),
                    Category = category,
                    Source = string.IsNullOrWhiteSpace(command.Result)
                        ? "API command"
                        : command.Result,
                    Result = result,
                    Time = command.CreatedAt == default
                        ? "Timestamp unavailable"
                        : FormatTimestamp(command.CreatedAt),
                    ResultForeground = GetResultForeground(result)
                });
            }

            RefreshFilterOptions();
            ApplyDeviceFilters();
            ApplyHistoryFilters();
            UpdateSummaryCounters();

            ConnectionStatusText.Text =
                $"Connected · {_allDevices.Count} devices · Updated {DateTime.Now:HH:mm:ss}";
            ConnectionStatusText.Foreground = BrushFrom("#6EE7B7");
        }
        catch (Exception ex)
        {
            ConnectionStatusText.Text = "API request failed";
            ConnectionStatusText.Foreground = BrushFrom("#FDA4AF");

            ShowDeviceError(ex.Message);
        }
        finally
        {
            RestoreMessages();
            _isLoading = false;
        }
    }

    private async Task<SimulatedDeviceState?> GetSimulatedDeviceStateAsync(
        string nodeId)
    {
        using var response = await ApiClient.HttpClient.GetAsync(
            $"api/Commands/device/{Uri.EscapeDataString(nodeId)}/state");

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content
            .ReadFromJsonAsync<SimulatedDeviceState>(JsonOptions);
    }

    private static SensorReadingRecord? FindLatestReading(
        SensorRegistration sensor,
        IEnumerable<SensorReadingRecord> readings)
    {
        var identifiers = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        AddIdentifier(identifiers, sensor.NodeId);
        AddIdentifier(identifiers, sensor.MacAddress);

        if (identifiers.Count == 0)
            return null;

        return readings
            .Where(reading =>
                reading != null &&
                IsMatchingDevice(reading.DeviceId, identifiers))
            .OrderByDescending(reading => GetTimestamp(reading.Timestamp))
            .FirstOrDefault();
    }

    private static void AddIdentifier(
        HashSet<string> identifiers,
        string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return;

        var trimmed = identifier.Trim();

        identifiers.Add(trimmed);

        var normalized = NormalizeDeviceIdentifier(trimmed);

        if (!string.IsNullOrWhiteSpace(normalized))
            identifiers.Add(normalized);
    }

    private static bool IsMatchingDevice(
        string? deviceId,
        HashSet<string> identifiers)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return false;

        var trimmed = deviceId.Trim();

        if (identifiers.Contains(trimmed))
            return true;

        var normalized = NormalizeDeviceIdentifier(trimmed);

        return !string.IsNullOrWhiteSpace(normalized)
            && identifiers.Contains(normalized);
    }

    private static string NormalizeDeviceIdentifier(string identifier)
    {
        return new string(
            identifier
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());
    }

    private static DateTime GetTimestamp(DateTime timestamp)
    {
        if (timestamp == default)
            return DateTime.MinValue;

        return timestamp.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
            : timestamp.ToUniversalTime();
    }

    private static DeviceRow BuildDeviceRow(
        SensorRegistration sensor,
        SensorReadingRecord? reading,
        string state,
        SimulatedDeviceState? simulatedState)
    {
        var unit = string.IsNullOrWhiteSpace(sensor.Unit)
            ? GetUnit(sensor.Category)
            : sensor.Unit;

        var latestValue = reading == null
            ? "No telemetry received"
            : $"{reading.Value.ToString("G", CultureInfo.InvariantCulture)} {unit}".Trim();

        if (simulatedState != null &&
            (sensor.Category.Contains("pump", StringComparison.OrdinalIgnoreCase)
             || sensor.Category.Contains("relay", StringComparison.OrdinalIgnoreCase)
             || sensor.Category.Contains("switch", StringComparison.OrdinalIgnoreCase)))
        {
            latestValue = simulatedState.OutputOn ? "ON" : "OFF";
        }

        return new DeviceRow
        {
            DisplayName = GetDisplayName(sensor),
            NodeId = sensor.NodeId,
            Category = GetCategory(sensor),
            Location = GetLocation(sensor),
            LatestValue = latestValue,
            LatestTimestamp = reading == null
                ? simulatedState == null
                    ? "Waiting for first reading"
                    : "Simulated device state"
                : reading.Timestamp == default
                    ? "Timestamp unavailable"
                    : FormatTimestamp(reading.Timestamp),
            StateText = state,
            StateBackground = GetStateBackground(state),
            StateForeground = GetStateForeground(state),
            EnableDisableLabel = sensor.IsActive
                ? "Disable Sensor"
                : "Enable Sensor",
            IsActive = sensor.IsActive,
            HasAlert = state == "Alert",
            OutputOn = simulatedState?.OutputOn ?? false
        };
    }

    private static string GetDeviceState(
        SensorRegistration sensor,
        SensorReadingRecord? reading)
    {
        if (reading != null)
        {
            var category = reading.SensorCategory ?? string.Empty;

            if (category.Contains("alert", StringComparison.OrdinalIgnoreCase)
                || category.Contains("fault", StringComparison.OrdinalIgnoreCase)
                || category.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                return "Alert";
            }
        }

        return sensor.IsActive ? "Active" : "Disabled";
    }

    private static string GetUnit(string category)
    {
        if (category.Contains("temperature", StringComparison.OrdinalIgnoreCase))
            return "°C";

        if (category.Contains("humidity", StringComparison.OrdinalIgnoreCase))
            return "%RH";

        if (category.Contains("power", StringComparison.OrdinalIgnoreCase))
            return "W";

        if (category.Contains("pressure", StringComparison.OrdinalIgnoreCase))
            return "hPa";

        if (category.Contains("light", StringComparison.OrdinalIgnoreCase))
            return "lx";

        if (category.Contains("voltage", StringComparison.OrdinalIgnoreCase))
            return "V";

        if (category.Contains("current", StringComparison.OrdinalIgnoreCase))
            return "A";

        if (category.Contains("relay", StringComparison.OrdinalIgnoreCase)
            || category.Contains("switch", StringComparison.OrdinalIgnoreCase))
            return "state";

        return string.Empty;
    }

    private static string FormatTimestamp(DateTime timestamp)
    {
        return timestamp.Kind == DateTimeKind.Unspecified
            ? timestamp.ToString("yyyy-MM-dd HH:mm:ss")
            : timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static string GetDisplayName(SensorRegistration sensor) =>
        string.IsNullOrWhiteSpace(sensor.Location)
            ? sensor.NodeId
            : sensor.Location;

    private static string GetCategory(SensorRegistration sensor) =>
        string.IsNullOrWhiteSpace(sensor.Category)
            ? "Uncategorised"
            : sensor.Category;

    private static string GetLocation(SensorRegistration sensor) =>
        string.IsNullOrWhiteSpace(sensor.Location)
            ? "Location not specified"
            : sensor.Location;

    private static SolidColorBrush BrushFrom(string hex) =>
        new(Color.Parse(hex));

    private static IBrush GetStateBackground(string state) =>
        BrushFrom(state switch
        {
            "Active" => "#163D32",
            "Alert" => "#4A2028",
            _ => "#303B46"
        });

    private static IBrush GetStateForeground(string state) =>
        BrushFrom(state switch
        {
            "Active" => "#6EE7B7",
            "Alert" => "#FDA4AF",
            _ => "#CBD5E1"
        });

    private static IBrush GetResultForeground(string result)
    {
        if (result.Contains("success", StringComparison.OrdinalIgnoreCase)
            || result.Contains("completed", StringComparison.OrdinalIgnoreCase))
            return BrushFrom("#6EE7B7");

        if (result.Contains("fail", StringComparison.OrdinalIgnoreCase)
            || result.Contains("error", StringComparison.OrdinalIgnoreCase)
            || result.Contains("rejected", StringComparison.OrdinalIgnoreCase))
            return BrushFrom("#FDA4AF");

        return BrushFrom("#CBD5E1");
    }

    private void RefreshFilterOptions()
    {
        var selectedDeviceCategory =
            DeviceCategoryFilterComboBox.SelectedItem as string;

        var selectedDeviceState =
            DeviceStateFilterComboBox.SelectedItem as string;

        var selectedHistoryCategory =
            HistoryCategoryFilterComboBox.SelectedItem as string;

        ReplaceOptions(
            _deviceCategories,
            new[] { "All categories" }.Concat(
                _allDevices.Select(x => x.Category)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)));

        ReplaceOptions(
            _historyCategories,
            new[] { "All categories" }.Concat(
                _allHistory.Select(x => x.Category)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)));

        DeviceCategoryFilterComboBox.SelectedItem =
            selectedDeviceCategory != null &&
            _deviceCategories.Contains(selectedDeviceCategory)
                ? selectedDeviceCategory
                : "All categories";

        DeviceStateFilterComboBox.SelectedItem =
            selectedDeviceState != null &&
            _deviceStates.Contains(selectedDeviceState)
                ? selectedDeviceState
                : "All states";

        HistoryCategoryFilterComboBox.SelectedItem =
            selectedHistoryCategory != null &&
            _historyCategories.Contains(selectedHistoryCategory)
                ? selectedHistoryCategory
                : "All categories";
    }

    private static void ReplaceOptions(
        ObservableCollection<string> target,
        IEnumerable<string> values)
    {
        target.Clear();

        foreach (var value in values)
            target.Add(value);
    }

    private void UpdateSummaryCounters()
    {
        RegisteredDevicesValue.Text = _allDevices.Count.ToString();

        OnlineDevicesValue.Text = _allDevices
            .Count(device => device.StateText == "Active")
            .ToString();

        ActiveAlertsValue.Text = _allDevices
            .Count(device => device.StateText == "Alert")
            .ToString();

        CommandsTodayValue.Text = _allHistory
            .Count(entry =>
                DateTime.TryParse(
                    entry.Time,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var time) &&
                time.Date == DateTime.Now.Date)
            .ToString();
    }

    private void ApplyDeviceFilters()
    {
        var search = DeviceSearchTextBox.Text?.Trim() ?? string.Empty;

        var category = DeviceCategoryFilterComboBox.SelectedItem as string
            ?? "All categories";

        var state = DeviceStateFilterComboBox.SelectedItem as string
            ?? "All states";

        var filtered = _allDevices.Where(device =>
            (string.IsNullOrWhiteSpace(search)
                || device.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || device.NodeId.Contains(search, StringComparison.OrdinalIgnoreCase)
                || device.Location.Contains(search, StringComparison.OrdinalIgnoreCase)
                || device.Category.Contains(search, StringComparison.OrdinalIgnoreCase))
            && (category == "All categories"
                || string.Equals(device.Category, category, StringComparison.OrdinalIgnoreCase))
            && (state == "All states"
                || string.Equals(device.StateText, state, StringComparison.OrdinalIgnoreCase)));

        _visibleDevices.Clear();

        foreach (var device in filtered)
            _visibleDevices.Add(device);

        DeviceCountText.Text = $"{_visibleDevices.Count} devices";
        DeviceEmptyText.Text = _allDevices.Count == 0
            ? "No registered devices found."
            : "No devices match the selected filters.";

        DeviceEmptyText.IsVisible = _visibleDevices.Count == 0;
        DevicesItemsControl.IsVisible = _visibleDevices.Count > 0;
    }

    private void ApplyHistoryFilters()
    {
        var search = CommandHistorySearchTextBox.Text?.Trim() ?? string.Empty;

        var category = HistoryCategoryFilterComboBox.SelectedItem as string
            ?? "All categories";

        var filtered = _allHistory.Where(entry =>
            (string.IsNullOrWhiteSpace(search)
                || entry.Command.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.Device.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.NodeId.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.Source.Contains(search, StringComparison.OrdinalIgnoreCase))
            && (category == "All categories"
                || string.Equals(entry.Category, category, StringComparison.OrdinalIgnoreCase)));

        _visibleHistory.Clear();

        foreach (var entry in filtered)
            _visibleHistory.Add(entry);

        HistoryCountText.Text = $"{_visibleHistory.Count} commands";
        HistoryEmptyText.Text = _allHistory.Count == 0
            ? "No command history available."
            : "No commands match the selected filters.";

        HistoryEmptyText.IsVisible = _visibleHistory.Count == 0;
        HistoryItemsControl.IsVisible = _visibleHistory.Count > 0;
    }

    private async void DeviceOverride_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            string.IsNullOrWhiteSpace(button.Tag?.ToString()))
            return;

        var nodeId = button.Tag.ToString()!;

        var device = _allDevices.FirstOrDefault(item =>
            string.Equals(
                item.NodeId,
                nodeId,
                StringComparison.OrdinalIgnoreCase));

        if (device == null)
        {
            ShowDeviceMessage("Device was not found.", true);
            return;
        }

        try
        {
            var command = await DetermineOverrideCommandAsync(device);

            await SendCommandAsync(
                nodeId,
                command,
                $"{command} executed for {device.DisplayName}.");
        }
        catch (Exception ex)
        {
            ShowDeviceMessage(
                $"Could not determine device state: {ex.Message}",
                true);
        }
    }

    private async Task<string> DetermineOverrideCommandAsync(DeviceRow device)
    {
        if (device.Category.Contains("pump", StringComparison.OrdinalIgnoreCase))
        {
            var state = await GetSimulatedDeviceStateAsync(device.NodeId);
            return state?.OutputOn == true ? "PumpStop" : "PumpStart";
        }

        if (device.Category.Contains("relay", StringComparison.OrdinalIgnoreCase)
            || device.Category.Contains("switch", StringComparison.OrdinalIgnoreCase))
        {
            var state = await GetSimulatedDeviceStateAsync(device.NodeId);
            return state?.OutputOn == true ? "RelayOff" : "RelayOn";
        }

        return "ManualOverride";
    }

    private async void DeviceEnableDisable_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            string.IsNullOrWhiteSpace(button.Tag?.ToString()))
            return;

        var nodeId = button.Tag.ToString()!;

        var device = _allDevices.FirstOrDefault(item =>
            string.Equals(
                item.NodeId,
                nodeId,
                StringComparison.OrdinalIgnoreCase));

        if (device == null)
        {
            ShowDeviceMessage("Device was not found.", true);
            return;
        }

        var command = device.IsActive ? "DisableSensor" : "EnableSensor";

        await SendCommandAsync(
            nodeId,
            command,
            $"{command} executed for {device.DisplayName}.");
    }

    private async Task SendCommandAsync(
        string nodeId,
        string command,
        string successMessage)
    {
        if (_isSendingCommand)
        {
            ShowDeviceMessage("Wait for the current command to finish.", true);
            return;
        }

        _isSendingCommand = true;

        try
        {
            ShowDeviceMessage($"Sending {command}...", false);

            using var response = await _commandService.SendOverrideAsync(
                nodeId,
                command);

            if (!response.IsSuccessStatusCode)
            {
                var details = await response.Content.ReadAsStringAsync();

                ShowDeviceMessage(
                    $"{command} failed (HTTP {(int)response.StatusCode}). {details}",
                    true);

                return;
            }

            ShowDeviceMessage(successMessage, false);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            ShowDeviceMessage(
                $"Could not send {command}: {ex.Message}",
                true);
        }
        finally
        {
            _isSendingCommand = false;
            RestoreMessages();
        }
    }

    private void ShowDeviceMessage(string message, bool isError)
    {
        if (isError)
        {
            _deviceError = message;
            _deviceSuccess = null;
            _deviceMessageIsError = true;
        }
        else
        {
            _deviceError = null;
            _deviceSuccess = message;
            _deviceMessageIsError = false;
        }

        RestoreMessages();
    }

    private void ShowDeviceError(string message)
    {
        _deviceError = message;
        _deviceSuccess = null;
        _deviceMessageIsError = true;

        RestoreMessages();
    }

    private void ShowHistoryError(string message)
    {
        _historyError = message;
        _historySuccess = null;

        RestoreMessages();
    }

    private void RestoreMessages()
    {
        var deviceMessage = _deviceError ?? _deviceSuccess;

        DeviceMessageText.Text = deviceMessage ?? string.Empty;
        DeviceMessageText.IsVisible =
            !string.IsNullOrWhiteSpace(deviceMessage);

        DeviceMessageText.Foreground =
            BrushFrom(_deviceMessageIsError ? "#FDA4AF" : "#6EE7B7");

        var historyMessage = _historyError ?? _historySuccess;

        HistoryMessageText.Text = historyMessage ?? string.Empty;
        HistoryMessageText.IsVisible =
            !string.IsNullOrWhiteSpace(historyMessage);

        HistoryMessageText.Foreground =
            BrushFrom(_historyError != null ? "#FDA4AF" : "#6EE7B7");
    }

    private void ClearDeviceError()
    {
        _deviceError = null;
        _deviceSuccess = null;
        _deviceMessageIsError = false;

        RestoreMessages();
    }

    private void ClearHistoryError()
    {
        _historyError = null;
        _historySuccess = null;

        RestoreMessages();
    }

    private async void ExportHistoryButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);

            if (topLevel?.StorageProvider == null)
            {
                ShowHistoryError("File export is unavailable.");
                return;
            }

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Export command history",
                    SuggestedFileName =
                        $"command-history-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("CSV file")
                        {
                            Patterns = new[] { "*.csv" }
                        }
                    }
                });

            if (file == null)
                return;

            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream, Encoding.UTF8);

            await writer.WriteLineAsync(
                "Command,Device,NodeId,Category,Source,Result,Time");

            foreach (var entry in _visibleHistory)
            {
                await writer.WriteLineAsync(string.Join(",",
                    Csv(entry.Command),
                    Csv(entry.Device),
                    Csv(entry.NodeId),
                    Csv(entry.Category),
                    Csv(entry.Source),
                    Csv(entry.Result),
                    Csv(entry.Time)));
            }

            _historyError = null;
            _historySuccess = "Command history exported.";

            RestoreMessages();
        }
        catch (Exception ex)
        {
            ShowHistoryError(ex.Message);
        }
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}

public sealed class DeviceRow
{
    public string DisplayName { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string LatestValue { get; set; } = string.Empty;
    public string LatestTimestamp { get; set; } = string.Empty;
    public string StateText { get; set; } = "Disabled";
    public string EnableDisableLabel { get; set; } = "Enable Sensor";

    public IBrush StateBackground { get; set; } =
        new SolidColorBrush(Color.Parse("#303B46"));

    public IBrush StateForeground { get; set; } =
        new SolidColorBrush(Color.Parse("#CBD5E1"));

    public bool IsActive { get; set; }
    public bool HasAlert { get; set; }
    public bool OutputOn { get; set; }
}

public sealed class HistoryRow
{
    public string Command { get; set; } = string.Empty;
    public string Device { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string CreatedAt => Time;

    public IBrush ResultForeground { get; set; } =
        new SolidColorBrush(Color.Parse("#CBD5E1"));
}

public sealed class CommandRecord
{
    public Guid Id { get; set; }
    public string? NodeId { get; set; }
    public string? Command { get; set; }
    public string? Status { get; set; }
    public string? Result { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class SimulatedDeviceState
{
    public string NodeId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool OutputOn { get; set; }
    public string OutputState { get; set; } = "OFF";
}
