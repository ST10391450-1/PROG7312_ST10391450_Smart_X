using Smart_X_API.Models.Sensors;

namespace Smart_X_API.Models.Telemetry;

public sealed class TelemetryAvlNode
{
    public SensorReadingRecord Reading { get; set; }

    /// <summary>UTC search key the tree is ordered by.</summary>
    public DateTime Timestamp { get; }

    public long SequenceNumber { get; set; }

    public int Height { get; set; }

    public TelemetryAvlNode? Left { get; set; }

    public TelemetryAvlNode? Right { get; set; }

    public TelemetryAvlNode(
        SensorReadingRecord reading,
        long sequenceNumber,
        DateTime utcTimestamp)
    {
        Reading = reading;
        Timestamp = utcTimestamp;
        SequenceNumber = sequenceNumber;
        Height = 1;
    }
}
