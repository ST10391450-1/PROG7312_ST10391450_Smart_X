
using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Models.Network;
using Smart_X_API.Services;

namespace Smart_X_API.Controllers.Network;

[ApiController]
[Route("api/[controller]")]
public class NetworkDisconnectedController : ControllerBase
{
    private readonly INetworkDisconnectedService _disconnectedService;
    private readonly ILogger<NetworkDisconnectedController> _logger;

    public NetworkDisconnectedController(
        INetworkDisconnectedService disconnectedService,
        ILogger<NetworkDisconnectedController> logger)
    {
        _disconnectedService = disconnectedService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(NetworkDisconnectedResponse),
        StatusCodes.Status200OK)]
    public IActionResult GetDisconnectedNodes()
    {
        try
        {
            var result = _disconnectedService.GetDisconnectedNodes();

            return Ok(result);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to retrieve disconnected network nodes.");

            return Problem(
                title: "Disconnected node retrieval failed.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
