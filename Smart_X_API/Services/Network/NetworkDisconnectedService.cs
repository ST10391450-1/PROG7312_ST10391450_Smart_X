using Smart_X_API.Controllers.Sensors;
using Smart_X_API.Models.Network;

namespace Smart_X_API.Services;

public interface INetworkDisconnectedService
{
    NetworkDisconnectedResponse GetDisconnectedNodes();
}

public sealed class NetworkDisconnectedService : INetworkDisconnectedService
{
    private readonly INetworkConnectionService _connectionService;

    public NetworkDisconnectedService(
        INetworkConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    public NetworkDisconnectedResponse GetDisconnectedNodes()
    {
        List<string> registeredNodeIds;

        lock (SensorsController.SensorLock)
        {
            registeredNodeIds = SensorsController.Sensors
                .Select(sensor => sensor.NodeId)
                .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var connections = _connectionService.GetConnections();

        var connectedNodeIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var connection in connections)
        {
            if (registeredNodeIds.Contains(
                connection.SourceNodeId,
                StringComparer.OrdinalIgnoreCase))
            {
                connectedNodeIds.Add(connection.SourceNodeId);
            }

            if (registeredNodeIds.Contains(
                connection.DestinationNodeId,
                StringComparer.OrdinalIgnoreCase))
            {
                connectedNodeIds.Add(connection.DestinationNodeId);
            }
        }

        var disconnectedNodeIds = registeredNodeIds
            .Where(nodeId => !connectedNodeIds.Contains(nodeId))
            .ToList();

        return new NetworkDisconnectedResponse
        {
            TotalNodes = registeredNodeIds.Count,
            ConnectedNodes = connectedNodeIds.Count,
            DisconnectedNodes = disconnectedNodeIds.Count,
            DisconnectedNodeIds = disconnectedNodeIds
        };
    }
}
