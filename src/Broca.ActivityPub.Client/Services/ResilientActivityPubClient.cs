using Broca.ActivityPub.Core.Interfaces;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Broca.ActivityPub.Client.Services;

/// <summary>
/// Wrapper around ActivityPubClient that provides CORS fallback via proxy
/// </summary>
public class ResilientActivityPubClient : IActivityPubClient
{
    private readonly IActivityPubClient _innerClient;
    private readonly ProxyService _proxyService;
    private readonly ILogger<ResilientActivityPubClient> _logger;

    public ResilientActivityPubClient(
        IActivityPubClient innerClient,
        ProxyService proxyService,
        ILogger<ResilientActivityPubClient> logger)
    {
        _innerClient = innerClient;
        _proxyService = proxyService;
        _logger = logger;
    }

    public string? ActorId => _innerClient.ActorId;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
        => _innerClient.InitializeAsync(cancellationToken);

    public Task<Actor?> GetSelfAsync(CancellationToken cancellationToken = default)
        => _innerClient.GetSelfAsync(cancellationToken);

    public async Task<Actor> GetActorAsync(Uri actorUri, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _innerClient.GetActorAsync(actorUri, cancellationToken);
        }
        catch (HttpRequestException ex) when (IsCorsError(ex))
        {
            _logger.LogWarning("CORS error fetching actor, attempting via proxy: {Uri}", actorUri);
            var result = await _proxyService.GetViaProxyAsync<Actor>(actorUri, cancellationToken);
            return result ?? throw new InvalidOperationException($"Actor not found at {actorUri}");
        }
    }

    public async Task<Actor> GetActorByAliasAsync(string alias, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _innerClient.GetActorByAliasAsync(alias, cancellationToken);
        }
        catch (HttpRequestException ex) when (IsCorsError(ex))
        {
            _logger.LogWarning("CORS error resolving actor alias, will let inner client handle it: {Alias}", alias);
            throw; // Can't easily proxy WebFinger, let the error propagate
        }
    }

    public async Task<T?> GetAsync<T>(Uri uri, bool useCache = true, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _innerClient.GetAsync<T>(uri, useCache, cancellationToken);
        }
        catch (HttpRequestException ex) when (IsCorsError(ex))
        {
            _logger.LogWarning("CORS error fetching resource, attempting via proxy: {Uri}", uri);
            return await _proxyService.GetViaProxyAsync<T>(uri, cancellationToken);
        }
    }

    public async Task<HttpResponseMessage> PostAsync<T>(Uri uri, T obj, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _innerClient.PostAsync(uri, obj, cancellationToken);
        }
        catch (HttpRequestException ex) when (IsCorsError(ex))
        {
            _logger.LogWarning("CORS error posting to resource, attempting via proxy: {Uri}", uri);
            return await _proxyService.PostViaProxyAsync(uri, obj, cancellationToken);
        }
    }

    public async IAsyncEnumerable<T> GetCollectionAsync<T>(
        Uri collectionUri,
        int? limit = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // For collection enumeration, we'll let the inner client handle it
        // If individual pages fail with CORS, they'll be caught by GetAsync above
        await foreach (var item in _innerClient.GetCollectionAsync<T>(collectionUri, limit, cancellationToken))
        {
            yield return item;
        }
    }

    public IActivityBuilder CreateActivityBuilder()
        => _innerClient.CreateActivityBuilder();

    public Task<HttpResponseMessage> PostToOutboxAsync(Activity activity, CancellationToken cancellationToken = default)
        => _innerClient.PostToOutboxAsync(activity, cancellationToken);

    /// <summary>
    /// Determines if an HTTP exception is likely caused by CORS
    /// </summary>
    private bool IsCorsError(HttpRequestException ex)
    {
        // In browser environments, CORS errors often manifest as connection failures
        // The inner exception might contain more details
        var message = ex.Message?.ToLowerInvariant() ?? "";
        
        return message.Contains("cors") ||
               message.Contains("blocked") ||
               message.Contains("cross-origin") ||
               message.Contains("access-control") ||
               // In Blazor WebAssembly, CORS errors sometimes appear as generic network errors
               (ex.InnerException != null && ex.InnerException.Message.Contains("Failed to fetch"));
    }
}
