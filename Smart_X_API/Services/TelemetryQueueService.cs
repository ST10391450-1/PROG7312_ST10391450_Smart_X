
namespace Smart_X_API.Services;

public sealed record TelemetryQueueItem(
    string DeviceId,
    string SensorCategory,
    double Value,
    string Unit,
    DateTime Timestamp);

public sealed record PriorityAlert(
    Guid Id,
    string DeviceId,
    string Message,
    int Priority,
    DateTime CreatedAt);

public sealed class TelemetryQueueService
{
    private readonly object _sync = new();
    private readonly Queue<TelemetryQueueItem> _telemetry = new();
    private readonly PriorityQueue<PriorityAlert, int> _alerts = new();
    private readonly int _capacity;

    public TelemetryQueueService(int capacity = 5000)
    {
        _capacity = Math.Max(100, capacity);
    }

    public void Enqueue(TelemetryQueueItem item)
    {
        lock (_sync)
        {
            while (_telemetry.Count >= _capacity)
                _telemetry.Dequeue();

            _telemetry.Enqueue(item);
        }
    }

    public IReadOnlyList<TelemetryQueueItem> GetRecent(int count = 100)
    {
        lock (_sync)
        {
            return _telemetry
                .Reverse()
                .Take(Math.Clamp(count, 1, _capacity))
                .Reverse()
                .ToList();
        }
    }

    public int Count
    {
        get
        {
            lock (_sync)
                return _telemetry.Count;
        }
    }

    public void EnqueueAlert(PriorityAlert alert)
    {
        lock (_sync)
        {
            _alerts.Enqueue(alert, -alert.Priority);
        }
    }

    public PriorityAlert? DequeueNextAlert()
    {
        lock (_sync)
        {
            return _alerts.TryDequeue(out var alert, out _)
                ? alert
                : null;
        }
    }
}
