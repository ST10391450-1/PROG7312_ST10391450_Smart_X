
namespace Smart_X_API.Models;

public sealed class DeviceRecommendation
{
    public string DeviceId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int Score { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
