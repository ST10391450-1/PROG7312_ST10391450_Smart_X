using Smart_X_UI.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Smart_X_UI.Services;

public class SensorService
{
    private readonly HttpClient _httpClient;

    public SensorService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // Gets all registered sensors from the API.
    public async Task<List<SensorRegistration>> GetSensorsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<SensorRegistration>>(
            "api/Sensors") ?? new List<SensorRegistration>();
    }

    // Registers a new sensor with the API.
    public async Task<SensorRegistration> RegisterSensorAsync(
        SensorRegistration sensor)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/Sensors",
            sensor);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<SensorRegistration>()
            ?? sensor;
    }

    // Removes a sensor from the AP
   public async Task DeleteSensorAsync(string nodeId)
    {
        using var response = await _httpClient.DeleteAsync(
            $"api/Sensors/{Uri.EscapeDataString(nodeId)}");

        response.EnsureSuccessStatusCode();
    }
    public async Task<SensorRegistration?> GetSensorAsync(string nodeId)
    {
        using var response = await _httpClient.GetAsync(
            $"api/Sensors/{Uri.EscapeDataString(nodeId)}");

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content
            .ReadFromJsonAsync<SensorRegistration>();
    }
}
