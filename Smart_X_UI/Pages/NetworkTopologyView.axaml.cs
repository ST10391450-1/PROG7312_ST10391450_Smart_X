
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Smart_X_UI.Models;
using Smart_X_UI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
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

        RenderSimulationGraph(simulation);
    }

    #endregion

    #region Functionality for Rendering the Virtual Network Graph

    private void RenderSimulationGraph(NetworkSimulationSnapshotResponse simulation)
    {
        var canvas = this.FindControl<Canvas>("SimulationGraphCanvas");

        if (canvas == null)
            return;

        canvas.Children.Clear();

        var nodes = simulation.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.NodeId))
            .GroupBy(
                node => node.NodeId,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (nodes.Count == 0)
        {
            SetText(
                "SimulationGraphStatusText",
                "No virtual nodes are available.");

            return;
        }

        const double nodeWidth = 112;
        const double nodeHeight = 58;

        var canvasWidth = double.IsNaN(canvas.Width) ||
                          double.IsInfinity(canvas.Width) ||
                          canvas.Width <= 0
            ? 700
            : canvas.Width;

        var canvasHeight = double.IsNaN(canvas.Height) ||
                           double.IsInfinity(canvas.Height) ||
                           canvas.Height <= 0
            ? 460
            : canvas.Height;

        var positions = CalculateGraphPositions(
            nodes,
            simulation.Connections,
            canvasWidth,
            canvasHeight,
            nodeWidth,
            nodeHeight);

        var validConnections = simulation.Connections
            .Where(connection =>
                !string.IsNullOrWhiteSpace(connection.SourceNodeId) &&
                !string.IsNullOrWhiteSpace(connection.DestinationNodeId) &&
                positions.ContainsKey(connection.SourceNodeId) &&
                positions.ContainsKey(connection.DestinationNodeId) &&
                !connection.SourceNodeId.Equals(
                    connection.DestinationNodeId,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var connection in validConnections)
        {
            var source = positions[connection.SourceNodeId];
            var destination = positions[connection.DestinationNodeId];

            var signal = connection.SignalStrength;

            var line = new Line
            {
                StartPoint = new Point(source.X, source.Y),
                EndPoint = new Point(destination.X, destination.Y),
                Stroke = GetSignalBrush(signal),
                StrokeThickness = signal >= 70 ? 3 : signal >= 40 ? 2 : 1.5,
                Opacity = 0.85
            };

            ToolTip.SetTip(
                line,
                $"Distance: {connection.DistanceMeters:F2} m\n" +
                $"Signal strength: {signal:F2}%\n" +
                $"Transmission power cost: {connection.TransmissionPowerCost:F2}");

            canvas.Children.Add(line);

            var midpointX = (source.X + destination.X) / 2;
            var midpointY = (source.Y + destination.Y) / 2;

            var edgeLabel = new Border
            {
                Background = Brush("#102432"),
                BorderBrush = Brush("#254253"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 2),
                Child = new StackPanel
                {
                    Spacing = 1,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"{connection.DistanceMeters:F0} m",
                            FontSize = 8,
                            Foreground = Brush("#DCE7ED")
                        },
                        new TextBlock
                        {
                            Text = $"Signal: {signal:F0}%",
                            FontSize = 8,
                            Foreground = GetSignalBrush(signal)
                        },
                        new TextBlock
                        {
                            Text = $"Power: {connection.TransmissionPowerCost:F2}",
                            FontSize = 8,
                            Foreground = Brush("#8FA7B8")
                        }
                    }
                }
            };

            canvas.Children.Add(edgeLabel);

            Canvas.SetLeft(edgeLabel, midpointX - 32);
            Canvas.SetTop(edgeLabel, midpointY - 22);
        }

        foreach (var node in nodes)
        {
            var position = positions[node.NodeId];
            var isOnline = node.IsOnline;

            var nodeBorder = new Border
            {
                Width = nodeWidth,
                Height = nodeHeight,
                Background = Brush(isOnline ? "#173C3A" : "#49352D"),
                BorderBrush = Brush(isOnline ? "#72D6B4" : "#E5A27F"),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(6),
                Child = new StackPanel
                {
                    Spacing = 3,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = node.Name,
                            FontSize = 10,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = Brush("#DCE7ED"),
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        },
                        new TextBlock
                        {
                            Text = node.NodeId,
                            FontSize = 9,
                            Foreground = Brush("#8FA7B8"),
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        },
                        new TextBlock
                        {
                            Text = isOnline ? "ONLINE" : "OFFLINE",
                            FontSize = 8,
                            FontWeight = FontWeight.Bold,
                            Foreground = Brush(isOnline ? "#72D6B4" : "#E5A27F"),
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                        }
                    }
                }
            };

            ToolTip.SetTip(
                nodeBorder,
                $"{node.Name}\n" +
                $"{node.NodeId}\n" +
                $"Status: {(isOnline ? "Online" : "Offline")}\n" +
                $"Last updated: {node.LastUpdated.ToLocalTime():yyyy-MM-dd HH:mm:ss}");

            canvas.Children.Add(nodeBorder);

            Canvas.SetLeft(nodeBorder, position.X - nodeWidth / 2);
            Canvas.SetTop(nodeBorder, position.Y - nodeHeight / 2);
        }

        var onlineCount = nodes.Count(node => node.IsOnline);
        var offlineCount = nodes.Count - onlineCount;

        SetText(
            "SimulationGraphStatusText",
            $"{nodes.Count} nodes | {validConnections.Count} connections | " +
            $"{onlineCount} online | {offlineCount} offline");
    }

    private Dictionary<string, (double X, double Y)> CalculateGraphPositions(
        IReadOnlyList<SimulatedNetworkNodeResponse> nodes,
        IReadOnlyList<SimulatedNetworkConnectionResponse> connections,
        double canvasWidth,
        double canvasHeight,
        double nodeWidth,
        double nodeHeight)
    {
        var result = new Dictionary<string, (double X, double Y)>(
            StringComparer.OrdinalIgnoreCase);

        var count = nodes.Count;

        if (count == 0)
            return result;

        var centreX = canvasWidth / 2;
        var centreY = canvasHeight / 2;

        var boundaryX = nodeWidth / 2 + 20;
        var boundaryY = nodeHeight / 2 + 20;

        var positions = new double[count, 2];
        var forces = new double[count, 2];

        var indexes = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

        var initialRadius = Math.Max(
            50,
            Math.Min(canvasWidth, canvasHeight) * 0.34);

        for (var i = 0; i < count; i++)
        {
            indexes[nodes[i].NodeId] = i;

            var angle = 2 * Math.PI * i / count;

            positions[i, 0] = centreX + initialRadius * Math.Cos(angle);
            positions[i, 1] = centreY + initialRadius * Math.Sin(angle);
        }

        var edges = connections
            .Where(connection =>
                !string.IsNullOrWhiteSpace(connection.SourceNodeId) &&
                !string.IsNullOrWhiteSpace(connection.DestinationNodeId) &&
                indexes.ContainsKey(connection.SourceNodeId) &&
                indexes.ContainsKey(connection.DestinationNodeId))
            .Select(connection => (
                Source: indexes[connection.SourceNodeId],
                Destination: indexes[connection.DestinationNodeId]))
            .Where(edge => edge.Source != edge.Destination)
            .ToList();

        var idealDistance = Math.Max(
            1,
            Math.Sqrt(canvasWidth * canvasHeight / count));

        for (var iteration = 0; iteration < 100; iteration++)
        {
            Array.Clear(forces, 0, forces.Length);

            for (var i = 0; i < count; i++)
            {
                for (var j = i + 1; j < count; j++)
                {
                    var dx = positions[i, 0] - positions[j, 0];
                    var dy = positions[i, 1] - positions[j, 1];

                    var distance = Math.Max(
                        1,
                        Math.Sqrt(dx * dx + dy * dy));

                    var repulsion = idealDistance * idealDistance / distance;

                    var forceX = dx / distance * repulsion;
                    var forceY = dy / distance * repulsion;

                    forces[i, 0] += forceX;
                    forces[i, 1] += forceY;

                    forces[j, 0] -= forceX;
                    forces[j, 1] -= forceY;
                }
            }

            foreach (var edge in edges)
            {
                var dx = positions[edge.Destination, 0] -
                         positions[edge.Source, 0];

                var dy = positions[edge.Destination, 1] -
                         positions[edge.Source, 1];

                var distance = Math.Max(
                    1,
                    Math.Sqrt(dx * dx + dy * dy));

                var attraction = distance * distance / idealDistance;

                var forceX = dx / distance * attraction;
                var forceY = dy / distance * attraction;

                forces[edge.Source, 0] += forceX;
                forces[edge.Source, 1] += forceY;

                forces[edge.Destination, 0] -= forceX;
                forces[edge.Destination, 1] -= forceY;
            }

            var temperature = 18.0 * (1.0 - iteration / 100.0);

            for (var i = 0; i < count; i++)
            {
                var forceLength = Math.Max(
                    1,
                    Math.Sqrt(
                        forces[i, 0] * forces[i, 0] +
                        forces[i, 1] * forces[i, 1]));

                var movement = Math.Min(forceLength, temperature);

                positions[i, 0] += forces[i, 0] / forceLength * movement;
                positions[i, 1] += forces[i, 1] / forceLength * movement;

                positions[i, 0] = Math.Clamp(
                    positions[i, 0],
                    boundaryX,
                    Math.Max(boundaryX, canvasWidth - boundaryX));

                positions[i, 1] = Math.Clamp(
                    positions[i, 1],
                    boundaryY,
                    Math.Max(boundaryY, canvasHeight - boundaryY));
            }
        }

        for (var i = 0; i < count; i++)
        {
            result[nodes[i].NodeId] = (
                positions[i, 0],
                positions[i, 1]);
        }

        return result;
    }

    private static IBrush GetSignalBrush(double signalStrength)
    {
        if (signalStrength >= 70)
            return Brush("#72D6B4");

        if (signalStrength >= 40)
            return Brush("#E5C17B");

        return Brush("#E5A27F");
    }

    private static SolidColorBrush Brush(string colour)
    {
        return new SolidColorBrush(Color.Parse(colour));
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
