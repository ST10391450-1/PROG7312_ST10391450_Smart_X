
using Smart_X_API.Controllers;
using Smart_X_API.Models;

namespace Smart_X_API.Services;

public sealed class DeviceLookupService
{
    private readonly object _sync = new();
    private Dictionary<string, SensorRegistration> _byNodeId =
        new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, SensorRegistration> _byMac =
        new(StringComparer.OrdinalIgnoreCase);
    private int _indexedCount = -1;

    public void Refresh()
    {
        lock (_sync)
        {
            _byNodeId = new Dictionary<string, SensorRegistration>(
                SensorsController.Sensors.ToDictionary(
                    x => x.NodeId,
                    StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

            _byMac = new Dictionary<string, SensorRegistration>(
                SensorsController.Sensors.ToDictionary(
                    x => x.MacAddress,
                    StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

            _indexedCount = SensorsController.Sensors.Count;
        }
    }

    public SensorRegistration? FindByNodeId(string nodeId)
    {
        EnsureIndexed();

        lock (_sync)
        {
            return _byNodeId.GetValueOrDefault(nodeId.Trim());
        }
    }

    public SensorRegistration? FindByMacAddress(string macAddress)
    {
        EnsureIndexed();

        lock (_sync)
        {
            return _byMac.GetValueOrDefault(macAddress.Trim());
        }
    }

    private void EnsureIndexed()
    {
        lock (_sync)
        {
            if (_indexedCount == SensorsController.Sensors.Count)
                return;
        }

        Refresh();
    }
}
