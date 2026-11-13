namespace Smart_X_API.Models.Network;

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

    public string? IpAddress { get; set; }

    public string? Location { get; set; }

    public bool IsEnabled { get; set; }

    public bool? IsOnline { get; set; }

    public DateTime? LastSeenAt { get; set; }
}

public class NetworkConnectionResponse
{
    public string SourceNodeId { get; set; } = string.Empty;

    public string DestinationNodeId { get; set; } = string.Empty;

    public double SignalStrength { get; set; }

    public double TransmissionPowerCost { get; set; }
}
