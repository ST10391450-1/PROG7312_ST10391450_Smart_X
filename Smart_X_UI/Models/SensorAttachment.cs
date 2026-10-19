
using System;

namespace Smart_X_UI.Models;

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
                return $"{FileSize} B";

            if (FileSize < 1024 * 1024)
                return $"{FileSize / 1024.0:F1} KB";

            return $"{FileSize / (1024.0 * 1024.0):F1} MB";
        }
    }
}
