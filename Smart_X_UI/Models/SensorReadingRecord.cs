
using System;

namespace Smart_X_UI.Models;

public class SensorReadingRecord
{
    public string DeviceId { get; set; } = string.Empty;

    public string SensorCategory { get; set; } = string.Empty;

    public double Value { get; set; }

    public DateTime Timestamp { get; set; }
}
 