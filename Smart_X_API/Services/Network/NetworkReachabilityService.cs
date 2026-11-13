using Smart_X_API.Controllers.Sensors;
using Smart_X_API.Models.Network;

namespace Smart_X_API.Services;

public interface INetworkReachabilityService
{
    NetworkReachabilityResponse CheckReachability(
        string startNodeId,
        string destinationNodeId);
}

public sealed class NetworkReachabilityService : INetworkReachabilityService
{
    private readonly INetworkConnectionService _connectionService;

    public NetworkReachabilityService(
        INetworkConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    public NetworkReachabilityResponse CheckReachability(
        string startNodeId,
        string destinationNodeId)
    {
        var adjacencyList = BuildAdjacencyList();

        var startId = ValidateNode(startNodeId, adjacencyList);
        var destinationId = ValidateNode(
            destinationNodeId,
            adjacencyList);

        var queue = new Queue<string>();
        var visited = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var previousNodes = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        queue.Enqueue(startId);
        visited.Add(startId);

        while (queue.Count > 0)
        {
            var currentNode = queue.Dequeue();

            if (string.Equals(
                currentNode,
                destinationId,
                StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            foreach (var neighbour in adjacencyList[currentNode])
            {
                if (!visited.Add(neighbour))
                {
                    continue;
                }

                previousNodes[neighbour] = currentNode;
                queue.Enqueue(neighbour);
            }
        }

        if (!visited.Contains(destinationId))
        {
            return new NetworkReachabilityResponse
            {
                StartNodeId = startId,
                DestinationNodeId = destinationId,
                IsReachable = false,
                PathNodeIds = new List<string>(),
                HopCount = null
            };
        }

        var path = new List<string>();
        var pathNode = destinationId;

        path.Add(pathNode);

        while (!string.Equals(
            pathNode,
            startId,
            StringComparison.OrdinalIgnoreCase))
        {
            pathNode = previousNodes[pathNode];
            path.Add(pathNode);
        }

        path.Reverse();

        return new NetworkReachabilityResponse
        {
            StartNodeId = startId,
            DestinationNodeId = destinationId,
            IsReachable = true,
            PathNodeIds = path,
            HopCount = path.Count - 1
        };
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

    private static string ValidateNode(
        string nodeId,
        Dictionary<string, List<string>> adjacencyList)
    {
        return NetworkGraphAlgorithms.ResolveNode(adjacencyList, nodeId);
    }
}
