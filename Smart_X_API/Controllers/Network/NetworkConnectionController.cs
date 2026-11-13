using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Models.Network;
using Smart_X_API.Services;

namespace Smart_X_API.Controllers.Network;

[ApiController]
[Route("api/[controller]")]
public class NetworkConnectionController : ControllerBase
{
    private readonly INetworkConnectionService _connectionService;
    private readonly ILogger<NetworkConnectionController> _logger;

    public NetworkConnectionController(
        INetworkConnectionService connectionService,
        ILogger<NetworkConnectionController> logger)
    {
        _connectionService = connectionService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<NetworkConnection>),
        StatusCodes.Status200OK)]
    public IActionResult GetConnections()
    {
        return Ok(_connectionService.GetConnections());
    }

    [HttpGet("{nodeId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetConnectionsByNode(string nodeId)
    {
        var connections = _connectionService.GetConnections(nodeId);

        return Ok(connections);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult AddConnection(
        [FromBody] NetworkConnection connection)
    {
        try
        {
            var createdConnection =
                _connectionService.AddConnection(connection);

            return Created(
                $"{Request.Path}/{createdConnection.SourceNodeId}",
                createdConnection);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new
            {
                Message = exception.Message
            });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new
            {
                Message = exception.Message
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to register network connection.");

            return Problem(
                title: "Network connection registration failed.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpDelete("{sourceNodeId}/{destinationNodeId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DeleteConnection(
        string sourceNodeId,
        string destinationNodeId)
    {
        var removed = _connectionService.RemoveConnection(
            sourceNodeId,
            destinationNodeId);

        return removed
            ? NoContent()
            : NotFound("Network connection was not found.");
    }
}
