namespace Smart_X_API.Models.Network;

public class NetworkConnection
{
    public string SourceNodeId { get; set; } = string.Empty;

    public string DestinationNodeId { get; set; } = string.Empty;

    public double SignalStrength { get; set; }

    public double TransmissionPowerCost { get; set; }

    public DateTime LastUpdated { get; set; }
}
