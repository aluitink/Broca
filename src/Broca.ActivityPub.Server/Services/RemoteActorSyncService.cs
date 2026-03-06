using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Server.Services;

public class RemoteActorSyncService : IRemoteActorSyncService
{
    private const string PublicAddress = "https://www.w3.org/ns/activitystreams#Public";
    private const int PageSize = 20;

    private readonly SignedClientProvider _signedClientProvider;
    private readonly IActivityRepository _activityRepository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<RemoteActorSyncService> _logger;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public RemoteActorSyncService(
        SignedClientProvider signedClientProvider,
        IActivityRepository activityRepository,
        IBlobStorageService blobStorage,
        IHttpClientFactory httpClientFactory,
        IOptions<ActivityPubServerOptions> options,
        ILogger<RemoteActorSyncService> logger)
    {
        _signedClientProvider = signedClientProvider;
        _activityRepository = activityRepository;
        _blobStorage = blobStorage;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public Task SyncActorAsync(string actorId, CancellationToken cancellationToken = default)
        => SyncActorAsync(actorId, new RemoteActorSyncOptions(), cancellationToken);

    public async Task SyncActorAsync(string actorId, RemoteActorSyncOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return;

        var lastSync = await GetLastSyncTimeAsync(actorId, cancellationToken);
        if (lastSync.HasValue && DateTimeOffset.UtcNow - lastSync.Value < options.MinSyncInterval)
        {
            _logger.LogDebug("Skipping sync for {ActorId}: last sync was {LastSync}", actorId, lastSync.Value);
            return;
        }

        _logger.LogInformation("Starting sync for remote actor {ActorId}", actorId);

        Actor actor;
        try
        {
            var client = await _signedClientProvider.CreateForSystemActorAsync(cancellationToken);
            actor = await client.GetActorAsync(new Uri(actorId), cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
        {
            _logger.LogInformation("Actor {ActorId} is gone (410), skipping sync", actorId);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch actor {ActorId} for sync", actorId);
            return;
        }

        await StoreActorJsonAsync(actorId, actor, cancellationToken);

        if (options.SyncMedia)
            await SyncMediaAsync(actorId, actor, cancellationToken);

        if (options.OutboxPageLimit > 0 && actor.Outbox?.Href != null)
            await SyncOutboxAsync(actorId, actor.Outbox.Href, options.OutboxPageLimit, cancellationToken);

        if (options.SyncFollowers && actor.Followers?.Href != null)
            await SyncCollectionIdsAsync(actorId, actor.Followers.Href, "followers", cancellationToken);

        if (options.SyncFollowing && actor.Following?.Href != null)
            await SyncCollectionIdsAsync(actorId, actor.Following.Href, "following", cancellationToken);

        await SaveSyncStateAsync(actorId, cancellationToken);

        _logger.LogInformation("Completed sync for remote actor {ActorId}", actorId);
    }

    public async Task<DateTimeOffset?> GetLastSyncTimeAsync(string actorId, CancellationToken cancellationToken = default)
    {
        var blobKey = GetSyncStateBlobKey(actorId);
        var blob = await _blobStorage.GetBlobAsync(_options.SystemActorUsername, blobKey, cancellationToken);
        if (!blob.HasValue) return null;

        try
        {
            using var reader = new StreamReader(blob.Value.Content);
            var json = await reader.ReadToEndAsync(cancellationToken);
            var state = JsonSerializer.Deserialize<SyncState>(json, _jsonOptions);
            return state?.SyncedAt;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read sync state for {ActorId}", actorId);
            return null;
        }
    }

    private async Task StoreActorJsonAsync(string actorId, Actor actor, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize<Actor>(actor, _jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _blobStorage.StoreBlobAsync(
                _options.SystemActorUsername,
                GetActorBlobKey(actorId),
                new MemoryStream(bytes),
                "application/json",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store actor JSON for {ActorId}", actorId);
        }
    }

    private async Task SyncMediaAsync(string actorId, Actor actor, CancellationToken cancellationToken)
    {
        var iconUrl = GetIconUrl(actor);
        if (!string.IsNullOrEmpty(iconUrl))
            await CacheImageAsync(actorId, iconUrl, "icon", cancellationToken);

        var imageUrl = GetImageUrl(actor);
        if (!string.IsNullOrEmpty(imageUrl))
            await CacheImageAsync(actorId, imageUrl, "image", cancellationToken);
    }

    private async Task CacheImageAsync(string actorId, string imageUrl, string type, CancellationToken cancellationToken)
    {
        var blobKey = GetMediaBlobKey(actorId, type);
        if (await _blobStorage.BlobExistsAsync(_options.SystemActorUsername, blobKey, cancellationToken))
            return;

        try
        {
            using var http = _httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            var response = await http.GetAsync(imageUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Remote {Type} image {Url} returned {Status}", type, imageUrl, response.StatusCode);
                return;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            await _blobStorage.StoreBlobAsync(_options.SystemActorUsername, blobKey, new MemoryStream(bytes), contentType, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache {Type} image for {ActorId}", type, actorId);
        }
    }

    private async Task SyncOutboxAsync(string actorId, Uri outboxUri, int pageLimit, CancellationToken cancellationToken)
    {
        var sysUsername = _options.SystemActorUsername;
        var itemLimit = pageLimit * PageSize;
        var count = 0;

        try
        {
            var client = await _signedClientProvider.CreateForSystemActorAsync(cancellationToken);
            await foreach (var item in client.GetCollectionAsync<IObjectOrLink>(outboxUri, itemLimit, cancellationToken))
            {
                if (item is not IObject obj)
                    continue;

                if (!IsPublic(obj))
                    continue;

                var activityId = obj.Id ?? Guid.NewGuid().ToString();
                await _activityRepository.SaveInboxActivityAsync(sysUsername, activityId, item, cancellationToken);
                count++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync outbox for {ActorId}", actorId);
        }

        _logger.LogDebug("Synced {Count} public outbox activities for {ActorId}", count, actorId);
    }

    private async Task SyncCollectionIdsAsync(string actorId, Uri collectionUri, string label, CancellationToken cancellationToken)
    {
        var ids = new List<string>();

        try
        {
            var client = await _signedClientProvider.CreateForSystemActorAsync(cancellationToken);
            await foreach (var item in client.GetCollectionAsync<IObjectOrLink>(collectionUri, cancellationToken: cancellationToken))
            {
                var id = item switch
                {
                    ILink link => link.Href?.ToString(),
                    IObject obj => obj.Id,
                    _ => null
                };

                if (!string.IsNullOrEmpty(id))
                    ids.Add(id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync {Label} collection for {ActorId}", label, actorId);
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(ids, _jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _blobStorage.StoreBlobAsync(
                _options.SystemActorUsername,
                GetCollectionBlobKey(actorId, label),
                new MemoryStream(bytes),
                "application/json",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store {Label} collection for {ActorId}", label, actorId);
        }

        _logger.LogDebug("Synced {Count} {Label} for {ActorId}", ids.Count, label, actorId);
    }

    private async Task SaveSyncStateAsync(string actorId, CancellationToken cancellationToken)
    {
        var state = new SyncState { SyncedAt = DateTimeOffset.UtcNow };
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _blobStorage.StoreBlobAsync(
            _options.SystemActorUsername,
            GetSyncStateBlobKey(actorId),
            new MemoryStream(bytes),
            "application/json",
            cancellationToken);
    }

    private static bool IsPublic(IObject obj)
    {
        if (obj is not KristofferStrube.ActivityStreams.Object asObj)
            return false;
        var to = asObj.To?.OfType<ILink>().Select(l => l.Href?.ToString()).Where(h => h != null);
        var cc = asObj.Cc?.OfType<ILink>().Select(l => l.Href?.ToString()).Where(h => h != null);
        var all = (to ?? Enumerable.Empty<string?>()).Concat(cc ?? Enumerable.Empty<string?>());
        return all.Any(href => href == PublicAddress);
    }

    private static string? GetIconUrl(Actor actor)
    {
        if (actor.Icon?.Any() != true) return null;
        var icon = actor.Icon.First();
        if (icon is Image img && img.Url?.Any() == true)
            return img.Url.First().Href?.ToString();
        if (icon is ILink link)
            return link.Href?.ToString();
        return null;
    }

    private static string? GetImageUrl(Actor actor)
    {
        if (actor.Image?.Any() != true) return null;
        var image = actor.Image.First();
        if (image is Image img && img.Url?.Any() == true)
            return img.Url.First().Href?.ToString();
        if (image is ILink link)
            return link.Href?.ToString();
        return null;
    }

    private static string GetActorHash(string actorId)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(actorId))).ToLowerInvariant();

    private static string GetActorBlobKey(string actorId)
        => $"remote-actors/{GetActorHash(actorId)}/actor.json";

    private static string GetSyncStateBlobKey(string actorId)
        => $"remote-actors/{GetActorHash(actorId)}/sync.json";

    private static string GetMediaBlobKey(string actorId, string type)
        => $"actor-media/{GetActorHash(actorId)}/{type}";

    private static string GetCollectionBlobKey(string actorId, string label)
        => $"remote-actors/{GetActorHash(actorId)}/{label}.json";

    private sealed class SyncState
    {
        public DateTimeOffset SyncedAt { get; set; }
    }
}
