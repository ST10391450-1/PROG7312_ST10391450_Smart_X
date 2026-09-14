using System;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Smart_X_UI.Pages;

namespace Smart_X_UI;

public partial class MainWindow : Window
{
    private const string ApiBaseUrl = "http://localhost:8080";

    private readonly HttpClient _httpClient;
    private readonly Control? _dashboardContent;

    public MainWindow()
    {
        InitializeComponent();

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        _dashboardContent = MainContent.Content as Control;

        _ = CheckApiConnectionAsync();
    }

    private async Task CheckApiConnectionAsync()
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync($"{ApiBaseUrl}/api/health");

            if (response.IsSuccessStatusCode)
            {
                SetApiConnected();
            }
            else
            {
                SetApiDisconnected($"HTTP {(int)response.StatusCode}");
            }
        }
        catch (TaskCanceledException)
        {
            SetApiDisconnected("Timeout");
        }
        catch (HttpRequestException)
        {
            SetApiDisconnected("Unavailable");
        }
        catch (Exception)
        {
            SetApiDisconnected("Connection error");
        }
    }

    private void SetApiConnected()
    {
        ApiConnectionStatusText.Text = "CONNECTED";
        ApiConnectionStatusText.Foreground = new SolidColorBrush(Color.Parse("#35B86B"));
        ApiStatusText.Text = "Ready";
        ApiConnectionIndicator.Fill = new SolidColorBrush(Color.Parse("#35B86B"));
    }

    private void SetApiDisconnected(string reason)
    {
        ApiConnectionStatusText.Text = "DISCONNECTED";
        ApiConnectionStatusText.Foreground = new SolidColorBrush(Color.Parse("#C95B63"));
        ApiStatusText.Text = reason;
        ApiConnectionIndicator.Fill = new SolidColorBrush(Color.Parse("#C95B63"));
    }

    private void SensorDataButton_Click(object? sender, RoutedEventArgs e)
    {
        MainContent.Content = new SensorPayloadManagement();
    }

    public void ShowDashboard()
    {
        if (_dashboardContent != null)
        {
            MainContent.Content = _dashboardContent;
        }

        _ = CheckApiConnectionAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        _httpClient.Dispose();
        base.OnClosed(e);
    }
}
