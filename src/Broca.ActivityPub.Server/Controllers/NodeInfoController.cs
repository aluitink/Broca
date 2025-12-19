using Broca.ActivityPub.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Broca.ActivityPub.Server.Controllers;

/// <summary>
/// Controller for NodeInfo protocol endpoints
/// </summary>
[ApiController]
public class NodeInfoController : ControllerBase
{
    private readonly NodeInfoService _nodeInfoService;
    private readonly ILogger<NodeInfoController> _logger;

    public NodeInfoController(NodeInfoService nodeInfoService, ILogger<NodeInfoController> logger)
    {
        _nodeInfoService = nodeInfoService;
        _logger = logger;
    }

    /// <summary>
    /// NodeInfo discovery endpoint
    /// </summary>
    [HttpGet(".well-known/nodeinfo")]
    [Produces("application/json")]
    public IActionResult GetNodeInfoDiscovery()
    {
        try
        {
            var result = _nodeInfoService.GetNodeInfoDiscovery();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing NodeInfo discovery request");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// NodeInfo 2.0 endpoint
    /// </summary>
    [HttpGet("nodeinfo/2.0")]
    [Produces("application/json; profile=\"http://nodeinfo.diaspora.software/ns/schema/2.0#\"")]
    public async Task<IActionResult> GetNodeInfo20()
    {
        try
        {
            var result = await _nodeInfoService.GetNodeInfo20Async(HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing NodeInfo 2.0 request");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// NodeInfo 2.1 endpoint
    /// </summary>
    [HttpGet("nodeinfo/2.1")]
    [Produces("application/json; profile=\"http://nodeinfo.diaspora.software/ns/schema/2.1#\"")]
    public async Task<IActionResult> GetNodeInfo21()
    {
        try
        {
            var result = await _nodeInfoService.GetNodeInfo21Async(HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing NodeInfo 2.1 request");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
