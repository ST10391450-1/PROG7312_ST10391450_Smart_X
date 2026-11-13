
using Smart_X_UI.Pages;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Smart_X_UI.Models;

namespace Smart_X_UI.Services;

public class NetworkRoutingService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public NetworkRoutingService()
    {
        _httpClient = ApiClient.HttpClient;
    }

    public async Task<NetworkMstResponse?> CalculateMinimumSpanningTreeAsync()
    {
        return await _httpClient.GetFromJsonAsync<NetworkMstResponse>(
            "api/NetworkRouting/mst",
            JsonOptions);
    }

    public async Task<NetworkShortestPathResponse?> FindMostEfficientPathAsync(
        string startNodeId,
        string destinationNodeId)
    {
        var start = Uri.EscapeDataString(startNodeId);
        var destination = Uri.EscapeDataString(destinationNodeId);

        return await _httpClient.GetFromJsonAsync<NetworkShortestPathResponse>(
            $"api/NetworkRouting/shortest-path/{start}/{destination}",
            JsonOptions);
    }
}
