// File attachement per sensor model

namespace Smart_X_API.Models;

public class SensorAttachment
{
    public Guid Id { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
}