using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Smart_X_UI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Smart_X_UI.Pages;

public partial class SensorPayloadManagement
{
    private static readonly string[] SupportedAttachmentPatterns =
    {
        "*.txt",
        "*.log",
        "*.cfg",
        "*.conf",
        "*.ini",
        "*.json",
        "*.xml",
        "*.yaml",
        "*.yml",
        "*.csv",
        "*.jpg",
        "*.jpeg",
        "*.png",
        "*.webp",
        "*.pdf"
    };

    private async void AttachFileButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SensorRegistration sensor })
        {
            await ShowMessage("Could not identify the sensor.");
            return;
        }

        AttachmentSensorComboBox.SelectedItem = sensor;

        var topLevel = TopLevel.GetTopLevel(this);

        if (topLevel is null)
        {
            await ShowMessage("Could not open the file picker.");
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Select attachment",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                new("Supported files")
                {
                    Patterns = SupportedAttachmentPatterns
                }
                }
            });

        if (files.Count == 0)
        {
            return;
        }

        await AttachFileAsync(sensor, files[0]);
    }

    private async Task AttachFileAsync(
        SensorRegistration sensor,
        IStorageFile file)
    {
        try
        {
            var localPath = file.TryGetLocalPath();

            if (string.IsNullOrWhiteSpace(localPath))
            {
                await ShowMessage(
                    "The selected file could not be accessed.");
                return;
            }

            var fileInfo = new FileInfo(localPath);

            const long maxFileSize = 50L * 1024L * 1024L;

            if (fileInfo.Length > maxFileSize)
            {
                await ShowMessage(
                    "The selected file is larger than the 50 MB limit.");
                return;
            }

            await UploadAttachmentAsync(
                sensor.NodeId,
                localPath);
        }
        catch (Exception ex)
        {
            await ShowMessage(
                $"Could not attach file.\n\n{ex.Message}");
        }
    }

    private async Task UploadAttachmentAsync(
        string nodeId,
        string filePath)
    {
        try
        {
            await using var stream =
                File.OpenRead(filePath);

            using var content = new MultipartFormDataContent();

            using var fileContent =
                new StreamContent(stream);

            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue(
                    "application/octet-stream");

            content.Add(
                fileContent,
                "file",
                Path.GetFileName(filePath));

            var endpoint =
                $"api/Sensors/{Uri.EscapeDataString(nodeId)}/attachments";

            using var response =
                await HttpClient.PostAsync(endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var error =
                    await response.Content.ReadAsStringAsync();

                await ShowMessage(
                    $"Attachment upload failed.\n\n" +
                    $"HTTP {(int)response.StatusCode}\n\n" +
                    error);

                return;
            }

            await ShowMessage(
                $"'{Path.GetFileName(filePath)}' was uploaded successfully.");

            await LoadAttachmentsAsync(nodeId);
        }
        catch (Exception ex)
        {
            await ShowMessage(
                $"Could not upload attachment.\n\n{ex.Message}");
        }
    }

    private async Task LoadAttachmentsAsync(string nodeId)
    {
        try
        {
            var endpoint =
                $"api/Sensors/{Uri.EscapeDataString(nodeId)}/attachments";

            var attachments =
                await HttpClient.GetFromJsonAsync<List<SensorAttachment>>(endpoint);

            _attachments.Clear();

            if (attachments is not null)
            {
                foreach (var attachment in attachments)
                {
                    _attachments.Add(attachment);
                }
            }
        }
        catch (Exception ex)
        {
            await ShowMessage(
                $"Could not load attachments.\n\n{ex.Message}");
        }
    }

    private async void RefreshAttachmentsButton_Click(
    object? sender,
    RoutedEventArgs e)
    {
        if (AttachmentSensorComboBox.SelectedItem is not SensorRegistration sensor)
        {
            await ShowMessage("Select a sensor first.");
            return;
        }

        await LoadAttachmentsAsync(sensor.NodeId);
    }

    private async void LoadAttachmentsButton_Click(
    object? sender,
    RoutedEventArgs e)
    {
        if (AttachmentSensorComboBox.SelectedItem is not SensorRegistration sensor)
        {
            await ShowMessage("Select a sensor first.");
            return;
        }

        await LoadAttachmentsAsync(sensor.NodeId);
    }

    private async void DownloadAttachmentButton_Click(
    object? sender,
    RoutedEventArgs e)
    {
        if (AttachmentSensorComboBox.SelectedItem is not SensorRegistration sensor)
        {
            await ShowMessage("Select a sensor first.");
            return;
        }

        if (sender is not Button { Tag: SensorAttachment attachment })
        {
            await ShowMessage("Could not identify the attachment.");
            return;
        }

        await DownloadAttachmentAsync(sensor, attachment);
    }
    private async Task DownloadAttachmentAsync(
        SensorRegistration sensor,
        SensorAttachment attachment)
    {
        var topLevel = TopLevel.GetTopLevel(this);

        if (topLevel is null)
        {
            await ShowMessage("Could not open the save dialog.");
            return;
        }

        try
        {
            var suggestedName =
                string.IsNullOrWhiteSpace(attachment.FileName)
                    ? "attachment"
                    : attachment.FileName;

            var saveFile =
                await topLevel.StorageProvider.SaveFilePickerAsync(
                    new FilePickerSaveOptions
                    {
                        Title = "Save attachment",
                        SuggestedFileName = suggestedName
                    });

            if (saveFile is null)
            {
                return;
            }

            var endpoint =
                $"api/Sensors/" +
                $"{Uri.EscapeDataString(sensor.NodeId)}/attachments/" +
                $"{Uri.EscapeDataString(attachment.Id.ToString())}";

            using var response =
                await HttpClient.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                var error =
                    await response.Content.ReadAsStringAsync();

                await ShowMessage(
                    $"Download failed.\n\n" +
                    $"HTTP {(int)response.StatusCode}\n\n" +
                    error);

                return;
            }

            await using var input =
                await response.Content.ReadAsStreamAsync();

            await using var output =
                await saveFile.OpenWriteAsync();

            await input.CopyToAsync(output);

            await ShowMessage(
                $"'{suggestedName}' was downloaded successfully.");
        }
        catch (Exception ex)
        {
            await ShowMessage(
                $"Could not download attachment.\n\n{ex.Message}");
        }
    }

    private async void DeleteAttachmentButton_Click(
    object? sender,
    RoutedEventArgs e)
    {
        if (AttachmentSensorComboBox.SelectedItem is not SensorRegistration sensor)
        {
            await ShowMessage("Select a sensor first.");
            return;
        }

        if (sender is not Button { Tag: SensorAttachment attachment })
        {
            await ShowMessage("Could not identify the attachment.");
            return;
        }

        await DeleteAttachmentAsync(sensor, attachment);
    }

    private async Task DeleteAttachmentAsync(
        SensorRegistration sensor,
        SensorAttachment attachment)
    {
        try
        {
            var endpoint =
                $"api/Sensors/" +
                $"{Uri.EscapeDataString(sensor.NodeId)}/attachments/" +
                $"{Uri.EscapeDataString(attachment.Id.ToString())}";

            using var response =
                await HttpClient.DeleteAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                var error =
                    await response.Content.ReadAsStringAsync();

                await ShowMessage(
                    $"Delete failed.\n\n" +
                    $"HTTP {(int)response.StatusCode}\n\n" +
                    error);

                return;
            }

            await ShowMessage(
                $"'{attachment.FileName}' was deleted.");

            await LoadAttachmentsAsync(sensor.NodeId);
        }
        catch (Exception ex)
        {
            await ShowMessage(
                $"Could not delete attachment.\n\n{ex.Message}");
        }
    }
}