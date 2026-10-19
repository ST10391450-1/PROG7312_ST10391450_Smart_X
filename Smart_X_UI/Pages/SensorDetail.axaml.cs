using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Smart_X_UI.Models;
using Smart_X_UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Smart_X_UI.Pages;

public partial class SensorDetails : UserControl
{
    private readonly SensorRegistration _sensor;
    private readonly AttachmentService _attachmentService =
        new(ApiClient.HttpClient);

    private readonly ObservableCollection<SensorAttachment> _attachments = new();
    private readonly DispatcherTimer _telemetryTimer;

    private bool _loadingTelemetry;
    private bool _loadingUnit;
    private bool _unitLoaded;
    private string _unitCategory = string.Empty;
    private string _unit = string.Empty;

    public event EventHandler? BackRequested;

    private sealed class UnitDetectionRequest
    {
        public string SensorName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

    private sealed class UnitDetectionResponse
    {
        public string? Unit { get; set; }
        public string? Type { get; set; }
        public string? DeterminedBy { get; set; }
        public bool RequiresConfiguration { get; set; }
    }

    public SensorDetails()
        : this(new SensorRegistration())
    {
    }

    public SensorDetails(SensorRegistration sensor)
    {
        InitializeComponent();

        _sensor = sensor;
        AttachmentsItemsControl.ItemsSource = _attachments;

        _telemetryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };

        _telemetryTimer.Tick += TelemetryTimer_Tick;

        LoadSensorDetails();

        _ = LoadAttachmentsAsync();
        _ = LoadCurrentTelemetryAsync();

        _telemetryTimer.Start();
    }

    private void LoadSensorDetails()
    {
        HeaderNodeIdText.Text = _sensor.NodeId;
        NodeIdText.Text = _sensor.NodeId;
        MacAddressText.Text = _sensor.MacAddress;
        CategoryText.Text = _sensor.Category;
        LocationText.Text = _sensor.Location;

        UpdateConnectionStatus(
            "WAITING",
            Brushes.Orange,
            CreateBrush(255, 58, 48, 32),
            CreateBrush(255, 102, 85, 43));

        EndpointText.Text = TelemetryService.GetEndpoint(_sensor);

        TelemetryStatusText.Text = "WAITING";
        TelemetryStatusText.Foreground = Brushes.Orange;
        TelemetryValueText.Text = "N/A";
        TelemetryTimestampText.Text = "N/A";

        UnitText.Text = "Loading...";
        UnitSourceText.Text = "Smart X API";
        TelemetryUnitText.Text = string.Empty;
    }

    private async void TelemetryTimer_Tick(object? sender, EventArgs e)
    {
        await LoadCurrentTelemetryAsync();
    }

    private async Task LoadCurrentTelemetryAsync()
    {
        if (_loadingTelemetry)
            return;

        if (string.IsNullOrWhiteSpace(_sensor.MacAddress))
        {
            SetNoTelemetryState("No MAC address configured.");
            return;
        }

        _loadingTelemetry = true;

        try
        {
            var deviceId = Uri.EscapeDataString(_sensor.MacAddress);

            using var response = await ApiClient.HttpClient.GetAsync(
                $"api/Telemetry?deviceId={deviceId}");

            response.EnsureSuccessStatusCode();

            var readings = await response.Content
                .ReadFromJsonAsync<List<SensorReadingRecord>>();

            if (readings == null || readings.Count == 0)
            {
                SetNoTelemetryState("Waiting for telemetry...");
                return;
            }

            var latestReading = readings
                .OrderByDescending(x => DateTimeService.EnsureUtc(x.Timestamp))
                .FirstOrDefault();

            if (latestReading == null)
            {
                SetNoTelemetryState("Waiting for telemetry...");
                return;
            }

            await EnsureUnitAsync(
                string.IsNullOrWhiteSpace(latestReading.SensorCategory)
                    ? _sensor.Category
                    : latestReading.SensorCategory);

            DisplayTelemetry(latestReading);
        }
        catch (HttpRequestException)
        {
            SetConnectionErrorState("DISCONNECTED");
        }
        catch (TaskCanceledException)
        {
            SetConnectionErrorState("TIMEOUT");
        }
        catch (Exception ex)
        {
            SetConnectionErrorState("ERROR");
            ShowActionStatus(
                $"Could not retrieve telemetry: {ex.Message}",
                false);
        }
        finally
        {
            _loadingTelemetry = false;
        }
    }

    private async Task EnsureUnitAsync(string? category)
    {
        var requestedCategory = category?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(requestedCategory))
        {
            _unit = string.Empty;
            _unitCategory = string.Empty;
            _unitLoaded = true;

            UnitText.Text = "Not configured";
            UnitSourceText.Text = "Category unavailable";
            TelemetryUnitText.Text = string.Empty;
            return;
        }

