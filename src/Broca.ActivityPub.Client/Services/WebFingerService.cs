using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;

namespace Broca.ActivityPub.Client.Services;

/// <summary>
/// Implementation of WebFinger protocol for user discovery
/// </summary>
/// <remarks>
/// WebFinger (RFC 7033) is used to discover information about users
/// in the fediverse, typically converting handles like @user@domain.tld
/// to ActivityPub actor URLs.
/// </remarks>
public class WebFingerService : IWebFingerService
{
    private readonly ILogger<WebFingerService> _logger;

    public WebFingerService(ILogger<WebFingerService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public virtual async Task<WebFingerResource> WebFingerUserByAliasAsync(
        HttpClient client,
        string userAlias,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(userAlias);

        _logger.LogInformation("WebFingerUserByAliasAsync({UserAlias})", userAlias);

        // Normalize the user alias
        userAlias = userAlias.TrimStart('@');
        var userParts = userAlias.Split('@');

        if (userParts.Length < 2)
        {
            throw new ArgumentException("UserAlias should be in '@user@domain.tld' or 'user@domain.tld' format.", nameof(userAlias));
        }

        var targetUri = BuildWebFingerUri(userAlias);

        try
        {
            var resource = await client.GetFromJsonAsync<WebFingerResource>(targetUri, cancellationToken);
            
            if (resource == null)
            {
                throw new InvalidOperationException($"WebFinger request returned null for {userAlias}");
            }

            return resource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve WebFinger for {UserAlias}", userAlias);
            throw;
        }
    }

    /// <summary>
    /// Builds a WebFinger URI from a user resource identifier
    /// </summary>
    /// <param name="resource">User identifier (user@domain.tld)</param>
    /// <returns>WebFinger endpoint URI</returns>
    protected Uri BuildWebFingerUri(string resource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);

        if (resource.StartsWith("@"))
        {
            resource = resource.TrimStart('@');
        }

        if (!resource.Contains('@'))
        {
            throw new ArgumentException("Resource must contain '@'", nameof(resource));
        }

        if (!resource.Contains('.'))
        {
            throw new ArgumentException("Resource must contain a domain with '.'", nameof(resource));
        }

        string userDomain = resource.Split('@')[1];

        return new Uri($"https://{userDomain}/.well-known/webfinger?resource=acct:{resource}");
    }
}
