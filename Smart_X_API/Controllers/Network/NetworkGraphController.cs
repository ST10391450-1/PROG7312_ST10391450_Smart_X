using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Services;

namespace Smart_X_API.Controllers.Network;

[ApiController]
[Route("api/[controller]")]
public class NetworkGraphController : ControllerBase
{
    private readonly INetworkGraphService _graphService;
    private readonly ILogger<NetworkGraphController> _logger;

    public NetworkGraphController(
        INetworkGraphService graphService,
        ILogger<NetworkGraphController> logger)
    {
        _graphService = graphService;
        _logger = logger;
    }

    [HttpGet("bfs/{startNodeId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult BreadthFirstSearch(string startNodeId)
    {
        return ExecuteGraphOperation(
            () => _graphService.BreadthFirstSearch(startNodeId));
    }

    [HttpGet("dfs/{startNodeId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DepthFirstSearch(string startNodeId)
    {
        return ExecuteGraphOperation(
            () => _graphService.DepthFirstSearch(startNodeId));
    }

    [HttpGet("reachable/{startNodeId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetReachableNodes(string startNodeId)
    {
        return ExecuteGraphOperation(
            () => _graphService.GetReachableNodes(startNodeId));
    }

    [HttpGet("disconnected/{startNodeId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetDisconnectedNodes(string startNodeId)
    {
        return ExecuteGraphOperation(
            () => _graphService.GetDisconnectedNodes(startNodeId));
    }

    [HttpGet("hops/{startNodeId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetHopCounts(string startNodeId)
    {
        return ExecuteGraphOperation(
            () => _graphService.GetHopCounts(startNodeId));
    }

    private IActionResult ExecuteGraphOperation<T>(Func<T> operation)
    {
        try
        {
            return Ok(operation());
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
                "A network graph operation failed.");

            return Problem(
                title: "Network graph operation failed.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}