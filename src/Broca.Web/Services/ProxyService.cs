using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Broca.Web.Services;

public class ProxyService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProxyService> _logger;

    public ProxyService(HttpClient httpClient, ILogger<ProxyService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public virtual async Task<T?> GetViaProxyAsync<T>(Uri targetUri, CancellationToken cancellationToken = default)
    {
        if (_httpClient.BaseAddress == null)
        {
            _logger.LogDebug("Proxy HttpClient has no BaseAddress; skipping proxy for {Uri}", targetUri);
            return default;
        }

        try
        {
            _logger.LogInformation("Attempting to fetch {Uri} via proxy", targetUri);

            var proxyUrl = $"/ap/proxy?url={Uri.EscapeDataString(targetUri.ToString())}";
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

    public virtual async Task<HttpResponseMessage> PostViaProxyAsync<T>(Uri targetUri, T data, CancellationToken cancellationToken = default)
    {
        if (_httpClient.BaseAddress == null)
            throw new InvalidOperationException($"Proxy HttpClient has no BaseAddress configured; cannot POST via proxy to {targetUri}");

        try
        {
            _logger.LogInformation("Attempting to post to {Uri} via proxy", targetUri);

            var proxyUrl = $"/ap/proxy?url={Uri.EscapeDataString(targetUri.ToString())}";
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
