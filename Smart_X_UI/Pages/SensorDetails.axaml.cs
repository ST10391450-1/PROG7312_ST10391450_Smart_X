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

    public event EventHandler? BackRequested;

    // Required by the Avalonia XAML loader.
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

    // Loads the sensor information shown on the page.
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
    }

    private async void TelemetryTimer_Tick(object? sender, EventArgs e)
    {
        await LoadCurrentTelemetryAsync();
    }

    // Gets the latest telemetry reading for the sensor.
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

    // Updates the telemetry information shown on screen.
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

    // Formats the telemetry value according to its sensor category.
    private static string FormatTelemetryValue(
        double value,
        string category)
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
            return value >= 0.5 ? "ON" : "OFF";
        }

        return value % 1 == 0
            ? value.ToString("F0")
            : value.ToString("F2");
    }

    // Loads all attachments for the sensor.
    private async Task LoadAttachmentsAsync()
    {
        try
        {
            AttachmentsStatusText.IsVisible = false;

            var attachments = await _attachmentService
                .GetAttachmentsAsync(_sensor.NodeId);

            _attachments.Clear();

            foreach (var attachment in attachments)
            {
                _attachments.Add(attachment);
            }

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
