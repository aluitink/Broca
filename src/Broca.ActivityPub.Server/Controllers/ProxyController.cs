using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Server.Controllers;

/// <summary>
/// Provides a server-side proxy for client requests to work around CORS and browser signing restrictions
/// </summary>
/// <remarks>
/// This proxy allows WASM clients to make ActivityPub requests that require proper HTTP signatures.
/// Browser-based apps cannot set the Date header (required by most servers), but can use (created-date).
/// The proxy re-signs requests with the Date header using either:
/// - System actor credentials (default)
/// - User impersonation (if configured and authenticated)
/// </remarks>
[ApiController]
[Route("[controller]")]
public class ProxyController : ControllerBase
{
    private readonly SignedClientProvider _signedClientProvider;
    private readonly ActivityPubServerOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<ProxyController> _logger;

    public ProxyController(
        SignedClientProvider signedClientProvider,
        IOptions<ActivityPubServerOptions> options,
        IOptions<JsonSerializerOptions> jsonOptions,
        ILogger<ProxyController> logger)
    {
        _signedClientProvider = signedClientProvider;
        _options = options.Value;
        _jsonOptions = jsonOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Proxies a GET request to a remote ActivityPub resource
    /// </summary>
    /// <remarks>
    /// Used for webfinger lookups and ActivityStreams object downloads.
    /// Signs the request with the system actor's credentials using the Date header.
    /// </remarks>
    /// <param name="url">The URL to fetch</param>
    /// <returns>The proxied response</returns>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest(new { error = "URL parameter is required" });
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var targetUri))
        {
            return BadRequest(new { error = "Invalid URL format" });
        }

        _logger.LogInformation("Proxying GET request to: {Url}", url);

        try
        {
            var client = await _signedClientProvider.CreateForSystemActorAsync(HttpContext.RequestAborted);
            var result = await client.GetAsync<JsonElement>(targetUri, useCache: false, HttpContext.RequestAborted);

            var json = JsonSerializer.Serialize(result, _jsonOptions);
            return Content(json, "application/activity+json");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to proxy GET request to: {Url}", url);
            return StatusCode(502, new { error = "Failed to fetch remote resource", details = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error proxying GET request to: {Url}", url);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Proxies a POST request to a remote ActivityPub resource
    /// </summary>
    /// <remarks>
    /// Accepts an ActivityStreams object/activity, deserializes it, and posts it to the target URL.
    /// Signs the request with the system actor's credentials using the Date header.
    /// </remarks>
    /// <param name="url">The URL to post to</param>
    /// <param name="data">The ActivityStreams object to post</param>
    /// <returns>The proxied response</returns>
    [HttpPost]
    public async Task<IActionResult> Post([FromQuery] string url, [FromBody] JsonElement data)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest(new { error = "URL parameter is required" });
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var targetUri))
        {
            return BadRequest(new { error = "Invalid URL format" });
        }

        _logger.LogInformation("Proxying POST request to: {Url}", url);

        try
        {
            var client = await _signedClientProvider.CreateForSystemActorAsync(HttpContext.RequestAborted);
            using var response = await client.PostAsync(targetUri, data, HttpContext.RequestAborted);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Proxied POST request failed with status {StatusCode}", response.StatusCode);
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Error response: {ErrorContent}", errorContent);
                return StatusCode((int)response.StatusCode, new { error = $"Remote server returned {response.StatusCode}", details = errorContent });
            }

            var content = await response.Content.ReadAsStringAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";

            return Content(content, contentType);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to proxy POST request to: {Url}", url);
            return StatusCode(502, new { error = "Failed to post to remote resource", details = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error proxying POST request to: {Url}", url);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
