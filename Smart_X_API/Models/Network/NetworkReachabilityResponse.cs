namespace Smart_X_API.Models.Network;

public class NetworkReachabilityResponse
{
    public string StartNodeId { get; set; } = string.Empty;
    public string DestinationNodeId { get; set; } = string.Empty;
    public bool IsReachable { get; set; }
    public List<string> PathNodeIds { get; set; } = new();
    public int? HopCount { get; set; }
}
