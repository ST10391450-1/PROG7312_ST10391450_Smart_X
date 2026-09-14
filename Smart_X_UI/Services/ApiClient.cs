using System;
using System.Net.Http;

namespace Smart_X_UI.Services;

public static class ApiClient
{
    public static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri("http://localhost:8080/"),
        Timeout = TimeSpan.FromSeconds(30)
    };
}