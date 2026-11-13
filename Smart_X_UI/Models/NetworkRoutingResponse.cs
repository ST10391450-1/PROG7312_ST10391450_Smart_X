
using System.Collections.Generic;

namespace Smart_X_UI.Models;

public class NetworkMstResponse
{
    public string Algorithm { get; set; } = string.Empty;

    public int TotalNodes { get; set; }

    public int ConnectionsConsidered { get; set; }

    public int SelectedEdgeCount { get; set; }

    public double TotalPowerCost { get; set; }

    public int ConnectedComponents { get; set; }

    public bool IsFullyConnected { get; set; }

    public List<NetworkMstEdge> Edges { get; set; } = new();
}

public class NetworkMstEdge
{
    public string SourceNodeId { get; set; } = string.Empty;

    public string DestinationNodeId { get; set; } = string.Empty;

    public double TransmissionPowerCost { get; set; }

    public double SignalStrength { get; set; }
}

public class NetworkShortestPathResponse
{
    public string StartNodeId { get; set; } = string.Empty;

    public string DestinationNodeId { get; set; } = string.Empty;

    public bool IsReachable { get; set; }

    public List<string> PathNodeIds { get; set; } = new();

    public int HopCount { get; set; }

    public double TotalPowerCost { get; set; }
}
