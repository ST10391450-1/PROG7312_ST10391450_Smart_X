
using Microsoft.AspNetCore.Mvc;
using Smart_X_API.Models.Network;
using Smart_X_API.Services;

namespace Smart_X_API.Controllers.Network;

[ApiController]
[Route("api/[controller]")]
public class NetworkTopologyController : ControllerBase
{
    private readonly INetworkTopologyService _networkTopologyService;
    private readonly ILogger<NetworkTopologyController> _logger;

    public NetworkTopologyController(
        INetworkTopologyService networkTopologyService,
        ILogger<NetworkTopologyController> logger)
    {
        _networkTopologyService = networkTopologyService;
        _logger = logger;
    }

    #region Functionality for Retrieving Network Topology

    [HttpGet]
    [ProducesResponseType(
        typeof(NetworkTopologyResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<NetworkTopologyResponse> GetTopology()
    {
        try
        {
            var topology = _networkTopologyService.GetTopology();

            return Ok(topology);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to retrieve network topology.");

            return Problem(
                title: "Network topology retrieval failed.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    #endregion
}
