
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Smart_X_UI.Models;
using Smart_X_UI.Services;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Smart_X_UI.Pages;

public partial class NetworkTopologyView : UserControl
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly NetworkRoutingService _routingService = new();

    private readonly DispatcherTimer _simulationTimer = new()
    {
        Interval = TimeSpan.FromSeconds(5)
    };

    private bool _isLoading;
    private bool _isSimulationLoading;

    public event EventHandler? BackRequested;

    public NetworkTopologyView()
    {
        InitializeComponent();

        _simulationTimer.Tick += SimulationTimer_Tick;

        Loaded += async (_, _) =>
        {
            await LoadTopologyAsync();
            await LoadSimulationAsync();
            _simulationTimer.Start();
        };

        Unloaded += (_, _) =>
        {
            _simulationTimer.Stop();
        };
    }

    #region Functionality for Initialising the Network Topology View

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    #endregion

    #region Functionality for Returning to the Previous View

    private void Back_Click(object? sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Functionality for Refreshing Network Data

    private async void RefreshTopology_Click(object? sender, RoutedEventArgs e)
    {
        await LoadTopologyAsync();
        await LoadSimulationAsync();
    }

    #endregion

    #region Functionality for Retrieving Network Topology from the API

    private async Task LoadTopologyAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;

        SetText("ApiStatusText", "Connecting to Smart X API...");
        SetText("LastUpdatedText", "Retrieving network topology...");

        try
        {
            var response = await ApiClient.HttpClient
                .GetFromJsonAsync<NetworkTopologyResponse>(
                    "api/NetworkTopology",
                    JsonOptions);

            if (response == null)
            {
                SetText("ApiStatusText", "API returned an empty response.");
                return;
            }

            DisplayTopology(response);

            SetText("ApiStatusText", "Connected to Smart X API");

            SetText(
                "LastUpdatedText",
                $"Last updated: {response.GeneratedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        }
        catch (HttpRequestException ex)
        {
            SetText("ApiStatusText", "API connection failed");
            SetText("LastUpdatedText", ex.Message);
        }
        catch (TaskCanceledException)
        {
            SetText("ApiStatusText", "API request timed out");
            SetText("LastUpdatedText", "The API did not respond within 30 seconds.");
        }
        catch (JsonException ex)
        {
            SetText("ApiStatusText", "Invalid API response");
            SetText("LastUpdatedText", ex.Message);
        }
        catch (Exception ex)
        {
            SetText("ApiStatusText", "Unexpected error");
            SetText("LastUpdatedText", ex.Message);
        }
        finally
        {
            _isLoading = false;
        }
    }

    #endregion

    #region Functionality for Displaying Network Topology Data

    private void DisplayTopology(NetworkTopologyResponse topology)
    {
        SetText("RegisteredNodesValue", topology.TotalNodes.ToString());
        SetText("ConnectedNodesValue", topology.OnlineNodes.ToString());
        SetText("DisconnectedNodesValue", topology.OfflineNodes.ToString());

        var nodesList = this.FindControl<ItemsControl>("NodesList");
        var connectionsList = this.FindControl<ItemsControl>("ConnectionsList");

        if (nodesList != null)
            nodesList.ItemsSource = topology.Nodes;

        if (connectionsList != null)
            connectionsList.ItemsSource = topology.Connections;

        SetText(
            "NodesEmptyText",
            topology.Nodes.Count == 0
                ? "No registered network nodes were returned by the API."
                : string.Empty);

        SetText(
            "ConnectionsEmptyText",
            topology.Connections.Count == 0
                ? "No recorded network connections are available."
                : string.Empty);
    }

    #endregion

    #region Functionality for Retrieving Virtual Network Simulation Data

    private async void SimulationTimer_Tick(object? sender, EventArgs e)
    {
        await LoadSimulationAsync();
    }

    private async Task LoadSimulationAsync()
    {
        if (_isSimulationLoading)
            return;

        _isSimulationLoading = true;

        try
        {
            var response = await ApiClient.HttpClient
                .GetFromJsonAsync<NetworkSimulationSnapshotResponse>(
                    "api/NetworkSimulation",
                    JsonOptions);

            if (response == null)
            {
                SetText(
                    "SimulationStatusText",
                    "Simulation API returned an empty response.");

                return;
            }

            DisplaySimulation(response);
        }
        catch (HttpRequestException ex)
        {
            SetText(
                "SimulationStatusText",
                $"Simulation API connection failed: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            SetText(
                "SimulationStatusText",
                "Simulation API request timed out.");
        }
        catch (JsonException ex)
        {
            SetText(
                "SimulationStatusText",
                $"Invalid simulation response: {ex.Message}");
        }
        catch (Exception ex)
        {
            SetText(
                "SimulationStatusText",
                $"Simulation error: {ex.Message}");
        }
        finally
        {
            _isSimulationLoading = false;
        }
    }

    #endregion

    #region Functionality for Displaying Virtual Network Simulation Data

    private void DisplaySimulation(NetworkSimulationSnapshotResponse simulation)
    {
        SetText(
            "SimulationTotalNodesValue",
            simulation.TotalNodes.ToString());

        SetText(
            "SimulationOnlineNodesValue",
            simulation.OnlineNodes.ToString());

        SetText(
            "SimulationOfflineNodesValue",
            simulation.OfflineNodes.ToString());

        SetText(
            "SimulationStatusText",
            "Virtual network simulation connected");

        SetText(
            "SimulationUpdatedText",
            $"Last updated: {simulation.GeneratedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}");

        var nodesList = this.FindControl<ItemsControl>("SimulationNodesList");

        var connectionsList = this.FindControl<ItemsControl>(
            "SimulationConnectionsList");

        if (nodesList != null)
        {
            nodesList.ItemsSource = simulation.Nodes;

            SetText(
                "SimulationNodesEmptyText",
                simulation.Nodes.Count == 0
                    ? "No virtual nodes are currently available."
                    : string.Empty);
        }

        if (connectionsList != null)
        {
            connectionsList.ItemsSource = simulation.Connections;

            SetText(
                "SimulationConnectionsEmptyText",
                simulation.Connections.Count == 0
                    ? "No virtual connections are currently available."
                    : string.Empty);
        }
    }

    #endregion

    #region Functionality for Calculating the Minimum Spanning Tree

    private async void CalculateMst_Click(object? sender, RoutedEventArgs e)
    {
        SetText("MstStatusText", "Calculating minimum spanning tree...");
        SetText("MstEdgesText", "Retrieving selected network connections...");

        try
        {
            var response = await _routingService.CalculateMinimumSpanningTreeAsync();

            if (response == null)
            {
                SetText("MstStatusText", "The API returned an empty response.");
                SetText("MstEdgesText", "No MST results available.");
                return;
            }

            DisplayMinimumSpanningTree(response);
        }
        catch (HttpRequestException ex)
        {
            SetText("MstStatusText", "Unable to calculate minimum spanning tree.");
            SetText("MstEdgesText", ex.Message);
        }
        catch (TaskCanceledException)
        {
            SetText("MstStatusText", "MST request timed out.");
        }
        catch (JsonException ex)
        {
            SetText("MstStatusText", "Invalid MST response.");
            SetText("MstEdgesText", ex.Message);
        }
        catch (Exception ex)
        {
            SetText("MstStatusText", "Unexpected MST error.");
            SetText("MstEdgesText", ex.Message);
        }
    }

    private void DisplayMinimumSpanningTree(NetworkMstResponse response)
    {
        SetText(
            "TotalNetworkCostValue",
            response.TotalPowerCost.ToString("F2"));

        SetText(
            "SelectedConnectionsValue",
            response.SelectedEdgeCount.ToString());

        SetText(
            "ConnectedComponentsValue",
            response.ConnectedComponents.ToString());

        SetText(
            "MstStatusText",
            response.IsFullyConnected
                ? "Network is fully connected. A spanning tree has been calculated."
                : "Network contains disconnected components. A minimum spanning forest has been calculated.");

        if (response.Edges.Count == 0)
        {
            SetText("MstEdgesText", "No connections were selected.");
            return;
        }

        var result = new StringBuilder();

        foreach (var edge in response.Edges)
        {
            result.AppendLine(
                $"{edge.SourceNodeId} → {edge.DestinationNodeId}");

            result.AppendLine(
                $"Power cost: {edge.TransmissionPowerCost:F2} | Signal: {edge.SignalStrength:F2}");

            result.AppendLine();
        }

        SetText("MstEdgesText", result.ToString().TrimEnd());
    }

    #endregion

    #region Functionality for Finding the Most Efficient Network Path

    private async void FindShortestPath_Click(object? sender, RoutedEventArgs e)
    {
        var startNode = this.FindControl<TextBox>("StartNodeIdTextBox")?.Text?.Trim();

        var destinationNode = this.FindControl<TextBox>(
            "DestinationNodeIdTextBox")?.Text?.Trim();

        if (string.IsNullOrWhiteSpace(startNode))
        {
            SetText("ShortestPathResultsText", "Enter a starting node ID.");
            return;
        }

        if (string.IsNullOrWhiteSpace(destinationNode))
        {
            SetText("ShortestPathResultsText", "Enter a destination node ID.");
            return;
        }

        if (string.Equals(
            startNode,
            destinationNode,
            StringComparison.OrdinalIgnoreCase))
        {
            SetText(
                "ShortestPathResultsText",
                "Starting and destination nodes must be different.");

            return;
        }

        SetText(
            "ShortestPathResultsText",
            "Calculating the most efficient route...");

        try
        {
            var response = await _routingService.FindMostEfficientPathAsync(
                startNode,
                destinationNode);

            if (response == null)
            {
                SetText(
                    "ShortestPathResultsText",
                    "The API returned an empty response.");

                return;
            }

            DisplayShortestPath(response);
        }
        catch (HttpRequestException ex)
        {
            SetText(
                "ShortestPathResultsText",
                $"Unable to retrieve shortest path: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            SetText(
                "ShortestPathResultsText",
                "Shortest-path request timed out.");
        }
        catch (JsonException ex)
        {
            SetText(
                "ShortestPathResultsText",
                $"Invalid routing response: {ex.Message}");
        }
        catch (Exception ex)
        {
            SetText(
                "ShortestPathResultsText",
                $"Unexpected routing error: {ex.Message}");
        }
    }

    private void DisplayShortestPath(NetworkShortestPathResponse response)
    {
        if (!response.IsReachable || response.PathNodeIds.Count == 0)
        {
            SetText(
                "ShortestPathResultsText",
                $"No route exists between {response.StartNodeId} and {response.DestinationNodeId}.");

            return;
        }

        var result = new StringBuilder();

        result.AppendLine("Route:");
        result.AppendLine(string.Join(" → ", response.PathNodeIds));
        result.AppendLine();
        result.AppendLine($"Starting node: {response.StartNodeId}");
        result.AppendLine($"Destination node: {response.DestinationNodeId}");
        result.AppendLine($"Hop count: {response.HopCount}");
        result.AppendLine($"Total power cost: {response.TotalPowerCost:F2}");

        SetText("ShortestPathResultsText", result.ToString());
    }

    #endregion

    #region Functionality for Updating Interface Text

    private void SetText(string controlName, string value)
    {
        var control = this.FindControl<TextBlock>(controlName);

        if (control != null)
            control.Text = value;
    }

    #endregion
}

public class NetworkTopologyResponse
{
    public DateTime GeneratedAt { get; set; }
    public int TotalNodes { get; set; }
    public int OnlineNodes { get; set; }
    public int OfflineNodes { get; set; }
    public List<NetworkNodeResponse> Nodes { get; set; } = new();
    public List<NetworkConnectionResponse> Connections { get; set; } = new();
}

public class NetworkNodeResponse
{
    public string NodeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
}

public class NetworkConnectionResponse
{
    public string SourceNodeId { get; set; } = string.Empty;
    public string DestinationNodeId { get; set; } = string.Empty;
    public double SignalStrength { get; set; }
    public double TransmissionPowerCost { get; set; }
}

public class NetworkSimulationSnapshotResponse
{
    public DateTime GeneratedAt { get; set; }
    public int TotalNodes { get; set; }
    public int OnlineNodes { get; set; }
    public int OfflineNodes { get; set; }
    public List<SimulatedNetworkNodeResponse> Nodes { get; set; } = new();
    public List<SimulatedNetworkConnectionResponse> Connections { get; set; } = new();
}

public class SimulatedNetworkNodeResponse
{
    public string NodeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public DateTime LastUpdated { get; set; }
    public string Status => IsOnline ? "ONLINE" : "OFFLINE";
}

public class SimulatedNetworkConnectionResponse
{
    public string SourceNodeId { get; set; } = string.Empty;
    public string DestinationNodeId { get; set; } = string.Empty;
    public double DistanceMeters { get; set; }
    public double SignalStrength { get; set; }
    public double TransmissionPowerCost { get; set; }
    public DateTime LastUpdated { get; set; }
}
