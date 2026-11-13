namespace Smart_X_API.Models.Telemetry;

public class TelemetryHistoryRequest
{
    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? DeviceId { get; set; }

    public string? SensorCategory { get; set; }

    public int? Limit { get; set; }
}
