using Smart_X_API.Models.Sensors;

namespace Smart_X_API.Models.Telemetry;

public class TelemetryRangeResponse
{
    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? DeviceId { get; set; }

    public string? SensorCategory { get; set; }

    /// <summary>Total readings stored in the AVL tree.</summary>
    public int TotalStoredReadings { get; set; }

    /// <summary>Height of the AVL tree (proves it stays balanced).</summary>
    public int TreeHeight { get; set; }

    public int MatchCount { get; set; }

    public int ReturnedCount { get; set; }

    public double ElapsedMilliseconds { get; set; }

    public List<SensorReadingRecord> Readings { get; set; } = new();
}
