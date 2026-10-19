
using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Smart_X_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommandsController : ControllerBase
{
    private static readonly object Sync = new();
    private static readonly List<DeviceCommand> Commands = new();

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

            var command = new DeviceCommand
            {
                Id = Guid.NewGuid(),
                NodeId = sensor.NodeId,
                Command = commandName,
                Status = "Processing",
                CreatedAt = DateTime.UtcNow
            };

            var category = sensor.Category ?? string.Empty;

            if (commandName is "EnableSensor" or "DisableSensor")
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
                {
                    return BadRequest("Relay commands require an actuator or relay device.");
                }

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
                {
                    return BadRequest("Pump commands require an actuator or pump device.");
                }

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

                var currentState = OutputStates.TryGetValue(
                    sensor.NodeId,
                    out var state) && state;

                OutputStates[sensor.NodeId] = !currentState;

                command.Status = "Completed";
                command.Result = OutputStates[sensor.NodeId]
                    ? "Simulated manual override turned output ON."
                    : "Simulated manual override turned output OFF.";
            }

            command.CompletedAt = DateTime.UtcNow;
            Commands.Add(command);

            return Ok(command);
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
