
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Smart_X_UI.Models;

namespace Smart_X_UI.Services;

public class SensorCategoryService
{
    private readonly HttpClient _httpClient;
    private const string Endpoint = "api/SensorCategories";

    public SensorCategoryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<SensorCategory>> GetAllAsync()
    {
        using var response = await _httpClient.GetAsync(Endpoint);
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<List<SensorCategory>>()
               ?? new List<SensorCategory>();
    }

    public async Task<SensorCategory> CreateAsync(SensorCategory category)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            Endpoint,
            category);

        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<SensorCategory>()
               ?? throw new InvalidOperationException(
                   "The API did not return the created category.");
    }

    public async Task UpdateAsync(Guid id, SensorCategory category)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            $"{Endpoint}/{id}",
            category);

        await EnsureSuccessAsync(response);
    }

    public async Task DeleteAsync(Guid id)
    {
        using var response = await _httpClient.DeleteAsync(
            $"{Endpoint}/{id}");

        await EnsureSuccessAsync(response);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync();

        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(body)
                ? $"API request failed: {(int)response.StatusCode} {response.ReasonPhrase}"
                : body,
            null,
            response.StatusCode);
    }
}
