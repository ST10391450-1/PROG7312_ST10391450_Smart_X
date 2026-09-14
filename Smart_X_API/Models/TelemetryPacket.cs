// API model for telemetry packets.
namespace Smart_X_API.Models;

public class TelemetryPacket<T>
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string SensorCategory { get; set; } = string.Empty;
    public T Value { get; set; } = default!;
}