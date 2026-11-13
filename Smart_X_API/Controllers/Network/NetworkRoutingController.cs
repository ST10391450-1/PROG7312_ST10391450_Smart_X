using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Models.Network;
using Smart_X_API.Services;

namespace Smart_X_API.Controllers.Network;

[ApiController]
[Route("api/[controller]")]
public class NetworkRoutingController : ControllerBase
{
    private readonly INetworkRoutingService _routingService;
    private readonly ILogger<NetworkRoutingController> _logger;

    public NetworkRoutingController(
        INetworkRoutingService routingService,
        ILogger<NetworkRoutingController> logger)
    {
        _routingService = routingService;
        _logger = logger;
    }

    [HttpGet("mst")]
    [ProducesResponseType(typeof(NetworkMstResponse), StatusCodes.Status200OK)]
    public IActionResult GetMinimumSpanningTree()
    {
        try
        {
            return Ok(_routingService.CalculateMinimumSpanningTree());
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to calculate the minimum spanning tree.");

            return Problem(
                title: "Minimum spanning tree calculation failed.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("shortest-path/{startNodeId}/{destinationNodeId}")]
    [ProducesResponseType(
        typeof(NetworkShortestPathResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetMostEfficientPath(
        string startNodeId,
        string destinationNodeId)
    {
        try
        {
            return Ok(_routingService.FindMostEfficientPath(
                startNodeId,
                destinationNodeId));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { Message = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { Message = exception.Message });
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to calculate the lowest-power path.");

            return Problem(
                title: "Lowest-power path calculation failed.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
