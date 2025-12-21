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

    /// <summary>
    /// Initializes the client by fetching credentials from the server using the API key
    /// </summary>
    /// <remarks>
    /// This method should be called when the client is configured with ActorId and ApiKey
    /// but not PrivateKeyPem. It fetches the actor's profile including the private key
    /// using the API key as authentication.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="InvalidOperationException">Thrown if ApiKey is not configured or actor cannot be fetched</exception>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.RequiresInitialization)
        {
            _logger.LogDebug("Client does not require initialization (already has private key or missing API key)");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.ActorId))
        {
            throw new InvalidOperationException("ActorId must be configured to initialize the client");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("ApiKey must be configured to initialize the client");
        }

        _logger.LogInformation("Initializing ActivityPub client for actor {ActorId} using API key", _options.ActorId);

        try
        {
            // Fetch actor profile with API key to get private key
            var client = CreateHttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_options.ActorId));
            
            // Set Accept headers for ActivityPub
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/activity+json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/ld+json", 0.9));
            
            // Add API key as Bearer token
            _logger.LogDebug("Adding Authorization header with API key for initialization request");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            _logger.LogDebug("Sending initialization request to {ActorId}", _options.ActorId);
            var response = await client.SendAsync(request, cancellationToken);
            
            _logger.LogInformation("Initialization request returned status {StatusCode}", response.StatusCode);
            response.EnsureSuccessStatusCode();

            var actor = await response.Content.ReadFromJsonAsync<Actor>(_jsonOptions, cancellationToken);
            if (actor == null)
            {
                throw new InvalidOperationException($"Failed to deserialize actor from {_options.ActorId}");
            }

            // Extract private key from extension data
            if (actor.ExtensionData?.TryGetValue("privateKeyPem", out var privateKeyElement) == true)
            {
                var privateKeyPem = privateKeyElement.GetString();
                if (string.IsNullOrWhiteSpace(privateKeyPem))
                {
                    throw new InvalidOperationException($"Actor {_options.ActorId} response did not include privateKeyPem. Ensure the API key is valid and the server is configured to return private keys.");
                }

                // Update options with private key
                _options.PrivateKeyPem = privateKeyPem;
                
                // Set public key ID if not already set
                if (string.IsNullOrWhiteSpace(_options.PublicKeyId))
                {
                    _options.PublicKeyId = $"{_options.ActorId.TrimEnd('#')}#main-key";
                }

                _logger.LogInformation("Successfully initialized client for {ActorId} with public key ID {PublicKeyId}", 
                    _options.ActorId, _options.PublicKeyId);
                _logger.LogDebug("Client is now authenticated: {IsAuthenticated}", _options.IsAuthenticated);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Failed to initialize client: Actor {_options.ActorId} response did not include privateKeyPem. " +
                    "Ensure the API key is valid and the server is configured with a matching AdminApiToken.");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch actor credentials from {ActorId}", _options.ActorId);
            throw new InvalidOperationException($"Failed to initialize client: unable to fetch actor from {_options.ActorId}", ex);
        }
        catch (InvalidOperationException)
        {
            // Re-throw InvalidOperationException as-is (already has proper message)
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error initializing client for {ActorId}", _options.ActorId);
            throw new InvalidOperationException($"Failed to initialize client: {ex.Message}", ex);
        }
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
            else if (items.ValueKind == JsonValueKind.Object)
            {
                // Handle malformed response where orderedItems/items is a single object instead of array
                _logger.LogWarning("Collection returned a single object instead of an array. This is non-standard but will be handled.");
                
                if (limit.HasValue && itemCount >= limit.Value)
                {
                    yield break;
                }

                T? deserializedItem = default;
                try
                {
                    deserializedItem = JsonSerializer.Deserialize<T>(items.GetRawText(), _jsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize collection item");
                }

                if (deserializedItem != null)
                {
                    yield return deserializedItem;
                    itemCount++;
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

        _logger.LogDebug("Signing request to {Uri} with key ID {KeyId}", request.RequestUri, _options.PublicKeyId);

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
        
        _logger.LogDebug("Request signed successfully");
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
