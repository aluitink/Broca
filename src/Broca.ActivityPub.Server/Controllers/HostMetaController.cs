using Broca.ActivityPub.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;

namespace Broca.ActivityPub.Server.Controllers;

/// <summary>
/// Controller for host-meta endpoint (WebFinger discovery)
/// </summary>
[ApiController]
public class HostMetaController : ControllerBase
{
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<HostMetaController> _logger;

    public HostMetaController(IOptions<ActivityPubServerOptions> options, ILogger<HostMetaController> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// host-meta endpoint (XRD format)
    /// </summary>
    [HttpGet(".well-known/host-meta")]
    [Produces("application/xrd+xml")]
    public IActionResult GetHostMeta()
    {
        try
        {
            var baseUrl = _options.BaseUrl.TrimEnd('/');
            var domain = new Uri(baseUrl).Host;

            var xrd = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<XRD xmlns=""http://docs.oasis-open.org/ns/xri/xrd-1.0"">
  <Link rel=""lrdd"" template=""{baseUrl}/.well-known/webfinger?resource={{uri}}""/>
</XRD>";

            return Content(xrd, "application/xrd+xml", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing host-meta request");
            return StatusCode(500);
        }
    }

    /// <summary>
    /// host-meta.json endpoint (JRD format)
    /// </summary>
    [HttpGet(".well-known/host-meta.json")]
    [Produces("application/jrd+json")]
    public IActionResult GetHostMetaJson()
    {
        try
        {
            var baseUrl = _options.BaseUrl.TrimEnd('/');
            
            var jrd = new
            {
                links = new[]
                {
                    new
                    {
                        rel = "lrdd",
                        template = $"{baseUrl}/.well-known/webfinger?resource={{uri}}"
                    }
                }
            };

            return Ok(jrd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing host-meta.json request");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
