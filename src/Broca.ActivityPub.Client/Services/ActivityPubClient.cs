using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Client.Services;

/// <summary>
/// Client implementation for ActivityPub requests
/// </summary>
/// <remarks>
/// Supports both anonymous and authenticated modes:
/// - Anonymous: Browse public ActivityPub content without signing requests
/// - Authenticated: Sign requests with actor's private key for authenticated access
/// </remarks>
public class ActivityPubClient : IActivityPubClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebFingerService _webFingerService;
    private readonly HttpSignatureService _signatureService;
    private readonly ILogger<ActivityPubClient> _logger;
    private readonly ActivityPubClientOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    // Simple in-memory cache
    private readonly Dictionary<string, (object Data, DateTime Expiry)> _cache = new();

    public string? ActorId => _options.ActorId;

    public ActivityPubClient(
        IHttpClientFactory httpClientFactory,
        IWebFingerService webFingerService,
        HttpSignatureService signatureService,
        IOptions<ActivityPubClientOptions> options,
        ILogger<ActivityPubClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _webFingerService = webFingerService ?? throw new ArgumentNullException(nameof(webFingerService));
        _signatureService = signatureService ?? throw new ArgumentNullException(nameof(signatureService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <inheritdoc/>
    public async Task<Actor?> GetSelfAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ActorId))
        {
            _logger.LogDebug("GetSelfAsync called in anonymous mode, returning null");
            return null;
        }

        return await GetActorAsync(new Uri(_options.ActorId), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Actor> GetActorAsync(Uri actorUri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actorUri);

        _logger.LogInformation("Fetching actor: {ActorUri}", actorUri);

        return await GetAsync<Actor>(actorUri, useCache: true, cancellationToken) 
            ?? throw new InvalidOperationException($"Failed to fetch actor: {actorUri}");
    }

    /// <inheritdoc/>
    public async Task<Actor> GetActorByAliasAsync(string alias, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);

        _logger.LogInformation("Resolving actor by alias: {Alias}", alias);

        // Use WebFinger to resolve the alias to an actor URI
        using var httpClient = CreateHttpClient();
        var webFingerResource = await _webFingerService.WebFingerUserByAliasAsync(httpClient, alias, cancellationToken);

        // Find the ActivityPub profile link
        var actorLink = webFingerResource.Links?.FirstOrDefault(l => 
            l.Rel == "self" && l.Type == "application/activity+json");

        if (actorLink?.Href == null)
        {
            throw new InvalidOperationException($"No ActivityPub profile found for alias: {alias}");
        }

        return await GetActorAsync(new Uri(actorLink.Href), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(Uri uri, bool useCache = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        _logger.LogDebug("GET request to: {Uri} (useCache: {UseCache})", uri, useCache);

        // Check cache
        if (useCache && _options.EnableCaching && TryGetFromCache<T>(uri.ToString(), out var cachedValue))
        {
            _logger.LogDebug("Returning cached value for: {Uri}", uri);
            return cachedValue;
        }

        using var httpClient = CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);

        // Set Accept header for ActivityPub content
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/activity+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/ld+json", 0.9));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json", 0.8));

        // Sign request if authenticated
        if (_options.IsAuthenticated)
        {
            await SignRequestAsync(request, cancellationToken);
        }

        try
        {
            var response = await httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET request failed with status {StatusCode} for: {Uri}", 
                    response.StatusCode, uri);
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return default;
                }
                
                response.EnsureSuccessStatusCode();
            }

            var result = await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken);

            // Cache the result
            if (useCache && _options.EnableCaching && result != null)
            {
                AddToCache(uri.ToString(), result);
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for: {Uri}", uri);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<HttpResponseMessage> PostAsync<T>(Uri uri, T obj, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(obj);

        if (!_options.IsAuthenticated)
        {
            throw new InvalidOperationException("POST requests require authenticated mode. Configure ActorId and PrivateKeyPem.");
        }

        _logger.LogInformation("POST request to: {Uri}", uri);

        using var httpClient = CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(obj, new MediaTypeHeaderValue("application/activity+json"), _jsonOptions)
        };

        // Sign the request
        await SignRequestAsync(request, cancellationToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("POST request failed with status {StatusCode} for: {Uri}", 
                response.StatusCode, uri);
        }

        return response;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<T> GetCollectionAsync<T>(
        Uri collectionUri, 
        int? limit = null, 
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collectionUri);

        _logger.LogInformation("Fetching collection: {CollectionUri} (limit: {Limit})", collectionUri, limit);

        var currentUri = collectionUri;
        var itemCount = 0;

        while (currentUri != null)
        {
            var page = await GetAsync<JsonElement>(currentUri, useCache: true, cancellationToken);
            
            if (page.ValueKind == JsonValueKind.Undefined || page.ValueKind == JsonValueKind.Null)
            {
                yield break;
            }

            // Handle both Collection and OrderedCollection
            JsonElement items;
            if (page.TryGetProperty("orderedItems", out var orderedItems))
            {
                items = orderedItems;
            }
            else if (page.TryGetProperty("items", out var regularItems))
            {
                items = regularItems;
            }
            else if (page.TryGetProperty("first", out var first))
            {
                // This is a collection, navigate to first page
                if (first.ValueKind == JsonValueKind.String)
                {
                    currentUri = new Uri(first.GetString()!);
                    continue;
                }
                else if (first.ValueKind == JsonValueKind.Object)
                {
                    items = first;
                }
                else
                {
                    yield break;
                }
            }
            else
            {
                yield break;
            }

            // Extract items from the page
            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (limit.HasValue && itemCount >= limit.Value)
                    {
                        yield break;
                    }

                    T? deserializedItem = default;
                    try
                    {
                        deserializedItem = JsonSerializer.Deserialize<T>(item.GetRawText(), _jsonOptions);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize collection item");
                        continue;
                    }

                    if (deserializedItem != null)
                    {
                        yield return deserializedItem;
                        itemCount++;
                    }
                }
            }

            // Check for next page
            if (page.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String)
            {
                currentUri = new Uri(next.GetString()!);
            }
            else
            {
                currentUri = null;
            }
        }
    }

    /// <inheritdoc/>
    public IActivityBuilder CreateActivityBuilder()
    {
        if (!_options.IsAuthenticated || string.IsNullOrWhiteSpace(_options.ActorId))
        {
            throw new InvalidOperationException(
                "Activity builder requires authenticated mode. Configure ActorId and PrivateKeyPem.");
        }

        // Extract base URL from actor ID
        var actorUri = new Uri(_options.ActorId);
        var baseUrl = $"{actorUri.Scheme}://{actorUri.Authority}";

        return new ActivityBuilder(_options.ActorId, baseUrl, _logger);
    }

    /// <inheritdoc/>
    public async Task<HttpResponseMessage> PostToOutboxAsync(Activity activity, CancellationToken cancellationToken = default)
    {
        if (!_options.IsAuthenticated || string.IsNullOrWhiteSpace(_options.ActorId))
        {
            throw new InvalidOperationException(
                "Posting to outbox requires authenticated mode. Configure ActorId and PrivateKeyPem.");
        }

        ArgumentNullException.ThrowIfNull(activity);

        // Get the authenticated actor to find their outbox
        var actor = await GetSelfAsync(cancellationToken);
        if (actor?.Outbox == null)
        {
            throw new InvalidOperationException("Actor does not have an outbox endpoint");
        }

        var outboxUri = actor.Outbox.Href 
            ?? throw new InvalidOperationException("Actor outbox has no valid URI");

        _logger.LogInformation("Posting activity {ActivityId} to outbox: {OutboxUri}", 
            activity.Id, outboxUri);

        return await PostAsync(outboxUri, activity, cancellationToken);
    }

    /// <summary>
    /// Signs an HTTP request using HTTP Signatures
    /// </summary>
    private async Task SignRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.PublicKeyId) || string.IsNullOrWhiteSpace(_options.PrivateKeyPem))
        {
            throw new InvalidOperationException("PublicKeyId and PrivateKeyPem are required for signing requests");
        }

        var method = request.Method.ToString();
        var uri = request.RequestUri!;
        var contentType = request.Content?.Headers.ContentType?.ToString();
        var accept = request.Headers.Accept.Count > 0 ? request.Headers.Accept.ToString() : null;

        await _signatureService.ApplyHttpSignatureAsync(
            method,
            uri,
            (headerName, headerValue) => request.Headers.TryAddWithoutValidation(headerName, headerValue),
            _options.PublicKeyId,
            _options.PrivateKeyPem,
            accept,
            contentType,
            async (ct) => request.Content != null ? await request.Content.ReadAsByteArrayAsync(ct) : Array.Empty<byte>(),
            cancellationToken);
    }

    /// <summary>
    /// Creates an HTTP client with appropriate headers
    /// </summary>
    private HttpClient CreateHttpClient()
    {
        var client = _httpClientFactory.CreateClient("ActivityPub");
        client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        return client;
    }

    /// <summary>
    /// Tries to get a value from the cache
    /// </summary>
    private bool TryGetFromCache<T>(string key, out T? value)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.Expiry > DateTime.UtcNow)
                {
                    value = (T)entry.Data;
                    return true;
                }
                else
                {
                    _cache.Remove(key);
                }
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Adds a value to the cache
    /// </summary>
    private void AddToCache<T>(string key, T value)
    {
        lock (_cache)
        {
            var expiry = DateTime.UtcNow.AddMinutes(_options.CacheExpirationMinutes);
            _cache[key] = (value!, expiry);
        }
    }
}
