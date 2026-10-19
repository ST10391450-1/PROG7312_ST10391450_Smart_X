using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Smart_X_UI.Models;

namespace Smart_X_UI.Services;

public sealed class CommandStreamService
{
    private readonly HttpClient _httpClient;

    public CommandStreamService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<List<SensorReadingRecord>> GetTelemetryAsync(
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<List<SensorReadingRecord>>(
            "api/Telemetry",
            cancellationToken) ?? new List<SensorReadingRecord>();
    }

    public async Task<List<CommandHistoryRecord>> GetHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<List<CommandHistoryRecord>>(
            "api/Commands",
            cancellationToken) ?? new List<CommandHistoryRecord>();
    }

    public Task<HttpResponseMessage> EnableSensorAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(nodeId, "EnableSensor", cancellationToken);
    }

    public Task<HttpResponseMessage> DisableSensorAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(nodeId, "DisableSensor", cancellationToken);
    }

    public Task<HttpResponseMessage> SendOverrideAsync(
        string nodeId,
        string command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("A command is required.", nameof(command));

        return SendCommandAsync(nodeId, command.Trim(), cancellationToken);
    }

    private async Task<HttpResponseMessage> SendCommandAsync(
        string nodeId,
        string command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ArgumentException("A device node ID is required.", nameof(nodeId));

        var request = new CommandRequest
        {
            NodeId = nodeId.Trim(),
            Command = command
        };

        var response = await _httpClient.PostAsJsonAsync(
            "api/Commands",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusCode = (int)response.StatusCode;

            response.Dispose();

            throw new HttpRequestException(
                $"API command failed ({statusCode}): {error}");
        }

        return response;
    }
}

public sealed class CommandRequest
{
    public string NodeId { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
}

public sealed class CommandHistoryRecord
{
    public string? NodeId { get; set; }
    public string? Command { get; set; }
    public string? Status { get; set; }
    public string? Result { get; set; }
    public DateTime CreatedAt { get; set; }
}