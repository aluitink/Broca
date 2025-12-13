using System.Net.Http.Json;
using System.Text.Json;
using Broca.ActivityPub.Client.Services;
using Broca.ActivityPub.Core.Interfaces;
using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.IntegrationTests.Infrastructure;

/// <summary>
/// Helper for Client-to-Server (C2S) ActivityPub interactions
/// Handles authenticated posting to outboxes and other C2S operations
/// </summary>
public class ClientToServerHelper
{
    private readonly IActivityPubClient _client;
    private readonly string _actorId;
    private readonly HttpClient _httpClient;

    public ClientToServerHelper(IActivityPubClient client, string actorId, HttpClient httpClient)
    {
        _client = client;
        _actorId = actorId;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Posts an activity to the authenticated user's outbox
    /// The server will assign a unique ID and store it
    /// </summary>
    /// <param name="activity">The activity to post</param>
    /// <returns>The HTTP response from the post</returns>
    public async Task<HttpResponseMessage> PostToOutboxAsync(Activity activity)
    {
        // Use the client's PostToOutboxAsync method
        var response = await _client.PostToOutboxAsync(activity);
        
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to post to outbox: {response.StatusCode} - {content}");
        }

        return response;
    }

    /// <summary>
    /// Gets activities from the user's outbox
    /// </summary>
    public async Task<IEnumerable<IObjectOrLink>> GetOutboxAsync(int limit = 20)
    {
        // Fetch the actor to get the actual outbox URL
        var actor = await _client.GetActorAsync(new Uri(_actorId));
        if (actor?.Outbox == null)
        {
            throw new InvalidOperationException($"Actor {_actorId} has no outbox");
        }

        var outboxUrl = actor.Outbox switch
        {
            Link link => link.Href,
            KristofferStrube.ActivityStreams.Object obj => obj.Id != null ? new Uri(obj.Id) : null,
            _ => null
        };

        if (outboxUrl == null)
        {
            throw new InvalidOperationException($"Could not determine outbox URL for actor {_actorId}");
        }

        var items = new List<IObjectOrLink>();
        await foreach (var item in _client.GetCollectionAsync<IObjectOrLink>(outboxUrl, limit))
        {
            items.Add(item);
        }
        
        return items;
    }

    /// <summary>
    /// Gets activities from the user's inbox
    /// </summary>
    public async Task<IEnumerable<IObjectOrLink>> GetInboxAsync(int limit = 20)
    {
        // Fetch the actor to get the actual inbox URL
        var actor = await _client.GetActorAsync(new Uri(_actorId));
        if (actor?.Inbox == null)
        {
            throw new InvalidOperationException($"Actor {_actorId} has no inbox");
        }

        var inboxUrl = actor.Inbox switch
        {
            Link link => link.Href,
            KristofferStrube.ActivityStreams.Object obj => obj.Id != null ? new Uri(obj.Id) : null,
            _ => null
        };

        if (inboxUrl == null)
        {
            throw new InvalidOperationException($"Could not determine inbox URL for actor {_actorId}");
        }

        var items = new List<IObjectOrLink>();
        await foreach (var item in _client.GetCollectionAsync<IObjectOrLink>(inboxUrl, limit))
        {
            items.Add(item);
        }
        
        return items;
    }

    private static string ExtractUsername(string actorId)
    {
        // Extract username from actor ID like "https://server-a.test/users/alice"
        var uri = new Uri(actorId);
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Last();
    }

    private static string GetBaseUrl(string actorId)
    {
        var uri = new Uri(actorId);
        return $"{uri.Scheme}://{uri.Authority}";
    }
}
