
namespace Smart_X_API.Services.Telemetry;

public sealed class AlertDeduplicationService
{
    private readonly HashSet<string> _activeAlerts = new();
    private readonly object _lock = new();

    public bool TryRegister(string sensorId, string alertType)
    {
        string alertKey = $"{sensorId}:{alertType}";

        lock (_lock)
        {
            return _activeAlerts.Add(alertKey);
        }
    }

    public void Resolve(string sensorId, string alertType)
    {
        string alertKey = $"{sensorId}:{alertType}";

        lock (_lock)
        {
            _activeAlerts.Remove(alertKey);
        }
    }
}
