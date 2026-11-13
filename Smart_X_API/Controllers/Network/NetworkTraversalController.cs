
using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Models.Network;
using Smart_X_API.Services;

namespace Smart_X_API.Controllers.Network;

[ApiController]
[Route("api/[controller]")]
public class NetworkTraversalController : ControllerBase
{
    private readonly INetworkTraversalService _traversalService;
    private readonly ILogger<NetworkTraversalController> _logger;

    public NetworkTraversalController(
        INetworkTraversalService traversalService,
        ILogger<NetworkTraversalController> logger)
    {
        _traversalService = traversalService;
        _logger = logger;
    }

    [HttpGet("bfs/{startNodeId}")]
    [ProducesResponseType(
        typeof(NetworkTraversalResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult BreadthFirstSearch(string startNodeId)
    {
        try
        {
            var result = _traversalService.BreadthFirstSearch(startNodeId);

            return Ok(result);
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
                "Failed to perform breadth-first search.");

            return Problem(
                title: "Breadth-first search failed.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("dfs/{startNodeId}")]
    [ProducesResponseType(
        typeof(NetworkTraversalResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DepthFirstSearch(string startNodeId)
    {
        try
        {
            var result = _traversalService.DepthFirstSearch(startNodeId);

            return Ok(result);
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
                "Failed to perform depth-first search.");

            return Problem(
                title: "Depth-first search failed.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
