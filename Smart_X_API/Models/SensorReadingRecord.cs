
// Recording of stored sensor readigns

namespace Smart_X_API.Models;

public class SensorReadingRecord
{
    public string DeviceId { get; set; } = string.Empty;
    public string SensorCategory { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTime Timestamp { get; set; }
}