
using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Models.Network;
using Smart_X_API.Services;

namespace Smart_X_API.Controllers.Network;

[ApiController]
[Route("api/[controller]")]
public class NetworkReachabilityController : ControllerBase
{
    private readonly INetworkReachabilityService _reachabilityService;
    private readonly ILogger<NetworkReachabilityController> _logger;

    public NetworkReachabilityController(
        INetworkReachabilityService reachabilityService,
        ILogger<NetworkReachabilityController> logger)
    {
        _reachabilityService = reachabilityService;
        _logger = logger;
    }

    [HttpGet("{startNodeId}/{destinationNodeId}")]
    [ProducesResponseType(
        typeof(NetworkReachabilityResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult CheckReachability(
        string startNodeId,
        string destinationNodeId)
    {
        try
        {
            var result = _reachabilityService.CheckReachability(
                startNodeId,
                destinationNodeId);

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
                "Failed to check network reachability.");

            return Problem(
                title: "Network reachability check failed.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
