using Smart_X_API.Models.Sensors;

namespace Smart_X_API.Models.Telemetry;

public class TelemetryHistoryResponse
{
    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int TotalReadings { get; set; }

    public string? DeviceId { get; set; }

    public string? SensorCategory { get; set; }

    public List<SensorReadingRecord> Readings { get; set; } = new();
}
