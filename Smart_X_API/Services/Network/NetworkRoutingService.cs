
using Smart_X_API.Controllers.Sensors;
using Smart_X_API.Models.Network;

namespace Smart_X_API.Services;

public interface INetworkRoutingService
{
    NetworkMstResponse CalculateMinimumSpanningTree();

    NetworkShortestPathResponse FindMostEfficientPath(
        string startNodeId,
        string destinationNodeId);
}

public sealed class NetworkRoutingService : INetworkRoutingService
{
    private readonly INetworkConnectionService _connectionService;

    public NetworkRoutingService(INetworkConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    private sealed record Edge(
        string A,
        string B,
        double Cost,
        double SignalStrength);

    public NetworkMstResponse CalculateMinimumSpanningTree()
    {
        var nodes = GetNodes();
        var edges = GetEdges(nodes)
            .OrderBy(edge => edge.Cost)
            .ThenByDescending(edge => edge.SignalStrength)
            .ToList();

        var disjointSet = new DisjointSet(nodes);
        var selectedEdges = new List<Edge>();

        foreach (var edge in edges)
        {
            if (disjointSet.Union(edge.A, edge.B))
            {
                selectedEdges.Add(edge);
            }
        }

        var totalPowerCost = selectedEdges.Sum(edge => edge.Cost);

        return new NetworkMstResponse
        {
            Algorithm = "Kruskal",
            TotalNodes = nodes.Count,
            ConnectionsConsidered = edges.Count,
            SelectedEdgeCount = selectedEdges.Count,
            TotalPowerCost = totalPowerCost,
            ConnectedComponents = disjointSet.ComponentCount,
            IsFullyConnected = disjointSet.ComponentCount <= 1,
            Edges = selectedEdges.Select(edge => new NetworkMstEdge
            {
                SourceNodeId = edge.A,
                DestinationNodeId = edge.B,
                TransmissionPowerCost = edge.Cost,
                SignalStrength = edge.SignalStrength
            }).ToList()
        };
    }

    public NetworkShortestPathResponse FindMostEfficientPath(
        string startNodeId,
        string destinationNodeId)
    {
        var nodes = GetNodes();
        var edges = GetEdges(nodes);

        var start = ResolveNode(nodes, startNodeId);
        var destination = ResolveNode(nodes, destinationNodeId);

        var neighbours = nodes.ToDictionary(
            node => node,
            _ => new List<(string Node, double Cost)>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var edge in edges)
        {
            neighbours[edge.A].Add((edge.B, edge.Cost));
            neighbours[edge.B].Add((edge.A, edge.Cost));
        }

        var distances = nodes.ToDictionary(
            node => node,
            _ => double.PositiveInfinity,
            StringComparer.OrdinalIgnoreCase);

        var previousNodes = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        var queue = new PriorityQueue<string, double>();

        distances[start] = 0;
        queue.Enqueue(start, 0);

        while (queue.TryDequeue(out var current, out var currentCost))
        {
            if (currentCost > distances[current])
            {
                continue;
            }

            if (current.Equals(destination, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            foreach (var neighbour in neighbours[current])
            {
                var newCost = currentCost + neighbour.Cost;

                if (newCost >= distances[neighbour.Node])
                {
                    continue;
                }

                distances[neighbour.Node] = newCost;
                previousNodes[neighbour.Node] = current;
                queue.Enqueue(neighbour.Node, newCost);
            }
        }

        if (double.IsPositiveInfinity(distances[destination]))
        {
            return new NetworkShortestPathResponse
            {
                StartNodeId = start,
                DestinationNodeId = destination,
                IsReachable = false,
                PathNodeIds = [],
                HopCount = 0,
                TotalPowerCost = 0
            };
        }

        var path = new List<string>();
        var currentNode = destination;

        while (true)
        {
            path.Add(currentNode);

            if (currentNode.Equals(start, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            currentNode = previousNodes[currentNode];
        }

        path.Reverse();

        return new NetworkShortestPathResponse
        {
            StartNodeId = start,
            DestinationNodeId = destination,
            IsReachable = true,
            PathNodeIds = path,
            HopCount = path.Count - 1,
            TotalPowerCost = distances[destination]
        };
    }

    private static List<string> GetNodes()
    {
        lock (SensorsController.SensorLock)
        {
            return SensorsController.Sensors
                .Select(sensor => sensor.NodeId?.Trim())
                .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(nodeId => nodeId!)
                .ToList();
        }
    }

    private List<Edge> GetEdges(List<string> nodes)
    {
        var registeredNodes = new HashSet<string>(
            nodes,
            StringComparer.OrdinalIgnoreCase);

        var uniqueEdges = new Dictionary<string, Edge>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var connection in _connectionService.GetConnections())
        {
            var source = connection.SourceNodeId?.Trim();
            var destination = connection.DestinationNodeId?.Trim();

            if (string.IsNullOrWhiteSpace(source) ||
                string.IsNullOrWhiteSpace(destination))
            {
                continue;
            }

            if (!registeredNodes.Contains(source) ||
                !registeredNodes.Contains(destination))
            {
                continue;
            }

            if (source.Equals(destination, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var first = string.Compare(
                source,
                destination,
                StringComparison.OrdinalIgnoreCase) <= 0
                ? source
                : destination;

            var second = first == source ? destination : source;
            var key = $"{first}|{second}";

            var edge = new Edge(
                first,
                second,
                connection.TransmissionPowerCost,
                connection.SignalStrength);

            if (!uniqueEdges.TryGetValue(key, out var existing) ||
                edge.Cost < existing.Cost ||
                (edge.Cost == existing.Cost &&
                 edge.SignalStrength > existing.SignalStrength))
            {
                uniqueEdges[key] = edge;
            }
        }

        return uniqueEdges.Values.ToList();
    }

    private static string ResolveNode(List<string> nodes, string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Node ID cannot be empty.", nameof(nodeId));
        }

        return nodes.FirstOrDefault(node =>
                   node.Equals(nodeId.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? throw new KeyNotFoundException(
                   $"Node '{nodeId}' was not found.");
    }

    private sealed class DisjointSet
    {
        private readonly Dictionary<string, string> _parents;
        private readonly Dictionary<string, int> _ranks;

        public int ComponentCount { get; private set; }

        public DisjointSet(IEnumerable<string> nodes)
        {
            _parents = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            _ranks = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var node in nodes)
            {
                _parents[node] = node;
                _ranks[node] = 0;
            }

            ComponentCount = _parents.Count;
        }

        private string Find(string node)
        {
            if (!_parents.TryGetValue(node, out var parent))
            {
                throw new KeyNotFoundException($"Node '{node}' was not found.");
            }

            if (!parent.Equals(node, StringComparison.OrdinalIgnoreCase))
            {
                _parents[node] = Find(parent);
            }

            return _parents[node];
        }

        public bool Union(string a, string b)
        {
            var rootA = Find(a);
            var rootB = Find(b);

            if (rootA.Equals(rootB, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (_ranks[rootA] < _ranks[rootB])
            {
                _parents[rootA] = rootB;
            }
            else if (_ranks[rootA] > _ranks[rootB])
            {
                _parents[rootB] = rootA;
            }
            else
            {
                _parents[rootB] = rootA;
                _ranks[rootA]++;
            }

            ComponentCount--;
            return true;
        }
    }
}
