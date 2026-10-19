using Smart_X_API.Controllers;
using Smart_X_API.Models;

namespace Smart_X_API.Services;

public sealed class DeviceLookupService
{
    private Dictionary<string, SensorRegistration> _byNodeId =
        new(StringComparer.OrdinalIgnoreCase);

    private Dictionary<string, SensorRegistration> _byMac =
        new(StringComparer.OrdinalIgnoreCase);

    private int _indexedCount = -1;

    public void Refresh()
    {
        lock (SensorsController.SensorLock)
        {
            var byNodeId = new Dictionary<string, SensorRegistration>(
                StringComparer.OrdinalIgnoreCase);

            var byMac = new Dictionary<string, SensorRegistration>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var sensor in SensorsController.Sensors)
            {
                if (!string.IsNullOrWhiteSpace(sensor.NodeId))
                    byNodeId[sensor.NodeId.Trim()] = sensor;

                if (!string.IsNullOrWhiteSpace(sensor.MacAddress))
                    byMac[sensor.MacAddress.Trim()] = sensor;
            }

            _byNodeId = byNodeId;
            _byMac = byMac;
            _indexedCount = SensorsController.Sensors.Count;
        }
    }

    public SensorRegistration? FindByNodeId(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return null;

        lock (SensorsController.SensorLock)
        {
            EnsureIndexed();

            return _byNodeId.GetValueOrDefault(nodeId.Trim());
        }
    }

    public SensorRegistration? FindByMacAddress(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
            return null;

        lock (SensorsController.SensorLock)
        {
            EnsureIndexed();

            return _byMac.GetValueOrDefault(macAddress.Trim());
        }
    }

    public SensorRegistration? FindByIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return null;

        lock (SensorsController.SensorLock)
        {
            EnsureIndexed();

            var key = identifier.Trim();

            return _byNodeId.GetValueOrDefault(key)
                ?? _byMac.GetValueOrDefault(key);
        }
    }

    private void EnsureIndexed()
    {
        if (_indexedCount == SensorsController.Sensors.Count)
            return;

        var byNodeId = new Dictionary<string, SensorRegistration>(
            StringComparer.OrdinalIgnoreCase);

        var byMac = new Dictionary<string, SensorRegistration>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var sensor in SensorsController.Sensors)
        {
            if (!string.IsNullOrWhiteSpace(sensor.NodeId))
                byNodeId[sensor.NodeId.Trim()] = sensor;

            if (!string.IsNullOrWhiteSpace(sensor.MacAddress))
                byMac[sensor.MacAddress.Trim()] = sensor;
        }

        _byNodeId = byNodeId;
        _byMac = byMac;
        _indexedCount = SensorsController.Sensors.Count;
    }
}