namespace Smart_X_API.Models;

public class TelemetryBatch
{
    public string DeviceId { get; set; } = string.Empty;
    public string SensorCategory { get; set; } = string.Empty;
    public double[,] RawValues { get; set; } = new double[0, 0];
}
