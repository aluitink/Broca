using Microsoft.AspNetCore.Mvc;

namespace Broca.ActivityPub.Server.Controllers;

[ApiController]
public class NsController : ControllerBase
{
    [HttpGet("ns/broca")]
    [Produces("application/ld+json", "application/json")]
    public IActionResult GetBrocaContext()
    {
        var scheme = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;
        var baseUrl = $"{scheme}://{Request.Host}";
        var nsBase = $"{baseUrl}/ns/broca#";

        var context = new
        {
            @context = new Dictionary<string, object>
            {
                ["broca"] = nsBase,
                ["broca:collections"] = new { @id = "broca:collections", @type = "@id" },
                ["broca:featured"] = new { @id = "broca:featured", @type = "@id" },
            }
        };

        return Ok(context);
    }
}