        if (_unitLoaded &&
            string.Equals(
                _unitCategory,
                requestedCategory,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_loadingUnit)
            return;

        _loadingUnit = true;

        try
        {
            using var response = await ApiClient.HttpClient.PostAsJsonAsync(
                "api/UnitDetection/detect",
                new UnitDetectionRequest
                {
                    SensorName = string.Empty,
                    Category = requestedCategory
                });

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<UnitDetectionResponse>();

            if (result == null)
            {
                _unit = string.Empty;
                UnitText.Text = "Not configured";
                UnitSourceText.Text = "No API result";
                TelemetryUnitText.Text = string.Empty;
            }
            else
            {
                _unit = result.Unit?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(_unit))
                {
                    UnitText.Text = result.RequiresConfiguration
                        ? "Configuration required"
                        : "Not configured";

                    TelemetryUnitText.Text = string.Empty;
                }
                else
                {
                    UnitText.Text = _unit;
                    TelemetryUnitText.Text = _unit;
                }

                UnitSourceText.Text =
                    string.IsNullOrWhiteSpace(result.DeterminedBy)
                        ? "Smart X API"
                        : result.DeterminedBy;
            }

            _unitCategory = requestedCategory;
            _unitLoaded = true;
        }
        catch (HttpRequestException)
        {
            _unit = string.Empty;
            _unitCategory = requestedCategory;
            _unitLoaded = false;

            UnitText.Text = "Unavailable";
            UnitSourceText.Text = "API connection failed";
            TelemetryUnitText.Text = string.Empty;
        }
        catch (TaskCanceledException)
        {
            _unit = string.Empty;
            _unitCategory = requestedCategory;
            _unitLoaded = false;

            UnitText.Text = "Unavailable";
            UnitSourceText.Text = "API request timed out";
            TelemetryUnitText.Text = string.Empty;
        }
        catch (JsonException)
        {
            _unit = string.Empty;
            _unitCategory = requestedCategory;
            _unitLoaded = false;

            UnitText.Text = "Unavailable";
            UnitSourceText.Text = "Invalid API response";
            TelemetryUnitText.Text = string.Empty;
        }
        finally
        {
            _loadingUnit = false;
        }
    }

    private void DisplayTelemetry(SensorReadingRecord reading)
    {
        var isCurrent = DateTimeService.IsRecent(reading.Timestamp);

        if (isCurrent)
        {
            UpdateConnectionStatus(
                "ACTIVE",
                Brushes.LimeGreen,
                CreateBrush(255, 35, 75, 45),
                CreateBrush(255, 50, 105, 65));

            TelemetryStatusText.Text = "RECEIVED";
            TelemetryStatusText.Foreground = Brushes.LimeGreen;
        }
        else
        {
            UpdateConnectionStatus(
                "INACTIVE",
                Brushes.Orange,
                CreateBrush(255, 58, 48, 32),
                CreateBrush(255, 102, 85, 43));

            TelemetryStatusText.Text = "STALE";
            TelemetryStatusText.Foreground = Brushes.Orange;
        }

        TelemetryValueText.Text =
            FormatTelemetryValue(reading.Value, reading.SensorCategory);

        TelemetryUnitText.Text =
            IsActuatorCategory(reading.SensorCategory)
                ? string.Empty
                : _unit;

        TelemetryTimestampText.Text = DateTimeService
            .EnsureUtc(reading.Timestamp)
            .ToLocalTime()
            .ToString("yyyy-MM-dd HH:mm:ss");
    }

    private void SetNoTelemetryState(string message)
    {
        UpdateConnectionStatus(
            "INACTIVE",
            Brushes.Orange,
            CreateBrush(255, 58, 48, 32),
            CreateBrush(255, 102, 85, 43));

        TelemetryStatusText.Text = "NO DATA";
        TelemetryStatusText.Foreground = Brushes.Orange;
        TelemetryValueText.Text = "N/A";
        TelemetryUnitText.Text = string.Empty;
        TelemetryTimestampText.Text = "N/A";

        ShowActionStatus(message, false);
    }

    private void SetConnectionErrorState(string status)
    {
        UpdateConnectionStatus(
            status,
            Brushes.OrangeRed,
            CreateBrush(255, 70, 32, 32),
            CreateBrush(255, 120, 55, 55));

        TelemetryStatusText.Text = status;
        TelemetryStatusText.Foreground = Brushes.OrangeRed;
    }

    private void UpdateConnectionStatus(
        string status,
        IBrush foreground,
        IBrush background,
        IBrush border)
    {
        StatusText.Text = status;
        StatusText.Foreground = foreground;

        ConnectionStatusBadgeText.Text = status;
        ConnectionStatusBadgeText.Foreground = foreground;
        ConnectionStatusIndicator.Fill = foreground;

        ConnectionStatusBadge.Background = background;
        ConnectionStatusBadge.BorderBrush = border;
    }

    private static IBrush CreateBrush(
        byte alpha,
        byte red,
        byte green,
        byte blue)
    {
        return new SolidColorBrush(
            Color.FromArgb(alpha, red, green, blue));
    }

    private static string FormatTelemetryValue(
        double value,
        string category)
    {
        if (IsActuatorCategory(category))
            return value >= 0.5 ? "ON" : "OFF";

        return value % 1 == 0
            ? value.ToString("F0")
            : value.ToString("F2");
    }

    private static bool IsActuatorCategory(string? category)
    {
        return string.Equals(
            category?.Trim(),
            "Actuator",
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadAttachmentsAsync()
    {
        try
        {
            AttachmentsStatusText.IsVisible = false;

            var attachments = await _attachmentService
                .GetAttachmentsAsync(_sensor.NodeId);

            _attachments.Clear();

            foreach (var attachment in attachments)
                _attachments.Add(attachment);

            if (_attachments.Count == 0)
            {
                AttachmentsStatusText.Text = "No attachments found.";
                AttachmentsStatusText.IsVisible = true;
            }
        }
        catch (HttpRequestException)
        {
            AttachmentsStatusText.Text =
                "Could not connect to the Smart X API.";

            AttachmentsStatusText.IsVisible = true;
        }
        catch (TaskCanceledException)
        {
            AttachmentsStatusText.Text =
                "The attachment request timed out.";

            AttachmentsStatusText.IsVisible = true;
        }
        catch (Exception ex)
        {
            AttachmentsStatusText.Text = ex.Message;
            AttachmentsStatusText.IsVisible = true;
        }
    }

    private async void RefreshAttachmentsButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        await LoadAttachmentsAsync();
    }

    private async void UploadAttachmentButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not TopLevel topLevel)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Select attachment",
                AllowMultiple = false
            });

        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();

        if (string.IsNullOrWhiteSpace(path))
        {
            ShowActionStatus(
                "The selected file could not be accessed.",
                false);

            return;
        }

        try
        {
            await _attachmentService.UploadAttachmentAsync(
                _sensor.NodeId,
                path);

            ShowActionStatus(
                "Attachment uploaded successfully.",
                true);

            await LoadAttachmentsAsync();
        }
        catch (HttpRequestException)
        {
            ShowActionStatus(
                "Could not connect to the Smart X API.",
                false);
        }
        catch (TaskCanceledException)
        {
            ShowActionStatus(
                "The upload request timed out.",
                false);
        }
        catch (Exception ex)
        {
            ShowActionStatus(ex.Message, false);
        }
    }

    private async void DownloadAttachmentButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not SensorAttachment attachment)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is not TopLevel topLevel)
            return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Save attachment",
                SuggestedFileName = attachment.FileName
            });

        if (file is null)
            return;

        var path = file.TryGetLocalPath();

        if (string.IsNullOrWhiteSpace(path))
        {
            ShowActionStatus(
                "The destination file could not be accessed.",
                false);

            return;
        }

        try
        {
            await using var source =
                await _attachmentService.DownloadAttachmentAsync(
                    _sensor.NodeId,
                    attachment.Id);

            await using var destination = File.Create(path);

            await source.CopyToAsync(destination);

            ShowActionStatus(
                "Attachment downloaded successfully.",
                true);
        }
        catch (HttpRequestException)
        {
            ShowActionStatus(
                "Could not connect to the Smart X API.",
                false);
        }
        catch (TaskCanceledException)
        {
            ShowActionStatus(
                "The download request timed out.",
                false);
        }
        catch (Exception ex)
        {
            ShowActionStatus(ex.Message, false);
        }
    }

    private async void DeleteAttachmentButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not SensorAttachment attachment)
        {
            return;
        }

        try
        {
            await _attachmentService.DeleteAttachmentAsync(
                _sensor.NodeId,
                attachment.Id);

            _attachments.Remove(attachment);

            ShowActionStatus(
                "Attachment deleted successfully.",
                true);

            if (_attachments.Count == 0)
            {
                AttachmentsStatusText.Text = "No attachments found.";
                AttachmentsStatusText.IsVisible = true;
            }
        }
        catch (HttpRequestException)
        {
            ShowActionStatus(
                "Could not connect to the Smart X API.",
                false);
        }
        catch (TaskCanceledException)
        {
            ShowActionStatus(
                "The delete request timed out.",
                false);
        }
        catch (Exception ex)
        {
            ShowActionStatus(ex.Message, false);
        }
    }

    private void ShowActionStatus(string message, bool success)
    {
        ActionStatusText.Text = message;
        ActionStatusText.Foreground =
            success ? Brushes.LimeGreen : Brushes.OrangeRed;

        ActionStatusText.IsVisible = true;
    }

    private void BackButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        _telemetryTimer.Stop();
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnDetachedFromVisualTree(
        VisualTreeAttachmentEventArgs e)
    {
        _telemetryTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }
}