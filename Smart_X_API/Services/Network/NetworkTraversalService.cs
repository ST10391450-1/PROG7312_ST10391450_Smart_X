using Smart_X_API.Controllers.Sensors;
using Smart_X_API.Models.Network;

namespace Smart_X_API.Services;

public interface INetworkTraversalService
{
    NetworkTraversalResponse BreadthFirstSearch(string startNodeId);
    NetworkTraversalResponse DepthFirstSearch(string startNodeId);
}

public sealed class NetworkTraversalService : INetworkTraversalService
{
    private readonly INetworkConnectionService _connectionService;

    public NetworkTraversalService(
        INetworkConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    public NetworkTraversalResponse BreadthFirstSearch(string startNodeId)
    {
        var adjacencyList = BuildAdjacencyList();

        var order = NetworkGraphAlgorithms.BreadthFirstSearch(
            adjacencyList,
            startNodeId);

        return CreateResponse(
            "Breadth-First Search",
            order[0],
            order,
            adjacencyList.Count);
    }

    public NetworkTraversalResponse DepthFirstSearch(string startNodeId)
    {
        var adjacencyList = BuildAdjacencyList();

        var order = NetworkGraphAlgorithms.DepthFirstSearch(
            adjacencyList,
            startNodeId);

        return CreateResponse(
            "Depth-First Search",
            order[0],
            order,
            adjacencyList.Count);
    }

    private Dictionary<string, List<string>> BuildAdjacencyList()
    {
        List<string> registeredNodeIds;

        lock (SensorsController.SensorLock)
        {
            registeredNodeIds = SensorsController.Sensors
                .Select(sensor => sensor.NodeId)
                .ToList();
        }

        return NetworkGraphAlgorithms.BuildUndirectedAdjacency(
            registeredNodeIds,
            _connectionService.GetConnections().Select(connection =>
                (connection.SourceNodeId, connection.DestinationNodeId)));
    }

    private static NetworkTraversalResponse CreateResponse(
        string algorithm,
        string startNodeId,
        List<string> traversalOrder,
        int totalNodes)
    {
        return new NetworkTraversalResponse
        {
            Algorithm = algorithm,
            StartNodeId = startNodeId,
            VisitedNodeIds = traversalOrder,
            VisitedCount = traversalOrder.Count,
            IsComplete = traversalOrder.Count == totalNodes
        };
    }
}
