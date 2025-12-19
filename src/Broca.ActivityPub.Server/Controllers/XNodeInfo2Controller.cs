using Broca.ActivityPub.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Broca.ActivityPub.Server.Controllers;

/// <summary>
/// Controller for x-nodeinfo2 endpoint (alternative NodeInfo format)
/// </summary>
[ApiController]
public class XNodeInfo2Controller : ControllerBase
{
    private readonly NodeInfoService _nodeInfoService;
    private readonly ILogger<XNodeInfo2Controller> _logger;

    public XNodeInfo2Controller(NodeInfoService nodeInfoService, ILogger<XNodeInfo2Controller> logger)
    {
        _nodeInfoService = nodeInfoService;
        _logger = logger;
    }

    /// <summary>
    /// x-nodeinfo2 endpoint
    /// </summary>
    [HttpGet(".well-known/x-nodeinfo2")]
    [Produces("application/json")]
    public async Task<IActionResult> GetXNodeInfo2()
    {
        try
        {
            var result = await _nodeInfoService.GetXNodeInfo2Async(HttpContext.RequestAborted);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing x-nodeinfo2 request");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
