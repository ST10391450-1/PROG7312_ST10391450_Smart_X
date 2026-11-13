using Smart_X_API.Controllers.Sensors;
using Smart_X_API.Models.Network;

namespace Smart_X_API.Services;

public interface INetworkConnectionService
{
    NetworkConnection AddConnection(NetworkConnection connection);

    bool RemoveConnection(string sourceNodeId, string destinationNodeId);

    NetworkConnection? GetConnection(
        string sourceNodeId,
        string destinationNodeId);

    IReadOnlyList<NetworkConnection> GetConnections();

    IReadOnlyList<NetworkConnection> GetConnections(string nodeId);
}

public sealed class NetworkConnectionService : INetworkConnectionService
{
    private readonly List<NetworkConnection> _connections = new();
    private readonly object _sync = new();

    public NetworkConnection AddConnection(NetworkConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var sourceNodeId = connection.SourceNodeId?.Trim();
        var destinationNodeId = connection.DestinationNodeId?.Trim();

        if (string.IsNullOrWhiteSpace(sourceNodeId))
            throw new ArgumentException("Source node ID is required.");

        if (string.IsNullOrWhiteSpace(destinationNodeId))
            throw new ArgumentException("Destination node ID is required.");

        if (sourceNodeId.Equals(
                destinationNodeId,
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A device cannot connect to itself.");

        if (!double.IsFinite(connection.SignalStrength) ||
            connection.SignalStrength < 0 ||
            connection.SignalStrength > 100)
            throw new ArgumentException(
                "Signal strength must be between 0 and 100.");

        if (!double.IsFinite(connection.TransmissionPowerCost) ||
            connection.TransmissionPowerCost < 0)
            throw new ArgumentException(
                "Transmission power cost must be a non-negative finite number.");

        lock (SensorsController.SensorLock)
        {
            var source = SensorsController.Sensors.FirstOrDefault(sensor =>
                sensor.NodeId.Equals(
                    sourceNodeId,
                    StringComparison.OrdinalIgnoreCase));

            var destination = SensorsController.Sensors.FirstOrDefault(sensor =>
                sensor.NodeId.Equals(
                    destinationNodeId,
                    StringComparison.OrdinalIgnoreCase));

            if (source == null)
                throw new KeyNotFoundException(
                    $"Source device '{sourceNodeId}' is not registered.");

            if (destination == null)
                throw new KeyNotFoundException(
                    $"Destination device '{destinationNodeId}' is not registered.");

            if (!source.IsEnabled || !destination.IsEnabled)
                throw new InvalidOperationException(
                    "Connections can only be created between enabled devices.");
        }

        lock (_sync)
        {
            var existingConnection = _connections.FirstOrDefault(existing =>
                (existing.SourceNodeId.Equals(
                    sourceNodeId,
                    StringComparison.OrdinalIgnoreCase) &&
                 existing.DestinationNodeId.Equals(
                    destinationNodeId,
                    StringComparison.OrdinalIgnoreCase)) ||
                (existing.SourceNodeId.Equals(
                    destinationNodeId,
                    StringComparison.OrdinalIgnoreCase) &&
                 existing.DestinationNodeId.Equals(
                    sourceNodeId,
                    StringComparison.OrdinalIgnoreCase)));

            if (existingConnection != null)
            {
                existingConnection.SignalStrength =
                    connection.SignalStrength;

                existingConnection.TransmissionPowerCost =
                    connection.TransmissionPowerCost;

                existingConnection.LastUpdated = DateTime.UtcNow;

                return CreateConnectionSnapshot(existingConnection);
            }

            var newConnection = new NetworkConnection
            {
                SourceNodeId = sourceNodeId,
                DestinationNodeId = destinationNodeId,
                SignalStrength = connection.SignalStrength,
                TransmissionPowerCost = connection.TransmissionPowerCost,
                LastUpdated = DateTime.UtcNow
            };

            _connections.Add(newConnection);

            return CreateConnectionSnapshot(newConnection);
        }
    }

    public bool RemoveConnection(
        string sourceNodeId,
        string destinationNodeId)
    {
        if (string.IsNullOrWhiteSpace(sourceNodeId) ||
            string.IsNullOrWhiteSpace(destinationNodeId))
            return false;

        sourceNodeId = sourceNodeId.Trim();
        destinationNodeId = destinationNodeId.Trim();

        lock (_sync)
        {
            var connection = _connections.FirstOrDefault(existing =>
                (existing.SourceNodeId.Equals(
                    sourceNodeId,
                    StringComparison.OrdinalIgnoreCase) &&
                 existing.DestinationNodeId.Equals(
                    destinationNodeId,
                    StringComparison.OrdinalIgnoreCase)) ||
                (existing.SourceNodeId.Equals(
                    destinationNodeId,
                    StringComparison.OrdinalIgnoreCase) &&
                 existing.DestinationNodeId.Equals(
                    sourceNodeId,
                    StringComparison.OrdinalIgnoreCase)));

            return connection != null &&
                   _connections.Remove(connection);
        }
    }

    public NetworkConnection? GetConnection(
        string sourceNodeId,
        string destinationNodeId)
    {
        if (string.IsNullOrWhiteSpace(sourceNodeId) ||
            string.IsNullOrWhiteSpace(destinationNodeId))
            return null;

        sourceNodeId = sourceNodeId.Trim();
        destinationNodeId = destinationNodeId.Trim();

        lock (_sync)
        {
            var connection = _connections.FirstOrDefault(existing =>
                (existing.SourceNodeId.Equals(
                    sourceNodeId,
                    StringComparison.OrdinalIgnoreCase) &&
                 existing.DestinationNodeId.Equals(
                    destinationNodeId,
                    StringComparison.OrdinalIgnoreCase)) ||
                (existing.SourceNodeId.Equals(
                    destinationNodeId,
                    StringComparison.OrdinalIgnoreCase) &&
                 existing.DestinationNodeId.Equals(
                    sourceNodeId,
                    StringComparison.OrdinalIgnoreCase)));

            return connection == null
                ? null
                : CreateConnectionSnapshot(connection);
        }
    }

    public IReadOnlyList<NetworkConnection> GetConnections()
    {
        lock (_sync)
        {
            return _connections
                .Select(CreateConnectionSnapshot)
                .ToList();
        }
    }

    public IReadOnlyList<NetworkConnection> GetConnections(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return Array.Empty<NetworkConnection>();

        nodeId = nodeId.Trim();

        lock (_sync)
        {
            return _connections
                .Where(connection =>
                    connection.SourceNodeId.Equals(
                        nodeId,
                        StringComparison.OrdinalIgnoreCase) ||
                    connection.DestinationNodeId.Equals(
                        nodeId,
                        StringComparison.OrdinalIgnoreCase))
                .Select(CreateConnectionSnapshot)
                .ToList();
        }
    }

    private static NetworkConnection CreateConnectionSnapshot(
        NetworkConnection connection)
    {
        return new NetworkConnection
        {
            SourceNodeId = connection.SourceNodeId,
            DestinationNodeId = connection.DestinationNodeId,
            SignalStrength = connection.SignalStrength,
            TransmissionPowerCost = connection.TransmissionPowerCost,
            LastUpdated = connection.LastUpdated
        };
    }
}