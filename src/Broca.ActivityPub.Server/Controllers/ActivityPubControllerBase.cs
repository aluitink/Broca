using Microsoft.AspNetCore.Mvc;

namespace Broca.ActivityPub.Server.Controllers;

/// <summary>
/// Base controller for ActivityPub endpoints with common helper methods
/// </summary>
public abstract class ActivityPubControllerBase : ControllerBase
{
    /// <summary>
    /// Gets the base URL for the request, handling reverse proxy scenarios.
    /// Uses X-Forwarded-Proto header if present (for HTTPS termination at proxy).
    /// </summary>
    /// <param name="suffix">Optional suffix to append to the base URL</param>
    /// <returns>The base URL with the correct scheme</returns>
    protected string GetBaseUrl(string? suffix = null)
    {
        // Use X-Forwarded-Proto if present (for reverse proxy scenarios), otherwise use Request.Scheme
        var scheme = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;
        var baseUrl = $"{scheme}://{Request.Host}";
        
        if (!string.IsNullOrEmpty(suffix))
        {
            baseUrl += suffix.TrimEnd('/');
        }
        
        return baseUrl;
    }
}
