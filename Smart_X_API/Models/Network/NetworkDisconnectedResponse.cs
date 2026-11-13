namespace Smart_X_API.Models.Network;

public class NetworkDisconnectedResponse
{
    public int TotalNodes { get; set; }
    public int ConnectedNodes { get; set; }
    public int DisconnectedNodes { get; set; }
    public List<string> DisconnectedNodeIds { get; set; } = new();
}
