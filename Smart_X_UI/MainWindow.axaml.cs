
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Smart_X_UI.Models;
using Smart_X_UI.Pages;
using Smart_X_UI.Services;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Smart_X_UI;

public partial class MainWindow : Window
{
    private readonly HttpClient _httpClient = ApiClient.HttpClient;
    private object? _homeContent;

    public MainWindow()
    {
        InitializeComponent();

        _homeContent = MainContent.Content;

        SizeChanged += MainWindow_SizeChanged;

        _ = CheckApiConnectionAsync();
    }

    private void MainWindow_SizeChanged(
        object? sender,
        SizeChangedEventArgs e)
    {
        if (MainContent.Content is Control content)
        {
            content.HorizontalAlignment =
                Avalonia.Layout.HorizontalAlignment.Stretch;

            content.VerticalAlignment =
                Avalonia.Layout.VerticalAlignment.Stretch;
        }
    }

    private async Task CheckApiConnectionAsync()
    {
        SetApiStatus(
            "CHECKING...",
            "Checking Smart X API...",
            "CONNECTING...",
            Brushes.Goldenrod);

        try
        {
            using var response =
                await _httpClient.GetAsync("api/health");

            if (response.IsSuccessStatusCode)
            {
                SetApiStatus(
                    "CONNECTED",
                    "Smart X API is running on localhost:8080",
                    "SYSTEM ONLINE",
                    Brushes.LightGreen);
            }
            else
            {
                SetApiStatus(
                    "ERROR",
                    $"API returned HTTP {(int)response.StatusCode}",
                    "API ERROR",
                    Brushes.IndianRed);
            }
        }
        catch (HttpRequestException)
        {
            SetApiStatus(
                "DISCONNECTED",
                "Could not connect to the Smart X API.",
                "SYSTEM OFFLINE",
                Brushes.IndianRed);
        }
        catch (TaskCanceledException)
        {
            SetApiStatus(
                "TIMEOUT",
                "The Smart X API request timed out.",
                "API TIMEOUT",
                Brushes.Goldenrod);
        }
        catch (Exception ex)
        {
            SetApiStatus(
                "ERROR",
                ex.Message,
                "API ERROR",
                Brushes.IndianRed);
        }
    }

    private void SetApiStatus(
        string connectionStatus,
        string statusMessage,
        string systemStatus,
        IBrush statusBrush)
    {
        ApiConnectionStatusText.Text = connectionStatus;
        ApiStatusText.Text = statusMessage;
        ApiModeText.Text = "Local";

        ApiConnectionIndicator.Fill = statusBrush;
        SystemStatusIndicator.Fill = statusBrush;

        SystemStatusText.Text = systemStatus;
        SystemStatusText.Foreground = statusBrush;

        SystemStatusDescriptionText.Text =
            connectionStatus == "CONNECTED"
                ? "Local services are available"
                : statusMessage;
    }

    private void RealTimeCommandStream_Click(
        object? sender,
        RoutedEventArgs e)
    {
        MainContent.Content = new RealTimeCommandStream();
    }

    private void SensorDataButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        ShowDashboard();
    }

    public void ShowHome()
    {
        MainContent.Content = _homeContent;
    }

    public void ShowDashboard()
    {
        var dashboard = new SensorDashboard();

        dashboard.BackRequested += Dashboard_BackRequested;
        dashboard.AddSensorRequested += Dashboard_AddSensorRequested;
        dashboard.AddLocationRequested += Dashboard_AddLocationRequested;
        dashboard.SensorDetailsRequested += Dashboard_SensorDetailsRequested;
        dashboard.ManageCategoriesRequested +=
            Dashboard_ManageCategoriesRequested;

        MainContent.Content = dashboard;
    }

    private void Dashboard_BackRequested(
        object? sender,
        EventArgs e)
    {
        ShowHome();
    }

    private void Dashboard_ManageCategoriesRequested(
        object? sender,
        EventArgs e)
    {
        var categoriesPage = new SensorCategoryManagement();

        categoriesPage.BackRequested +=
            CategoryManagement_BackRequested;

        MainContent.Content = categoriesPage;
    }

    private void CategoryManagement_BackRequested(
        object? sender,
        EventArgs e)
    {
        ShowDashboard();
    }

    private void Dashboard_AddLocationRequested(
        object? sender,
        EventArgs e)
    {
        var addLocation = new AddLocation();

        addLocation.BackRequested += AddLocation_BackRequested;
        addLocation.LocationAdded += AddLocation_LocationAdded;

        MainContent.Content = addLocation;
    }

    private void AddLocation_BackRequested(
        object? sender,
        EventArgs e)
    {
        ShowDashboard();
    }

    private void AddLocation_LocationAdded(
        object? sender,
        EventArgs e)
    {
        ShowDashboard();
    }

    private void Dashboard_AddSensorRequested(
        object? sender,
        EventArgs e)
    {
        var addSensor = new SensorAdd();

        addSensor.BackRequested += AddSensor_BackRequested;
        addSensor.SensorRegistered += AddSensor_SensorRegistered;

        MainContent.Content = addSensor;
    }

    private void AddSensor_BackRequested(
        object? sender,
        EventArgs e)
    {
        ShowDashboard();
    }

    private void AddSensor_SensorRegistered(
        object? sender,
        SensorRegistration sensor)
    {
        ShowDashboard();
    }

    private void Dashboard_SensorDetailsRequested(
        object? sender,
        SensorRegistration sensor)
    {
        var details = new SensorDetails(sensor);

        details.BackRequested += SensorDetails_BackRequested;

        MainContent.Content = details;
    }

    private void SensorDetails_BackRequested(
        object? sender,
        EventArgs e)
    {
        ShowDashboard();
    }
}
