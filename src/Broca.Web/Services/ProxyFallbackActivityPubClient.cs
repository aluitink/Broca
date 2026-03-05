using Broca.ActivityPub.Client.Services;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Broca.Web.Services;

public class ProxyFallbackActivityPubClient : ActivityPubClient
{
    private readonly ProxyService _proxyService;
    private readonly ILogger<ProxyFallbackActivityPubClient> _logger;

    public ProxyFallbackActivityPubClient(
        IHttpClientFactory httpClientFactory,
        IWebFingerService webFingerService,
        HttpSignatureService signatureService,
        IOptions<ActivityPubClientOptions> options,
        ILogger<ActivityPubClient> baseLogger,
        ILogger<ProxyFallbackActivityPubClient> logger,
        ProxyService proxyService)
        : base(httpClientFactory, webFingerService, signatureService, options, baseLogger)
    {
        _proxyService = proxyService;
        _logger = logger;
    }

    public override async Task<T?> GetAsync<T>(Uri uri, bool useCache = true, CancellationToken cancellationToken = default) where T : default
    {
        try
        {
            var result = await base.GetAsync<T>(uri, useCache, cancellationToken);
            if (result is not null)
                return result;

            _logger.LogInformation("Direct fetch returned null, retrying via proxy: {Uri}", uri);
            return await _proxyService.GetViaProxyAsync<T>(uri, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Direct fetch failed, retrying via proxy: {Uri}", uri);
            return await _proxyService.GetViaProxyAsync<T>(uri, cancellationToken);
        }
    }

    public override async Task<Actor> GetActorByAliasAsync(string alias, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.GetActorByAliasAsync(alias, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Direct alias lookup failed for {Alias}, retrying via proxy", alias);
            return await ResolveActorByAliasViaProxyAsync(alias, cancellationToken);
        }
    }

    private async Task<Actor> ResolveActorByAliasViaProxyAsync(string alias, CancellationToken cancellationToken)
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

        return await GetAsync<Actor>(actorUri, useCache: true, cancellationToken)
            ?? throw new InvalidOperationException($"Actor not found at {actorUri}");
    }
}
