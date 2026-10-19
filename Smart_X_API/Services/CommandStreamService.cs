
using Smart_X_API.Models;

namespace Smart_X_API.Services;

public class CommandStreamService
{
    private readonly TelemetryService _telemetryService;
    private readonly List<CommandRecord> _commands = new();
    private readonly object _lock = new();
    private int _nextId = 1;

    private static readonly HashSet<string> AllowedCommands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ManualOverride",
            "PumpStart",
            "PumpStop",
            "RelayOn",
            "RelayOff",
            "EnableSensor",
            "DisableSensor"
        };

    public CommandStreamService(TelemetryService telemetryService)
    {
        _telemetryService = telemetryService;
    }

    public IReadOnlyList<SensorReadingRecord> GetTelemetry(string? deviceId = null)
    {
        return _telemetryService.GetReadings(deviceId);
    }

    public IReadOnlyList<CommandRecord> GetHistory(string? nodeId = null)
    {
        lock (_lock)
        {
            IEnumerable<CommandRecord> result = _commands;

            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                result = result.Where(command =>
                    command.NodeId.Equals(
                        nodeId,
                        StringComparison.OrdinalIgnoreCase));
            }

            return result
                .OrderByDescending(command => command.CreatedAt)
                .ToList();
        }
    }

    public CommandRecord CreateCommand(string nodeId, string command)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ArgumentException("Device ID is required.", nameof(nodeId));

        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Command is required.", nameof(command));

        if (!AllowedCommands.Contains(command))
            throw new ArgumentException("Unsupported command.", nameof(command));

        lock (_lock)
        {
            var record = new CommandRecord
            {
                Id = _nextId++,
                NodeId = nodeId,
                Command = command,
                Status = "Accepted",
                Result = "Command recorded; device execution is not yet connected.",
                CreatedAt = DateTime.UtcNow
            };

            _commands.Add(record);

            return record;
        }
    }
}

public class CommandRecord
{
    public int Id { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
