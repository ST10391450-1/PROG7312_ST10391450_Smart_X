
using System.Threading.Channels;

namespace Smart_X_API.Services.Telemetry;

public sealed class TelemetryProcessingQueue
{
    private readonly Channel<TelemetryEvent> _queue =
        Channel.CreateBounded<TelemetryEvent>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });

    public ValueTask EnqueueAsync(
        TelemetryEvent telemetry,
        CancellationToken cancellationToken = default)
    {
        return _queue.Writer.WriteAsync(telemetry, cancellationToken);
    }

    public IAsyncEnumerable<TelemetryEvent> ReadAllAsync(
        CancellationToken cancellationToken)
    {
        return _queue.Reader.ReadAllAsync(cancellationToken);
    }
}

public sealed record TelemetryEvent(
    string DeviceId,
    string SensorCategory,
    double Value,
    DateTime Timestamp);