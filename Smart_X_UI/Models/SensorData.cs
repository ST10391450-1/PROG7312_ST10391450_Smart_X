using System;

namespace Smart_X_UI.Models;

public class SensorRegistration
{
    public string MacAddress { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string NodeId { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{NodeId} — {Category} — {Location}";
    }
}

public class TelemetryPacket<T>
{
    public string DeviceId { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    public string SensorCategory { get; set; } = string.Empty;

    public T Value { get; set; } = default!;
}

public class SensorAttachment
{
    public Guid Id { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTime UploadedAt { get; set; }

    public string DisplaySize
    {
        get
        {
            if (FileSize < 1024)
            {
                return $"{FileSize} B";
            }

            if (FileSize < 1024 * 1024)
            {
                return $"{FileSize / 1024.0:F1} KB";
            }

            return $"{FileSize / (1024.0 * 1024.0):F1} MB";
        }
    }
}

