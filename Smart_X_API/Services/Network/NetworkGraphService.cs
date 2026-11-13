namespace Smart_X_API.Services;

public interface INetworkGraphService
{
    IReadOnlyList<string> BreadthFirstSearch(string startNodeId);

    IReadOnlyList<string> DepthFirstSearch(string startNodeId);

    IReadOnlyList<string> GetReachableNodes(string startNodeId);

    IReadOnlyList<string> GetDisconnectedNodes(string startNodeId);

    IReadOnlyDictionary<string, int> GetHopCounts(string startNodeId);
}

/// <summary>
/// Graph operations over the live network topology. The algorithms live in
/// <see cref="NetworkGraphAlgorithms"/> so they are written exactly once.
/// </summary>
public sealed class NetworkGraphService : INetworkGraphService
{
    private readonly INetworkTopologyService _topologyService;

    public NetworkGraphService(
        INetworkTopologyService topologyService)
    {
        _topologyService = topologyService;
    }

    private Dictionary<string, List<string>> BuildGraph()
    {
        var topology = _topologyService.GetTopology();

        return NetworkGraphAlgorithms.BuildUndirectedAdjacency(
            topology.Nodes.Select(node => node.NodeId),
            topology.Connections.Select(connection =>
                (connection.SourceNodeId, connection.DestinationNodeId)));
    }

    public IReadOnlyList<string> BreadthFirstSearch(string startNodeId)
    {
        return NetworkGraphAlgorithms.BreadthFirstSearch(
            BuildGraph(),
            startNodeId);
    }

    public IReadOnlyList<string> DepthFirstSearch(string startNodeId)
    {
        return NetworkGraphAlgorithms.DepthFirstSearch(
            BuildGraph(),
            startNodeId);
    }

    public IReadOnlyList<string> GetReachableNodes(string startNodeId)
    {
        return BreadthFirstSearch(startNodeId);
    }

    public IReadOnlyList<string> GetDisconnectedNodes(string startNodeId)
    {
        var graph = BuildGraph();

        var reachable = new HashSet<string>(
            NetworkGraphAlgorithms.BreadthFirstSearch(graph, startNodeId),
            StringComparer.OrdinalIgnoreCase);

        return graph.Keys
            .Where(nodeId => !reachable.Contains(nodeId))
            .OrderBy(nodeId => nodeId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyDictionary<string, int> GetHopCounts(string startNodeId)
    {
        return NetworkGraphAlgorithms.HopCounts(
            BuildGraph(),
            startNodeId);
    }
}
