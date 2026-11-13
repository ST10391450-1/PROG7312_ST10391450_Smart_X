namespace Smart_X_API.Models.Network;

public class NetworkTraversalResponse
{
    public string Algorithm { get; set; } = string.Empty;
    public string StartNodeId { get; set; } = string.Empty;
    public List<string> VisitedNodeIds { get; set; } = new();
    public int VisitedCount { get; set; }
    public bool IsComplete { get; set; }
}
