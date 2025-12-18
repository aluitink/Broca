using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Client.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Broca.ActivityPub.Server.Controllers;

/// <summary>
/// Provides a server-side proxy for client requests to work around CORS restrictions
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProxyController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpSignatureService _signatureService;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<ProxyController> _logger;

    public ProxyController(
        IHttpClientFactory httpClientFactory,
        HttpSignatureService signatureService,
        IOptions<ActivityPubServerOptions> options,
        ILogger<ProxyController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _signatureService = signatureService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Proxies a GET request to a remote ActivityPub resource
    /// </summary>
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
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            using var request = new HttpRequestMessage(HttpMethod.Get, targetUri);
            
            // Set ActivityPub headers
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/activity+json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/ld+json", 0.9));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json", 0.8));
            request.Headers.UserAgent.ParseAdd("Broca.ActivityPub.Server/1.0");

            // Check if we should sign the request with the system actor
            var shouldSign = await ShouldSignProxyRequest(targetUri);
            if (shouldSign)
            {
                await SignProxyRequestAsync(request);
            }

            var response = await httpClient.SendAsync(request);
            
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
    /// <param name="url">The URL to post to</param>
    /// <param name="data">The data to post</param>
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
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            using var request = new HttpRequestMessage(HttpMethod.Post, targetUri);
            
            // Set content
            var jsonContent = JsonSerializer.Serialize(data);
            request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/activity+json");
            
            // Set headers
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/activity+json"));
            request.Headers.UserAgent.ParseAdd("Broca.ActivityPub.Server/1.0");

            // Sign the request
            await SignProxyRequestAsync(request);

            var response = await httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Proxied POST request failed with status {StatusCode}", response.StatusCode);
                return StatusCode((int)response.StatusCode, new { error = $"Remote server returned {response.StatusCode}" });
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

    /// <summary>
    /// Determines if a proxy request should be signed
    /// </summary>
    private Task<bool> ShouldSignProxyRequest(Uri targetUri)
    {
        // For now, we'll sign all requests that might require authentication
        // In the future, this could be more sophisticated
        return Task.FromResult(true);
    }

    /// <summary>
    /// Signs a proxy request using the system actor's credentials
    /// </summary>
    private async Task SignProxyRequestAsync(HttpRequestMessage request)
    {
        // TODO: Get system actor credentials from configuration
        // For now, this is a placeholder that doesn't sign
        // In a real implementation, you would:
        // 1. Get the system actor ID and private key from configuration
        // 2. Use the HttpSignatureService to sign the request
        
        _logger.LogDebug("Proxy request signing not yet implemented");
        await Task.CompletedTask;
    }
}
