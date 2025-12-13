using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Server.Services;

/// <summary>
/// Service for WebFinger protocol support
/// </summary>
public class WebFingerService
{
    private readonly IActorRepository _actorRepository;
    private readonly IdentityProviderService? _identityProviderService;
    private readonly ILogger<WebFingerService> _logger;
    private readonly ActivityPubServerOptions _options;

    public WebFingerService(
        IActorRepository actorRepository,
        ILogger<WebFingerService> logger,
        IOptions<ActivityPubServerOptions> options,
        IdentityProviderService? identityProviderService = null)
    {
        _actorRepository = actorRepository;
        _identityProviderService = identityProviderService;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Handles WebFinger resource lookup
    /// </summary>
    public async Task<object?> GetResourceAsync(string resource, CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse resource (acct:username@domain or https://domain/users/username)
            var username = ParseResourceToUsername(resource);
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Could not parse username from resource: {Resource}", resource);
                return null;
            }

            // Check if actor exists, or try to create from identity provider
            var actor = await _actorRepository.GetActorByUsernameAsync(username, cancellationToken);
            if (actor == null && _identityProviderService != null)
            {
                actor = await _identityProviderService.GetOrCreateActorAsync(username, cancellationToken);
            }
            
            if (actor == null)
            {
                _logger.LogWarning("Actor not found for username: {Username}", username);
                return null;
            }

            // Extract domain from base URL
            var domain = new Uri(_options.BaseUrl).Host;
            var baseUrl = _options.BaseUrl.TrimEnd('/');
            var routePrefix = _options.NormalizedRoutePrefix;

            // Return WebFinger response
            return new
            {
                subject = $"acct:{username}@{domain}",
                links = new[]
                {
                    new
                    {
                        rel = "self",
                        type = "application/activity+json",
                        href = $"{baseUrl}{routePrefix}/users/{username}"
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting WebFinger resource for {Resource}", resource);
            return null;
        }
    }

    private string? ParseResourceToUsername(string resource)
    {
        // Handle acct:username@domain format
        if (resource.StartsWith("acct:", StringComparison.OrdinalIgnoreCase))
        {
            var acct = resource.Substring(5); // Remove "acct:"
            var parts = acct.Split('@');
            return parts.Length > 0 ? parts[0] : null;
        }

        // Handle https://domain/users/username or https://domain/prefix/users/username format
        if (Uri.TryCreate(resource, UriKind.Absolute, out var uri))
        {
            var segments = uri.Segments;
            // Find "users" segment
            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (segments[i].TrimEnd('/') == "users")
                {
                    return segments[i + 1].TrimEnd('/');
                }
            }
        }

        return null;
    }
}
