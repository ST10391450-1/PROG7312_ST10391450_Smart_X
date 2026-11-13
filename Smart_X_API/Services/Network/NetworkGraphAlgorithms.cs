using Smart_X_API.Models;

namespace Smart_X_API.Services;

/// <summary>
/// Shared graph building blocks used by every network service.
/// The mesh is modelled as an UNDIRECTED graph: a recorded connection
/// A-B allows traffic in both directions, so it is stored as two
/// adjacency entries (A -> B and B -> A).
/// </summary>
public static class NetworkGraphAlgorithms
{
    #region Graph Construction

    public static Dictionary<string, List<string>> BuildUndirectedAdjacency(
        IEnumerable<string> nodeIds,
        IEnumerable<(string Source, string Destination)> links)
    {
        var canonical = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var nodeId in nodeIds)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                continue;

            canonical.TryAdd(nodeId.Trim(), nodeId.Trim());
        }

        var adjacency = canonical.Values.ToDictionary(
            nodeId => nodeId,
            _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var (source, destination) in links)
        {
            if (string.IsNullOrWhiteSpace(source) ||
                string.IsNullOrWhiteSpace(destination))
                continue;

            if (!canonical.TryGetValue(source.Trim(), out var from) ||
                !canonical.TryGetValue(destination.Trim(), out var to))
                continue;

            if (from.Equals(to, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!adjacency[from].Contains(to, StringComparer.OrdinalIgnoreCase))
                adjacency[from].Add(to);

            if (!adjacency[to].Contains(from, StringComparer.OrdinalIgnoreCase))
                adjacency[to].Add(from);
        }

        // Sorted neighbours make every traversal deterministic.
        foreach (var neighbours in adjacency.Values)
            neighbours.Sort(StringComparer.OrdinalIgnoreCase);

        return adjacency;
    }

    public static string ResolveNode(
        Dictionary<string, List<string>> adjacency,
        string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ArgumentException("A node ID is required.");

        var trimmed = nodeId.Trim();

        var match = adjacency.Keys.FirstOrDefault(key =>
            key.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

        return match ?? throw new KeyNotFoundException(
            $"Node '{trimmed}' is not registered.");
    }

    #endregion

    #region Traversals  (all O(V + E))

    public static List<string> BreadthFirstSearch(
        Dictionary<string, List<string>> adjacency,
        string startNodeId)
    {
        var start = ResolveNode(adjacency, startNodeId);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start };
        var queue = new Queue<string>();
        var order = new List<string>();

        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            order.Add(current);

            foreach (var neighbour in adjacency[current])
            {
                if (visited.Add(neighbour))
                    queue.Enqueue(neighbour);
            }
        }

        return order;
    }

    public static List<string> DepthFirstSearch(
        Dictionary<string, List<string>> adjacency,
        string startNodeId)
    {
        var start = ResolveNode(adjacency, startNodeId);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        var order = new List<string>();

        stack.Push(start);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (!visited.Add(current))
                continue;

            order.Add(current);

            // Pushed in reverse so the alphabetically first neighbour
            // is explored first.
            var neighbours = adjacency[current];

            for (var i = neighbours.Count - 1; i >= 0; i--)
            {
                if (!visited.Contains(neighbours[i]))
                    stack.Push(neighbours[i]);
            }
        }

        return order;
    }

    public static Dictionary<string, int> HopCounts(
        Dictionary<string, List<string>> adjacency,
        string startNodeId)
    {
        var start = ResolveNode(adjacency, startNodeId);

        var hops = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [start] = 0
        };

        var queue = new Queue<string>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var neighbour in adjacency[current])
            {
                if (hops.ContainsKey(neighbour))
                    continue;

                hops[neighbour] = hops[current] + 1;
                queue.Enqueue(neighbour);
            }
        }

        return hops;
    }

    #endregion
}
