using Smart_X_API.Controllers.Sensors;
using Smart_X_API.Models.Network;
using Smart_X_API.Models.Sensors;
using Smart_X_API.Services.Telemetry;

namespace Smart_X_API.Services;

public interface INetworkTopologyService
{
    NetworkTopologyResponse GetTopology();
}

public sealed class NetworkTopologyService : INetworkTopologyService
{
    private static readonly TimeSpan OfflineTimeout =
        TimeSpan.FromMinutes(2);

    private readonly TelemetryService _telemetryService;
    private readonly INetworkConnectionService _connectionService;

    public NetworkTopologyService(
        TelemetryService telemetryService,
        INetworkConnectionService connectionService)
    {
        _telemetryService = telemetryService;
        _connectionService = connectionService;
    }

    public NetworkTopologyResponse GetTopology()
    {
        List<SensorRegistration> sensors;

        lock (SensorsController.SensorLock)
        {
            sensors = SensorsController.Sensors
                .Select(sensor => new SensorRegistration
                {
                    NodeId = sensor.NodeId,
                    MacAddress = sensor.MacAddress,
                    Category = sensor.Category,
                    Location = sensor.Location,
                    IsEnabled = sensor.IsEnabled
                })
                .ToList();
        }

        var readings = _telemetryService.GetReadings();

        var latestReadings = readings
            .Where(reading =>
                !string.IsNullOrWhiteSpace(reading.DeviceId))
            .GroupBy(
                reading => reading.DeviceId.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(reading => NormalizeTimestamp(reading.Timestamp))
                    .Max(),
                StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var cutoff = now - OfflineTimeout;

        var nodes = sensors.Select(sensor =>
        {
            DateTime? lastSeenAt = null;

            if (latestReadings.TryGetValue(
                sensor.NodeId,
                out var nodeTimestamp))
            {
                lastSeenAt = nodeTimestamp;
            }
            else if (latestReadings.TryGetValue(
                sensor.MacAddress,
                out var macTimestamp))
            {
                lastSeenAt = macTimestamp;
            }

            bool isOnline = false;

            if (sensor.IsEnabled && lastSeenAt.HasValue)
            {
                isOnline =
                    lastSeenAt.Value >= cutoff &&
                    lastSeenAt.Value <= now;
            }

            return new NetworkNodeResponse
            {
                NodeId = sensor.NodeId,
                Name = sensor.Category,
                MacAddress = sensor.MacAddress,
                IpAddress = null,
                Location = sensor.Location,
                IsEnabled = sensor.IsEnabled,
                IsOnline = isOnline,
                LastSeenAt = lastSeenAt
            };
        }).ToList();

        var registeredNodeIds = nodes
            .Select(node => node.NodeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var connections = _connectionService
            .GetConnections()
            .Where(connection =>
                registeredNodeIds.Contains(connection.SourceNodeId) &&
                registeredNodeIds.Contains(connection.DestinationNodeId))
            .Select(connection => new NetworkConnectionResponse
            {
                SourceNodeId = connection.SourceNodeId,
                DestinationNodeId = connection.DestinationNodeId,
                SignalStrength = connection.SignalStrength,
                TransmissionPowerCost = connection.TransmissionPowerCost
            })
            .ToList();

        var onlineNodes = nodes.Count(
            node => node.IsOnline == true);

        var offlineNodes = nodes.Count(
            node => node.IsOnline == false);

        return new NetworkTopologyResponse
        {
            GeneratedAt = now,
            TotalNodes = nodes.Count,
            OnlineNodes = onlineNodes,
            OfflineNodes = offlineNodes,
            Nodes = nodes,
            Connections = connections
        };
    }

    private static DateTime NormalizeTimestamp(DateTime timestamp)
    {
        return timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
        };
    }
}
