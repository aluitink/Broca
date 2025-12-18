using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Broca.ActivityPub.Client.Services;

/// <summary>
/// Provides fallback proxy support for CORS-blocked requests
/// </summary>
public class ProxyService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProxyService> _logger;

    public ProxyService(HttpClient httpClient, ILogger<ProxyService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Attempts to fetch a resource through the origin server proxy
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response to</typeparam>
    /// <param name="targetUri">The URI to fetch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The deserialized response, or null if the request failed</returns>
    public async Task<T?> GetViaProxyAsync<T>(Uri targetUri, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Attempting to fetch {Uri} via proxy", targetUri);

            // Build proxy URL - assumes the server has a /api/proxy endpoint
            var proxyUrl = $"/api/proxy?url={Uri.EscapeDataString(targetUri.ToString())}";
            
            var response = await _httpClient.GetAsync(proxyUrl, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Proxy request failed with status {StatusCode} for: {Uri}", 
                    response.StatusCode, targetUri);
                return default;
            }

            var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
            
            _logger.LogInformation("Successfully fetched {Uri} via proxy", targetUri);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch {Uri} via proxy", targetUri);
            return default;
        }
    }

    /// <summary>
    /// Posts data to a resource through the origin server proxy
    /// </summary>
    /// <typeparam name="T">The type of data to post</typeparam>
    /// <param name="targetUri">The URI to post to</param>
    /// <param name="data">The data to post</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The HTTP response</returns>
    public async Task<HttpResponseMessage> PostViaProxyAsync<T>(Uri targetUri, T data, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Attempting to post to {Uri} via proxy", targetUri);

            // Build proxy URL
            var proxyUrl = $"/api/proxy?url={Uri.EscapeDataString(targetUri.ToString())}";
            
            var response = await _httpClient.PostAsJsonAsync(proxyUrl, data, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Proxy POST failed with status {StatusCode} for: {Uri}", 
                    response.StatusCode, targetUri);
            }
            else
            {
                _logger.LogInformation("Successfully posted to {Uri} via proxy", targetUri);
            }
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post to {Uri} via proxy", targetUri);
            throw;
        }
    }
}
