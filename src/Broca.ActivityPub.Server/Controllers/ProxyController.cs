using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Client.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;
using KristofferStrube.ActivityStreams;
using System.Net.Http.Json;

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
[Route("api/[controller]")]
public class ProxyController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpSignatureService _signatureService;
    private readonly ISystemIdentityService _systemIdentityService;
    private readonly ActivityPubServerOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<ProxyController> _logger;

    public ProxyController(
        IHttpClientFactory httpClientFactory,
        HttpSignatureService signatureService,
        ISystemIdentityService systemIdentityService,
        IOptions<ActivityPubServerOptions> options,
        IOptions<JsonSerializerOptions> jsonOptions,
        ILogger<ProxyController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _signatureService = signatureService;
        _systemIdentityService = systemIdentityService;
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
            // Get system actor credentials
            var systemActor = await _systemIdentityService.GetSystemActorAsync();
            var privateKey = await _systemIdentityService.GetSystemPrivateKeyAsync();
            var publicKeyId = $"{systemActor.Id}#main-key";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            using var request = new HttpRequestMessage(HttpMethod.Get, targetUri);
            
            // Set ActivityPub headers
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/activity+json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/ld+json", 0.9));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json", 0.8));
            request.Headers.UserAgent.ParseAdd(_options.UserAgent);

            // Sign the request with the system actor credentials using Date header
            await _signatureService.ApplyHttpSignatureAsync(
                request.Method.ToString(),
                targetUri,
                (headerName, headerValue) => request.Headers.TryAddWithoutValidation(headerName, headerValue),
                publicKeyId,
                privateKey,
                accept: request.Headers.Accept.ToString(),
                contentType: null,
                getContentFunc: null,
                cancellationToken: HttpContext.RequestAborted);

            var response = await httpClient.SendAsync(request, HttpContext.RequestAborted);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Proxied GET request failed with status {StatusCode}", response.StatusCode);
                return StatusCode((int)response.StatusCode, new { error = $"Remote server returned {response.StatusCode}" });
            }

            var content = await response.Content.ReadAsStringAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            
            return Content(content, contentType);
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
            // Get system actor credentials
            var systemActor = await _systemIdentityService.GetSystemActorAsync();
            var privateKey = await _systemIdentityService.GetSystemPrivateKeyAsync();
            var publicKeyId = $"{systemActor.Id}#main-key";

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            using var request = new HttpRequestMessage(HttpMethod.Post, targetUri);
            
            // Set content as JSON
            request.Content = JsonContent.Create(data, new MediaTypeHeaderValue("application/activity+json"), _jsonOptions);
            
            // Set headers
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/activity+json"));
            request.Headers.UserAgent.ParseAdd(_options.UserAgent);

            // Sign the request with the system actor credentials using Date header
            await _signatureService.ApplyHttpSignatureAsync(
                request.Method.ToString(),
                targetUri,
                (headerName, headerValue) => request.Headers.TryAddWithoutValidation(headerName, headerValue),
                publicKeyId,
                privateKey,
                accept: request.Headers.Accept.ToString(),
                contentType: request.Content.Headers.ContentType?.ToString(),
                getContentFunc: async (ct) => await request.Content.ReadAsByteArrayAsync(ct),
                cancellationToken: HttpContext.RequestAborted);

            var response = await httpClient.SendAsync(request, HttpContext.RequestAborted);
            
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
