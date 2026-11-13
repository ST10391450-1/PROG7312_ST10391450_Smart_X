using Smart_X_API.Models.Sensors;
using Smart_X_API.Models.Telemetry;

namespace Smart_X_API.Services.Telemetry;

/// <summary>
/// Self-balancing (AVL) binary search tree of sensor readings keyed by
/// timestamp. Insert is O(log n); a range query is O(log n + k) where k is
/// the number of readings inside the range, because whole subtrees that
/// fall outside [start, end] are skipped.
/// </summary>
public sealed class TelemetryAvlTree
{
    private TelemetryAvlNode? _root;
    private long _sequenceNumber;
    private readonly object _lock = new();

    public int Count { get; private set; }

    /// <summary>Height of the tree (about 1.44 * log2(n) at worst).</summary>
    public int Height
    {
        get
        {
            lock (_lock)
            {
                return GetHeight(_root);
            }
        }
    }

    /// <summary>Treats unspecified timestamps as UTC, converts local to UTC.</summary>
    public static DateTime NormalizeUtc(DateTime timestamp)
    {
        return timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
        };
    }

    public void Insert(SensorReadingRecord reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        lock (_lock)
        {
            var sequenceNumber = ++_sequenceNumber;

            _root = InsertNode(
                _root,
                reading,
                NormalizeUtc(reading.Timestamp),
                sequenceNumber);

            Count++;
        }
    }

    public IReadOnlyList<SensorReadingRecord> GetRange(
        DateTime startTime,
        DateTime endTime,
        string? deviceId = null,
        string? sensorCategory = null)
    {
        if (startTime > endTime)
        {
            throw new ArgumentException(
                "Start time cannot be later than end time.");
        }

        startTime = NormalizeUtc(startTime);
        endTime = NormalizeUtc(endTime);

        lock (_lock)
        {
            var results = new List<SensorReadingRecord>();

            CollectRange(
                _root,
                startTime,
                endTime,
                deviceId,
                sensorCategory,
                results);

            return results;
        }
    }

    private TelemetryAvlNode InsertNode(
        TelemetryAvlNode? node,
        SensorReadingRecord reading,
        DateTime utcTimestamp,
        long sequenceNumber)
    {
        if (node == null)
        {
            return new TelemetryAvlNode(
                reading,
                sequenceNumber,
                utcTimestamp);
        }

        var comparison = Compare(
            utcTimestamp,
            sequenceNumber,
            node.Timestamp,
            node.SequenceNumber);

        if (comparison < 0)
        {
            node.Left = InsertNode(
                node.Left,
                reading,
                utcTimestamp,
                sequenceNumber);
        }
        else
        {
            node.Right = InsertNode(
                node.Right,
                reading,
                utcTimestamp,
                sequenceNumber);
        }

        UpdateHeight(node);

        return Balance(node);
    }

    private void CollectRange(
        TelemetryAvlNode? node,
        DateTime startTime,
        DateTime endTime,
        string? deviceId,
        string? sensorCategory,
        List<SensorReadingRecord> results)
    {
        if (node == null)
        {
            return;
        }

        var timestamp = node.Timestamp;

        if (timestamp >= startTime)
        {
            CollectRange(
                node.Left,
                startTime,
                endTime,
                deviceId,
                sensorCategory,
                results);
        }

        if (timestamp >= startTime && timestamp <= endTime)
        {
            var matchesDevice =
                string.IsNullOrWhiteSpace(deviceId) ||
                string.Equals(
                    node.Reading.DeviceId,
                    deviceId,
                    StringComparison.OrdinalIgnoreCase);

            var matchesCategory =
                string.IsNullOrWhiteSpace(sensorCategory) ||
                string.Equals(
                    node.Reading.SensorCategory,
                    sensorCategory,
                    StringComparison.OrdinalIgnoreCase);

            if (matchesDevice && matchesCategory)
            {
                results.Add(node.Reading);
            }
        }

        if (timestamp <= endTime)
        {
            CollectRange(
                node.Right,
                startTime,
                endTime,
                deviceId,
                sensorCategory,
                results);
        }
    }

    private TelemetryAvlNode Balance(TelemetryAvlNode node)
    {
        var balanceFactor =
            GetHeight(node.Left) - GetHeight(node.Right);

        if (balanceFactor > 1)
        {
            if (GetBalanceFactor(node.Left!) < 0)
            {
                node.Left = RotateLeft(node.Left!);
            }

            return RotateRight(node);
        }

        if (balanceFactor < -1)
        {
            if (GetBalanceFactor(node.Right!) > 0)
            {
                node.Right = RotateRight(node.Right!);
            }

            return RotateLeft(node);
        }

        return node;
    }

    private TelemetryAvlNode RotateRight(TelemetryAvlNode node)
    {
        var newRoot = node.Left!;
        var transferredSubtree = newRoot.Right;

        newRoot.Right = node;
        node.Left = transferredSubtree;

        UpdateHeight(node);
        UpdateHeight(newRoot);

        return newRoot;
    }

    private TelemetryAvlNode RotateLeft(TelemetryAvlNode node)
    {
        var newRoot = node.Right!;
        var transferredSubtree = newRoot.Left;

        newRoot.Left = node;
        node.Right = transferredSubtree;

        UpdateHeight(node);
        UpdateHeight(newRoot);

        return newRoot;
    }

    private static int Compare(
        DateTime firstTimestamp,
        long firstSequence,
        DateTime secondTimestamp,
        long secondSequence)
    {
        var timestampComparison =
            firstTimestamp.CompareTo(secondTimestamp);

        return timestampComparison != 0
            ? timestampComparison
            : firstSequence.CompareTo(secondSequence);
    }

    private static void UpdateHeight(TelemetryAvlNode node)
    {
        node.Height = 1 + Math.Max(
            GetHeight(node.Left),
            GetHeight(node.Right));
    }

    private static int GetHeight(TelemetryAvlNode? node)
    {
        return node?.Height ?? 0;
    }

    private static int GetBalanceFactor(TelemetryAvlNode node)
    {
        return GetHeight(node.Left) - GetHeight(node.Right);
    }
}
