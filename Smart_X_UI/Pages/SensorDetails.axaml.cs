using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Smart_X_UI.Models;
using Smart_X_UI.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Smart_X_UI.Pages;

public partial class SensorDetails : UserControl
{
    private readonly SensorRegistration _sensor;

    private readonly AttachmentService _attachmentService =
        new(ApiClient.HttpClient);

    private readonly TelemetryService _telemetryService =
        new(ApiClient.HttpClient);

    private readonly ObservableCollection<SensorAttachment> _attachments =
        new();

    public event EventHandler? BackRequested;

    public SensorDetails(SensorRegistration sensor)
    {
        InitializeComponent();

        _sensor = sensor;

        AttachmentsItemsControl.ItemsSource = _attachments;

        LoadSensorDetails();
        _ = LoadAttachmentsAsync();
    }

    private void LoadSensorDetails()
    {
        HeaderNodeIdText.Text = _sensor.NodeId;
        NodeIdText.Text = _sensor.NodeId;
        MacAddressText.Text = _sensor.MacAddress;
        CategoryText.Text = _sensor.Category;
        LocationText.Text = _sensor.Location;
        StatusText.Text = "ACTIVE";
        EndpointText.Text = TelemetryService.GetEndpoint(_sensor);
    }

    private async Task LoadAttachmentsAsync()
    {
        try
        {
            AttachmentsStatusText.IsVisible = false;

            var attachments =
                await _attachmentService.GetAttachmentsAsync(_sensor.NodeId);

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
        {
            return;
        }

        var files =
            await topLevel.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Select attachment",
                    AllowMultiple = false
                });

        if (files.Count == 0)
        {
            return;
        }

        string? path = files[0].TryGetLocalPath();

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
        {
            return;
        }

        var file =
            await topLevel.StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Save attachment",
                    SuggestedFileName = attachment.FileName
                });

        if (file is null)
        {
            return;
        }

        string? path = file.TryGetLocalPath();

        if (string.IsNullOrWhiteSpace(path))
        {
            ShowActionStatus(
                "The destination file could not be accessed.",
                false);
            return;
        }

        try
        {
            await using Stream source =
                await _attachmentService.DownloadAttachmentAsync(
                    _sensor.NodeId,
                    attachment.Id);

            await using FileStream destination =
                File.Create(path);

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
                AttachmentsStatusText.Text =
                    "No attachments found.";

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

    private async void SimulateTelemetryButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        SimulateTelemetryButton.IsEnabled = false;

        try
        {
            using HttpResponseMessage response =
                await _telemetryService.SendTelemetryAsync(_sensor);

            string responseBody =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                TelemetryStatusText.Text = "REJECTED";
                TelemetryStatusText.Foreground = Brushes.OrangeRed;

                ShowActionStatus(
                    $"Telemetry rejected: HTTP {(int)response.StatusCode}\n{responseBody}",
                    false);

                return;
            }

            TelemetryStatusText.Text = "RECEIVED";
            TelemetryStatusText.Foreground = Brushes.LimeGreen;
            TelemetryValueText.Text = GetTelemetryValue(responseBody);
            TelemetryTimestampText.Text = GetTelemetryTimestamp(responseBody);

            ShowActionStatus(
                "Telemetry sent successfully.",
                true);
        }
        catch (HttpRequestException)
        {
            TelemetryStatusText.Text = "DISCONNECTED";
            TelemetryStatusText.Foreground = Brushes.OrangeRed;

            ShowActionStatus(
                "Could not connect to the Smart X API.",
                false);
        }
        catch (TaskCanceledException)
        {
            TelemetryStatusText.Text = "TIMEOUT";
            TelemetryStatusText.Foreground = Brushes.OrangeRed;

            ShowActionStatus(
                "The telemetry request timed out.",
                false);
        }
        catch (Exception ex)
        {
            TelemetryStatusText.Text = "ERROR";
            TelemetryStatusText.Foreground = Brushes.OrangeRed;

            ShowActionStatus(ex.Message, false);
        }
        finally
        {
            SimulateTelemetryButton.IsEnabled = true;
        }
    }

    private static string GetTelemetryValue(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return "N/A";
        }

        try
        {
            using JsonDocument document =
                JsonDocument.Parse(response);

            if (document.RootElement.TryGetProperty(
                    "value",
                    out JsonElement value))
            {
                return value.ValueKind switch
                {
                    JsonValueKind.True => "ON",
                    JsonValueKind.False => "OFF",
                    _ => value.ToString()
                };
            }
        }
        catch (JsonException)
        {
        }

        return response;
    }

    private static string GetTelemetryTimestamp(string response)
    {
        try
        {
            using JsonDocument document =
                JsonDocument.Parse(response);

            if (document.RootElement.TryGetProperty(
                    "timestamp",
                    out JsonElement timestamp) &&
                timestamp.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(
                    timestamp.GetString(),
                    out DateTime parsed))
            {
                return parsed
                    .ToLocalTime()
                    .ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
        catch (JsonException)
        {
        }

        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private void ShowActionStatus(
        string message,
        bool success)
    {
        ActionStatusText.Text = message;

        ActionStatusText.Foreground =
            success
                ? Brushes.LimeGreen
                : Brushes.OrangeRed;

        ActionStatusText.IsVisible = true;
    }

    private void BackButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }
}