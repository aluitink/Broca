using System.Globalization;
using Microsoft.AspNetCore.Http;
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

    protected static void ValidateRequestClockSkew(HttpRequest request)
    {
        var dateHeaderValue = request.Headers.TryGetValue("Date", out var dateValues)
            ? dateValues.ToString()
            : request.Headers.TryGetValue("Created", out var createdValues)
                ? createdValues.ToString()
                : null;

        if (string.IsNullOrEmpty(dateHeaderValue))
            throw new InvalidOperationException("Request is missing a Date or Created header");

        if (!DateTimeOffset.TryParse(dateHeaderValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var requestDate))
            throw new InvalidOperationException($"Request Date header is not a valid date/time: '{dateHeaderValue}'");

        var now = DateTimeOffset.UtcNow;

        if (now - requestDate > TimeSpan.FromHours(12))
            throw new InvalidOperationException("Request date is too old (clock skew exceeds 12 hours)");

        if (requestDate - now > TimeSpan.FromMinutes(5))
            throw new InvalidOperationException("Request date is too far in the future (clock skew exceeds 5 minutes)");
    }
}
