using Broca.ActivityPub.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Broca.ActivityPub.Server.Controllers;

[ApiController]
[Route(".well-known/webfinger")]
public class WebFingerController : ControllerBase
{
    private readonly WebFingerService _webFingerService;
    private readonly ILogger<WebFingerController> _logger;

    public WebFingerController(WebFingerService webFingerService, ILogger<WebFingerController> logger)
    {
        _webFingerService = webFingerService;
        _logger = logger;
    }

    [HttpGet]
    [Produces("application/jrd+json")]
    public async Task<IActionResult> Get([FromQuery] string resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            return BadRequest(new { error = "Resource parameter is required" });
        }

        try
        {
            var result = await _webFingerService.GetResourceAsync(resource, HttpContext.RequestAborted);
            
            if (result == null)
            {
                return NotFound(new { error = "Resource not found" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WebFinger request for resource {Resource}", resource);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
