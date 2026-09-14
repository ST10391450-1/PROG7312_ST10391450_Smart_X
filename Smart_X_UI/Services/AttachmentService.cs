using Smart_X_UI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Smart_X_UI.Services;

public class AttachmentService
{
    private readonly HttpClient _httpClient;

    public AttachmentService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<SensorAttachment>> GetAttachmentsAsync(
        string nodeId)
    {
        var endpoint =
            $"api/Sensors/{Uri.EscapeDataString(nodeId)}/attachments";

        return await _httpClient.GetFromJsonAsync<List<SensorAttachment>>(
            endpoint) ?? new List<SensorAttachment>();
    }

    public async Task UploadAttachmentAsync(
        string nodeId,
        string filePath)
    {
        await using var stream = File.OpenRead(filePath);

        using var content = new MultipartFormDataContent();

        using var fileContent = new StreamContent(stream);

        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");

        content.Add(
            fileContent,
            "file",
            Path.GetFileName(filePath));

        var endpoint =
            $"api/Sensors/{Uri.EscapeDataString(nodeId)}/attachments";

        using HttpResponseMessage response =
            await _httpClient.PostAsync(endpoint, content);

        response.EnsureSuccessStatusCode();
    }

    public async Task<Stream> DownloadAttachmentAsync(
        string nodeId,
        Guid attachmentId)
    {
        var endpoint =
            $"api/Sensors/" +
            $"{Uri.EscapeDataString(nodeId)}/attachments/" +
            $"{Uri.EscapeDataString(attachmentId.ToString())}";

        using HttpResponseMessage response =
            await _httpClient.GetAsync(endpoint);

        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync();

        var memoryStream = new MemoryStream();

        await stream.CopyToAsync(memoryStream);

        memoryStream.Position = 0;

        return memoryStream;
    }

    public async Task DeleteAttachmentAsync(
        string nodeId,
        Guid attachmentId)
    {
        var endpoint =
            $"api/Sensors/" +
            $"{Uri.EscapeDataString(nodeId)}/attachments/" +
            $"{Uri.EscapeDataString(attachmentId.ToString())}";

        using HttpResponseMessage response =
            await _httpClient.DeleteAsync(endpoint);

        response.EnsureSuccessStatusCode();
    }
}