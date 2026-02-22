using Broca.ActivityPub.Client.Services;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Server.Services;

/// <summary>
/// Factory for creating ActivityBuilder instances on the server side
/// </summary>
public class ActivityBuilderFactory : IActivityBuilderFactory
{
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<ActivityBuilder> _logger;

    public ActivityBuilderFactory(
        IOptions<ActivityPubServerOptions> options,
        ILogger<ActivityBuilder> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates an activity builder for a given actor
    /// </summary>
    /// <param name="actorId">The full actor ID (e.g., https://example.com/users/alice)</param>
    /// <returns>Activity builder anchored to the actor</returns>
    public IActivityBuilder CreateForActor(string actorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        // Extract base URL from actor ID or use configured base URL
        var baseUrl = _options.BaseUrl?.TrimEnd('/') ?? "http://localhost";
        var routePrefix = _options.NormalizedRoutePrefix;
        var baseUrlWithPrefix = $"{baseUrl}{routePrefix}";

        return new ActivityBuilder(actorId, baseUrlWithPrefix, _logger);
    }

    /// <summary>
    /// Creates an activity builder for a username on this server
    /// </summary>
    /// <param name="username">The username (e.g., alice)</param>
    /// <returns>Activity builder anchored to the local actor</returns>
    public IActivityBuilder CreateForUsername(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var baseUrl = _options.BaseUrl?.TrimEnd('/') ?? "http://localhost";
        var routePrefix = _options.NormalizedRoutePrefix;
        var actorId = $"{baseUrl}{routePrefix}/users/{username}";
        var baseUrlWithPrefix = $"{baseUrl}{routePrefix}";

        return new ActivityBuilder(actorId, baseUrlWithPrefix, _logger);
    }

    /// <summary>
    /// Creates an activity builder for the system actor
    /// </summary>
    /// <returns>Activity builder anchored to the system actor</returns>
    public IActivityBuilder CreateForSystemActor()
    {
        return CreateForUsername(_options.SystemActorUsername ?? "sys");
    }
}
