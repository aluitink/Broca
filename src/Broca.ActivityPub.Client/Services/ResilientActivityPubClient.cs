using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
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
        catch (Exception ex) when (ex is HttpRequestException || IsCorsLikeError(ex))
        {
            _logger.LogWarning("Direct WebFinger lookup failed for {Alias}, attempting via proxy", alias);
            return await ResolveViaWebFingerProxyAsync(alias, cancellationToken);
        }
    }

    private async Task<Actor> ResolveViaWebFingerProxyAsync(string alias, CancellationToken cancellationToken)
    {
        alias = alias.TrimStart('@');
        var parts = alias.Split('@', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            throw new InvalidOperationException($"Cannot resolve alias via proxy: invalid format '{alias}'");

        var webFingerUrl = new Uri(
            $"https://{parts[1]}/.well-known/webfinger?resource=acct:{Uri.EscapeDataString(parts[0])}@{parts[1]}");

        var resource = await _proxyService.GetViaProxyAsync<WebFingerResource>(webFingerUrl, cancellationToken)
            ?? throw new InvalidOperationException($"WebFinger proxy returned no result for: {alias}");

        var actorLink = resource.Links?.FirstOrDefault(l =>
            l.Rel == "self" &&
            !string.IsNullOrEmpty(l.Href) &&
            (l.Type == "application/activity+json" ||
             l.Type?.StartsWith("application/ld+json") == true));

        if (actorLink?.Href == null || !Uri.TryCreate(actorLink.Href, UriKind.Absolute, out var actorUri))
            throw new InvalidOperationException($"No ActivityPub profile found for alias: {alias}");

        return await GetActorAsync(actorUri, cancellationToken);
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

    public IAsyncEnumerable<T> GetCollectionAsync<T>(
        Uri collectionUri,
        CollectionSearchParameters search,
        int? limit = null,
        CancellationToken cancellationToken = default)
        => _innerClient.GetCollectionAsync<T>(collectionUri, search, limit, cancellationToken);

    public IActivityBuilder CreateActivityBuilder()
        => _innerClient.CreateActivityBuilder();

    public Task<HttpResponseMessage> PostToOutboxAsync(Activity activity, CancellationToken cancellationToken = default)
        => _innerClient.PostToOutboxAsync(activity, cancellationToken);

    /// <summary>
    /// Determines if an HTTP exception is likely caused by CORS or a network-level block
    /// </summary>
    private bool IsCorsError(HttpRequestException ex) => IsCorsLikeError(ex);

    private static bool IsCorsLikeError(Exception ex)
    {
        var message = ex.Message?.ToLowerInvariant() ?? "";
        return message.Contains("cors") ||
               message.Contains("blocked") ||
               message.Contains("cross-origin") ||
               message.Contains("access-control") ||
               message.Contains("failed to fetch") ||
               (ex.InnerException != null && IsCorsLikeError(ex.InnerException));
    }
}
