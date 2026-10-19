using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Models;

namespace Smart_X_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommandsController : ControllerBase
{
    private static readonly object Sync = new();
    private static readonly List<DeviceCommand> Commands = new();
    private static readonly Stack<UndoEntry> UndoStack = new();

    private static readonly HashSet<string> AllowedCommands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "RelayOn",
            "RelayOff",
            "PumpStart",
            "PumpStop",
            "ManualOverride",
            "EnableSensor",
            "DisableSensor"
        };

    private static readonly Dictionary<string, bool> OutputStates =
        new(StringComparer.OrdinalIgnoreCase);

    [HttpPost]
    public IActionResult CreateCommand([FromBody] CreateCommandRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.NodeId))
            return BadRequest("Node ID is required.");

        if (string.IsNullOrWhiteSpace(request.Command) ||
            !AllowedCommands.Contains(request.Command.Trim()))
            return BadRequest("Unsupported command.");

        lock (Sync)
        {
            var sensor = SensorsController.Sensors.FirstOrDefault(
                x => string.Equals(
                    x.NodeId,
                    request.NodeId.Trim(),
                    StringComparison.OrdinalIgnoreCase));

            if (sensor == null)
                return NotFound($"No sensor found with node ID '{request.NodeId}'.");

            var commandName = AllowedCommands.First(
                x => x.Equals(
                    request.Command.Trim(),
                    StringComparison.OrdinalIgnoreCase));

            var category = sensor.Category ?? string.Empty;
            var previousEnabled = sensor.IsEnabled;
            var previousOutput = OutputStates.TryGetValue(
                sensor.NodeId,
                out var existingOutput) && existingOutput;

            var command = new DeviceCommand
            {
                Id = Guid.NewGuid(),
                NodeId = sensor.NodeId,
                Command = commandName,
                Status = "Processing",
                CreatedAt = DateTime.UtcNow
            };

            var changesEnabledState =
                commandName is "EnableSensor" or "DisableSensor";

            if (changesEnabledState)
            {
                sensor.IsEnabled = commandName == "EnableSensor";
                command.Status = "Completed";
                command.Result = sensor.IsEnabled
                    ? "Simulated sensor enabled."
                    : "Simulated sensor disabled.";
            }
            else if (commandName is "RelayOn" or "RelayOff")
            {
                if (!category.Contains("actuator", StringComparison.OrdinalIgnoreCase) &&
                    !category.Contains("relay", StringComparison.OrdinalIgnoreCase))
                    return BadRequest("Relay commands require an actuator or relay device.");

                if (!sensor.IsEnabled)
                    return Conflict("The device is disabled. Enable it first.");

                OutputStates[sensor.NodeId] = commandName == "RelayOn";
                command.Status = "Completed";
                command.Result = OutputStates[sensor.NodeId]
                    ? "Simulated relay is ON."
                    : "Simulated relay is OFF.";
            }
            else if (commandName is "PumpStart" or "PumpStop")
            {
                if (!category.Contains("actuator", StringComparison.OrdinalIgnoreCase) &&
                    !category.Contains("pump", StringComparison.OrdinalIgnoreCase))
                    return BadRequest("Pump commands require an actuator or pump device.");

                if (!sensor.IsEnabled)
                    return Conflict("The device is disabled. Enable it first.");

                OutputStates[sensor.NodeId] = commandName == "PumpStart";
                command.Status = "Completed";
                command.Result = OutputStates[sensor.NodeId]
                    ? "Simulated pump is running."
                    : "Simulated pump is stopped.";
            }
            else
            {
                if (!sensor.IsEnabled)
                    return Conflict("The device is disabled. Enable it first.");

                OutputStates[sensor.NodeId] = !previousOutput;
                command.Status = "Completed";
                command.Result = OutputStates[sensor.NodeId]
                    ? "Simulated manual override turned output ON."
                    : "Simulated manual override turned output OFF.";
            }

            command.CompletedAt = DateTime.UtcNow;
            Commands.Add(command);

            UndoStack.Push(new UndoEntry
            {
                OriginalCommandId = command.Id,
                NodeId = sensor.NodeId,
                Command = commandName,
                PreviousEnabled = previousEnabled,
                PreviousOutput = previousOutput,
                ChangesEnabledState = changesEnabledState
            });

            return Ok(command);
        }
    }

    [HttpPost("undo")]
    public IActionResult UndoLastCommand()
    {
        lock (Sync)
        {
            if (UndoStack.Count == 0)
                return Conflict("There are no commands available to undo.");

            var entry = UndoStack.Peek();

            var sensor = SensorsController.Sensors.FirstOrDefault(
                x => string.Equals(
                    x.NodeId,
                    entry.NodeId,
                    StringComparison.OrdinalIgnoreCase));

            if (sensor == null)
                return Conflict("The device for the last command no longer exists.");

            if (entry.ChangesEnabledState)
            {
                sensor.IsEnabled = entry.PreviousEnabled;
            }
            else
            {
                OutputStates[sensor.NodeId] = entry.PreviousOutput;
            }

            UndoStack.Pop();

            var undoCommand = new DeviceCommand
            {
                Id = Guid.NewGuid(),
                NodeId = sensor.NodeId,
                Command = "Undo",
                Status = "Completed",
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Result = $"Undid {entry.Command} (command ID: {entry.OriginalCommandId}). " +
                         (entry.ChangesEnabledState
                             ? $"Device enabled state restored to {sensor.IsEnabled}."
                             : $"Output state restored to {(entry.PreviousOutput ? "ON" : "OFF")}.")
            };

            Commands.Add(undoCommand);

            return Ok(new
            {
                Message = "Last command undone successfully.",
                OriginalCommandId = entry.OriginalCommandId,
                Undo = undoCommand,
                DeviceState = new
                {
                    sensor.NodeId,
                    sensor.IsEnabled,
                    OutputOn = OutputStates.TryGetValue(sensor.NodeId, out var state) && state
                }
            });
        }
    }

    [HttpGet("undo/available")]
    public IActionResult IsUndoAvailable()
    {
        lock (Sync)
        {
            return Ok(new
            {
                CanUndo = UndoStack.Count > 0,
                UndoCount = UndoStack.Count,
                LastCommand = UndoStack.Count > 0
                    ? UndoStack.Peek().Command
                    : null
            });
        }
    }

    [HttpGet("pending")]
    public IActionResult GetPendingCommands()
    {
        lock (Sync)
        {
            return Ok(Commands
                .Where(x => x.Status == "Pending")
                .OrderBy(x => x.CreatedAt)
                .ToList());
        }
    }

    [HttpPost("{id:guid}/result")]
    public IActionResult UpdateCommandResult(
        Guid id,
        [FromBody] CommandResultRequest request)
    {
        if (request == null)
            return BadRequest("Command result is required.");

        lock (Sync)
        {
            var command = Commands.FirstOrDefault(x => x.Id == id);

            if (command == null)
                return NotFound("Command not found.");

            if (command.Status != "Processing")
                return Conflict("Command is not awaiting a result.");

            command.Status = request.Success ? "Completed" : "Failed";
            command.Result = request.Result;
            command.CompletedAt = DateTime.UtcNow;

            return Ok(command);
        }
    }

    [HttpGet]
    public IActionResult GetCommands()
    {
        lock (Sync)
        {
            return Ok(Commands
                .OrderByDescending(x => x.CreatedAt)
                .ToList());
        }
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetCommand(Guid id)
    {
        lock (Sync)
        {
            var command = Commands.FirstOrDefault(x => x.Id == id);

            return command == null
                ? NotFound("Command not found.")
                : Ok(command);
        }
    }

    [HttpGet("device/{nodeId}/state")]
    public IActionResult GetDeviceState(string nodeId)
    {
        lock (Sync)
        {
            var sensor = SensorsController.Sensors.FirstOrDefault(
                x => string.Equals(
                    x.NodeId,
                    nodeId,
                    StringComparison.OrdinalIgnoreCase));

            if (sensor == null)
                return NotFound("Device not found.");

            var outputOn = OutputStates.TryGetValue(
                sensor.NodeId,
                out var state) && state;

            return Ok(new
            {
                sensor.NodeId,
                sensor.Category,
                sensor.IsEnabled,
                OutputOn = outputOn,
                OutputState = outputOn ? "ON" : "OFF"
            });
        }
    }

    private sealed class UndoEntry
    {
        public Guid OriginalCommandId { get; init; }
        public string NodeId { get; init; } = string.Empty;
        public string Command { get; init; } = string.Empty;
        public bool PreviousEnabled { get; init; }
        public bool PreviousOutput { get; init; }
        public bool ChangesEnabledState { get; init; }
    }
}

public class CreateCommandRequest
{
    public string NodeId { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
}

public class CommandResultRequest
{
    public bool Success { get; set; }
    public string Result { get; set; } = string.Empty;
}

public class DeviceCommand
{
    public Guid Id { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? Result { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}