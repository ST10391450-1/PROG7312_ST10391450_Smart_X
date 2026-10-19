
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Smart_X_UI.Models;
using Smart_X_UI.Pages;
using Smart_X_UI.Services;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Smart_X_UI;

public partial class MainWindow : Window
{
    private readonly HttpClient _httpClient = ApiClient.HttpClient;

    private readonly Dictionary<string, Control> _pageCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Func<Control>> _pageFactories =
        new(StringComparer.OrdinalIgnoreCase);

    private Control? _homeContent;

    private string? _currentPageKey = "Home";

    public int CachedPageCount => _pageCache.Count;

    public MainWindow()
    {
        InitializeComponent();

        _homeContent = MainContent.Content as Control;

        if (_homeContent is not null)
        {
            _pageCache["Home"] = _homeContent;
        }

        SizeChanged += MainWindow_SizeChanged;
        Closing += MainWindow_Closing;

        RegisterPage("SensorDashboard", CreateDashboard);
        RegisterPage("CommandStream", () => new RealTimeCommandStream());
        RegisterPage("SensorCategories", CreateCategoryManagement);
        RegisterPage("AddLocation", CreateAddLocation);
        RegisterPage("AddSensor", CreateAddSensor);

        _ = CheckApiConnectionAsync();
    }

    private void RegisterPage(string key, Func<Control> factory)
    {
        _pageFactories[key] = factory;
    }

    private Control CreateDashboard()
    {
        var dashboard = new SensorDashboard();

        dashboard.BackRequested += Dashboard_BackRequested;
        dashboard.AddSensorRequested += Dashboard_AddSensorRequested;
        dashboard.AddLocationRequested += Dashboard_AddLocationRequested;
        dashboard.SensorDetailsRequested += Dashboard_SensorDetailsRequested;
        dashboard.ManageCategoriesRequested +=
            Dashboard_ManageCategoriesRequested;

        return dashboard;
    }

    private Control CreateCategoryManagement()
    {
        var categoriesPage = new SensorCategoryManagement();

        categoriesPage.BackRequested +=
            CategoryManagement_BackRequested;

        return categoriesPage;
    }

    private Control CreateAddLocation()
    {
        var addLocation = new AddLocation();

        addLocation.BackRequested += AddLocation_BackRequested;
        addLocation.LocationAdded += AddLocation_LocationAdded;

        return addLocation;
    }

    private Control CreateAddSensor()
    {
        var addSensor = new SensorAdd();

        addSensor.BackRequested += AddSensor_BackRequested;
        addSensor.SensorRegistered += AddSensor_SensorRegistered;

        return addSensor;
    }

    private void NavigateTo(string key)
    {
        if (!_pageCache.TryGetValue(key, out var page))
        {
            if (!_pageFactories.TryGetValue(key, out var factory))
            {
                throw new KeyNotFoundException(
                    $"Page '{key}' has not been registered.");
            }

            page = factory();
            _pageCache.Add(key, page);
        }

        MainContent.Content = page;
        _currentPageKey = key;

        ApplyContentStretch(page);
    }

    private void ApplyContentStretch(Control content)
    {
        content.HorizontalAlignment =
            Avalonia.Layout.HorizontalAlignment.Stretch;

        content.VerticalAlignment =
            Avalonia.Layout.VerticalAlignment.Stretch;
    }

    private void MainWindow_SizeChanged(
        object? sender,
        SizeChangedEventArgs e)
    {
        if (MainContent.Content is Control content)
        {
            ApplyContentStretch(content);
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
        NavigateTo("CommandStream");
    }

    private void SensorDataButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        ShowDashboard();
    }

    public void ShowHome()
    {
        NavigateTo("Home");
    }

    public void ShowDashboard()
    {
        NavigateTo("SensorDashboard");
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
        NavigateTo("SensorCategories");
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
        NavigateTo("AddLocation");
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
        NavigateTo("AddSensor");
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
        var key = $"SensorDetails:{sensor.NodeId}";

        if (!_pageCache.TryGetValue(key, out var page))
        {
            var details = new SensorDetails(sensor);
            details.BackRequested += SensorDetails_BackRequested;

            page = details;
            _pageCache.Add(key, page);
        }

        MainContent.Content = page;
        _currentPageKey = key;

        ApplyContentStretch(page);
    }

    private void SensorDetails_BackRequested(
        object? sender,
        EventArgs e)
    {
        ShowDashboard();
    }

    private void MainWindow_Closing(
        object? sender,
        WindowClosingEventArgs e)
    {
        _pageCache.Clear();
        _pageFactories.Clear();
        _homeContent = null;
        _currentPageKey = null;
    }
}
