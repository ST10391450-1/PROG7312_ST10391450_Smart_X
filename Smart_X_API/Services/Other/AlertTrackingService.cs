
namespace Smart_X_API.Services;

public sealed class AlertTrackingService
{
    private readonly object _sync = new();
    private readonly HashSet<string> _activeAlerts =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _disconnectedDevices =
        new(StringComparer.OrdinalIgnoreCase);

    public bool TryActivateAlert(string deviceId, string alertType)
    {
        var key = $"{deviceId.Trim()}|{alertType.Trim()}";

        lock (_sync)
            return _activeAlerts.Add(key);
    }

    public bool ResolveAlert(string deviceId, string alertType)
    {
        var key = $"{deviceId.Trim()}|{alertType.Trim()}";

        lock (_sync)
            return _activeAlerts.Remove(key);
    }

    public void MarkDisconnected(string deviceId)
    {
        lock (_sync)
            _disconnectedDevices.Add(deviceId.Trim());
    }

    public void MarkConnected(string deviceId)
    {
        lock (_sync)
            _disconnectedDevices.Remove(deviceId.Trim());
    }

    public IReadOnlyCollection<string> GetActiveAlerts()
    {
        lock (_sync)
            return _activeAlerts.ToArray();
    }

    public IReadOnlyCollection<string> GetDisconnectedDevices()
    {
        lock (_sync)
            return _disconnectedDevices.ToArray();
    }
}
