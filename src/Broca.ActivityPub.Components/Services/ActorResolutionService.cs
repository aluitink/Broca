using Broca.ActivityPub.Core.Interfaces;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Logging;

namespace Broca.ActivityPub.Components.Services;

/// <summary>
/// Service for resolving actor information from URIs with in-flight request deduplication
/// </summary>
public class ActorResolutionService
{
    private readonly IActivityPubClient _client;
    private readonly ILogger<ActorResolutionService> _logger;
    private readonly Dictionary<string, Task<ActorInfo?>> _inFlightRequests = new();

    public ActorResolutionService(IActivityPubClient client, ILogger<ActorResolutionService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolves an actor from a URI or link object
    /// </summary>
    public async Task<ActorInfo?> ResolveActorAsync(IObjectOrLink? actorLink, CancellationToken cancellationToken = default)
    {
        if (actorLink == null)
            return null;

        Uri? actorUri = null;

        // Extract URI from different types
        if (actorLink is KristofferStrube.ActivityStreams.Object obj && !string.IsNullOrEmpty(obj.Id))
        {
            actorUri = new Uri(obj.Id);
            
            // If already an Actor/Person, extract info directly
            if (obj is Actor actor)
            {
                return CreateActorInfo(actor);
            }
        }
        else if (actorLink is Link link && link.Href != null)
        {
            actorUri = link.Href;
        }

        if (actorUri == null)
            return null;

        var uriString = actorUri.ToString();

        // Check for in-flight request to avoid duplicate concurrent fetches
        Task<ActorInfo?>? existingTask = null;
        lock (_inFlightRequests)
        {
            if (_inFlightRequests.TryGetValue(uriString, out existingTask))
            {
                _logger.LogDebug("Reusing in-flight request for {ActorUri}", actorUri);
            }
        }

        if (existingTask != null)
        {
            return await existingTask;
        }

        // Create new fetch task
        var fetchTask = FetchActorAsync(actorUri, cancellationToken);

        // Store in-flight request
        lock (_inFlightRequests)
        {
            _inFlightRequests[uriString] = fetchTask;
        }

        try
        {
            return await fetchTask;
        }
        finally
        {
            // Clean up in-flight request
            lock (_inFlightRequests)
            {
                _inFlightRequests.Remove(uriString);
            }
        }
    }

    private async Task<ActorInfo?> FetchActorAsync(Uri actorUri, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Fetching actor from {ActorUri}", actorUri);
            var fetchedActor = await _client.GetActorAsync(actorUri, cancellationToken);
            return CreateActorInfo(fetchedActor);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve actor from {ActorUri}", actorUri);
            return null;
        }
    }

    /// <summary>
    /// Resolves multiple actors in parallel
    /// </summary>
    public async Task<Dictionary<string, ActorInfo>> ResolveActorsAsync(
        IEnumerable<IObjectOrLink> actorLinks, 
        CancellationToken cancellationToken = default)
    {
        var tasks = actorLinks
            .Select(async link => (Link: link, Info: await ResolveActorAsync(link, cancellationToken)))
            .ToList();

        await Task.WhenAll(tasks);

        var results = new Dictionary<string, ActorInfo>();
        foreach (var task in tasks)
        {
            var (link, info) = await task;
            if (info != null && link is KristofferStrube.ActivityStreams.Object obj && !string.IsNullOrEmpty(obj.Id))
            {
                results[obj.Id] = info;
            }
        }

        return results;
    }

    /// <summary>
    /// Clears any in-flight requests (useful for testing or reset scenarios)
    /// </summary>
    public void ClearInFlightRequests()
    {
        lock (_inFlightRequests)
        {
            _inFlightRequests.Clear();
        }
    }

    private ActorInfo CreateActorInfo(Actor actor)
    {
        var preferredUsername = actor.PreferredUsername ?? "unknown";
        var name = actor.Name?.FirstOrDefault() ?? preferredUsername;
        var iconUrl = GetIconUrl(actor);
        var handle = GetHandle(actor);

        return new ActorInfo
        {
            Id = actor.Id ?? string.Empty,
            PreferredUsername = preferredUsername,
            Name = name,
            Handle = handle,
            IconUrl = iconUrl,
            Summary = actor.Summary?.FirstOrDefault(),
            Actor = actor
        };
    }

    private string? GetIconUrl(Actor actor)
    {
        if (actor is Person person && person.Icon?.Any() == true)
        {
            var icon = person.Icon.First();
            if (icon is Image img && img.Url?.Any() == true)
            {
                var url = img.Url.First();
                return url.Href?.ToString();
            }
        }
        return null;
    }

    private string GetHandle(Actor actor)
    {
        var username = actor.PreferredUsername ?? "unknown";
        
        if (!string.IsNullOrEmpty(actor.Id) && Uri.TryCreate(actor.Id, UriKind.Absolute, out var uri))
        {
            return $"@{username}@{uri.Host}";
        }
        
        return $"@{username}";
    }
}

/// <summary>
/// Resolved actor information for display
/// </summary>
public class ActorInfo
{
    public required string Id { get; set; }
    public required string PreferredUsername { get; set; }
    public required string Name { get; set; }
    public required string Handle { get; set; }
    public string? IconUrl { get; set; }
    public string? Summary { get; set; }
    public Actor? Actor { get; set; }
}
