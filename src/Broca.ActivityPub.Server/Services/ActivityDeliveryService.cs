using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Broca.ActivityPub.Client.Services;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Microsoft.Extensions.Options;
using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Server.Services;

/// <summary>
/// Service responsible for delivering activities to remote inboxes
/// </summary>
public class ActivityDeliveryService
{
    private readonly IDeliveryQueueRepository _deliveryQueue;
    private readonly IActorRepository _actorRepository;
    private readonly IActivityRepository _activityRepository;
    private readonly IActivityPubClient _activityPubClient;
    private readonly HttpSignatureService _signatureService;
    private readonly ICryptoProvider _cryptoProvider;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<ActivityDeliveryService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public ActivityDeliveryService(
        IDeliveryQueueRepository deliveryQueue,
        IActorRepository actorRepository,
        IActivityRepository activityRepository,
        IActivityPubClient activityPubClient,
        HttpSignatureService signatureService,
        ICryptoProvider cryptoProvider,
        IOptions<ActivityPubServerOptions> options,
        ILogger<ActivityDeliveryService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _deliveryQueue = deliveryQueue;
        _actorRepository = actorRepository;
        _activityRepository = activityRepository;
        _activityPubClient = activityPubClient;
        _signatureService = signatureService;
        _cryptoProvider = cryptoProvider;
        _options = options.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Queues an activity for delivery to a specific target actor's inbox
    /// Used for directed activities like Follow, Like, Announce, etc.
    /// </summary>
    public async Task QueueActivityToTargetAsync(
        string senderUsername,
        string activityId,
        IObjectOrLink activity,
        string targetActorId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableActivityDelivery)
        {
            _logger.LogDebug("Activity delivery is disabled. Skipping delivery for {ActivityId}", activityId);
            return;
        }

        var senderActor = await _actorRepository.GetActorByUsernameAsync(senderUsername, cancellationToken);
        if (senderActor == null)
        {
            _logger.LogWarning("Cannot queue activity for delivery: sender actor {Username} not found", senderUsername);
            return;
        }

        var senderActorId = senderActor.Id;
        if (string.IsNullOrEmpty(senderActorId))
        {
            _logger.LogWarning("Cannot queue activity for delivery: sender actor {Username} has no ID", senderUsername);
            return;
        }

        _logger.LogInformation("Queueing activity {ActivityId} for direct delivery to {TargetActorId}",
            activityId, targetActorId);

        // Attempt to resolve the target actor's inbox now. If resolution fails the item is still
        // enqueued — the inbox will be resolved again on each delivery attempt.
        string inboxUrl = "";
        try
        {
            var targetActor = await _activityPubClient.GetActorAsync(new Uri(targetActorId), cancellationToken);
            inboxUrl = targetActor?.Inbox?.Href?.ToString() ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve actor {TargetActorId} at queue time; inbox will be resolved at delivery time", targetActorId);
        }

        var deliveryItem = new DeliveryQueueItem
        {
            Activity = activity,
            InboxUrl = inboxUrl,
            TargetActorId = targetActorId,
            SenderActorId = senderActorId,
            SenderUsername = senderUsername,
            Status = DeliveryStatus.Pending,
            MaxRetries = 5
        };

        await _deliveryQueue.EnqueueAsync(deliveryItem, cancellationToken);
        _logger.LogInformation("Queued direct delivery for activity {ActivityId} to {TargetActorId}", activityId, targetActorId);
    }

    /// <summary>
    /// Queues an activity for delivery to all followers of the sender
    /// </summary>
    public async Task QueueActivityForDeliveryAsync(
        string senderUsername, 
        string activityId, 
        IObjectOrLink activity, 
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableActivityDelivery)
        {
            _logger.LogDebug("Activity delivery is disabled. Skipping delivery for {ActivityId}", activityId);
            return;
        }

        try
        {
            // Get sender actor
            var senderActor = await _actorRepository.GetActorByUsernameAsync(senderUsername, cancellationToken);
            if (senderActor == null)
            {
                _logger.LogWarning("Cannot queue activity for delivery: sender actor {Username} not found", senderUsername);
                return;
            }

            var senderActorId = senderActor.Id;
            if (string.IsNullOrEmpty(senderActorId))
            {
                _logger.LogWarning("Cannot queue activity for delivery: sender actor {Username} has no ID", senderUsername);
                return;
            }

            // Get followers
            var followers = await _actorRepository.GetFollowersAsync(senderUsername, cancellationToken);
            
            if (!followers.Any())
            {
                _logger.LogDebug("No followers found for {Username}. Skipping delivery.", senderUsername);
                return;
            }

            _logger.LogInformation("Queueing activity {ActivityId} for delivery to {FollowerCount} followers", 
                activityId, followers.Count());

            // key: shared inbox URL (or personal inbox if no shared inbox); value: list of (followerId, personalInbox)
            var inboxGroups = new Dictionary<string, List<(string FollowerId, string PersonalInbox)>>();

            foreach (var followerActorId in followers)
            {
                try
                {
                    var followerActor = await _activityPubClient.GetActorAsync(new Uri(followerActorId), cancellationToken);

                    if (followerActor == null)
                    {
                        _logger.LogWarning("Follower {FollowerActorId} could not be fetched. Skipping.", followerActorId);
                        continue;
                    }

                    var personalInbox = followerActor.Inbox?.Href?.ToString();

                    if (string.IsNullOrEmpty(personalInbox))
                    {
                        _logger.LogWarning("Could not determine inbox URL for follower {FollowerActorId}. Skipping.", followerActorId);
                        continue;
                    }

                    string? sharedInbox = null;
                    if (followerActor.Endpoints is Endpoints endpoints)
                    {
                        sharedInbox = endpoints.SharedInbox?.ToString();
                    }

                    var groupKey = sharedInbox ?? personalInbox;

                    if (!inboxGroups.ContainsKey(groupKey))
                    {
                        inboxGroups[groupKey] = new List<(string, string)>();
                    }
                    inboxGroups[groupKey].Add((followerActorId, personalInbox));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error preparing delivery to follower {FollowerActorId}", followerActorId);
                }
            }

            var deliveryItems = new List<DeliveryQueueItem>();

            foreach (var (groupKey, groupedFollowers) in inboxGroups)
            {
                // Use the shared inbox when multiple followers are grouped under it.
                // For a single follower, use their personal inbox so the remote server can
                // always identify the recipient without relying on activity addressing.
                string inboxUrl = groupedFollowers.Count > 1
                    ? groupKey
                    : groupedFollowers[0].PersonalInbox;

                deliveryItems.Add(new DeliveryQueueItem
                {
                    Activity = activity,
                    InboxUrl = inboxUrl,
                    SenderActorId = senderActorId,
                    SenderUsername = senderUsername,
                    Status = DeliveryStatus.Pending,
                    MaxRetries = 5
                });

                if (groupedFollowers.Count > 1)
                {
                    _logger.LogInformation("Using shared inbox {InboxUrl} for {FollowerCount} followers",
                        groupKey, groupedFollowers.Count);
                }
            }

            if (deliveryItems.Any())
            {
                await _deliveryQueue.EnqueueBatchAsync(deliveryItems, cancellationToken);
                _logger.LogInformation("Queued {Count} deliveries for activity {ActivityId} to {InboxCount} inboxes",
                    deliveryItems.Count, activityId, deliveryItems.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queueing activity {ActivityId} for delivery", activityId);
        }
    }

    /// <summary>
    /// Queues an activity for delivery to explicit recipients (To, Cc, Bcc, etc.)
    /// Groups recipients by server and uses shared inbox when multiple recipients are on the same server
    /// </summary>
    public async Task QueueActivityToRecipientsAsync(
        string senderUsername,
        string activityId,
        IObjectOrLink activity,
        IEnumerable<string> recipientIds,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableActivityDelivery)
        {
            _logger.LogDebug("Activity delivery is disabled. Skipping delivery for {ActivityId}", activityId);
            return;
        }

        try
        {
            // Get sender actor
            var senderActor = await _actorRepository.GetActorByUsernameAsync(senderUsername, cancellationToken);
            if (senderActor == null)
            {
                _logger.LogWarning("Cannot queue activity for delivery: sender actor {Username} not found", senderUsername);
                return;
            }

            var senderActorId = senderActor.Id;
            if (string.IsNullOrEmpty(senderActorId))
            {
                _logger.LogWarning("Cannot queue activity for delivery: sender actor {Username} has no ID", senderUsername);
                return;
            }

            _logger.LogInformation("Queueing activity {ActivityId} for delivery to {RecipientCount} recipients",
                activityId, recipientIds.Count());

            // Filter out special addressing URIs that shouldn't be delivered to
            const string PublicAddress = "https://www.w3.org/ns/activitystreams#Public";
            var actualRecipients = recipientIds.Where(id =>
            {
                // Skip as:Public addressing
                if (id == PublicAddress)
                {
                    _logger.LogDebug("Skipping as:Public in recipient list - not a deliverable actor");
                    return false;
                }

                // Skip followers collection URLs (e.g., .../users/alice/followers)
                if (id.EndsWith("/followers") || id.EndsWith("/following"))
                {
                    _logger.LogDebug("Skipping followers/following collection URL {RecipientId} - not a deliverable actor", id);
                    return false;
                }

                return true;
            }).ToList();

            if (!actualRecipients.Any())
            {
                _logger.LogDebug("No actual recipients to deliver to after filtering special URIs");
                return;
            }

            // Group recipients by their server's shared inbox
            var inboxGroups = new Dictionary<string, List<string>>();

            foreach (var recipientId in actualRecipients)
            {
                try
                {
                    // Fetch the recipient's actor to get their inbox
                    var recipientActor = await _activityPubClient.GetActorAsync(new Uri(recipientId), cancellationToken);

                    if (recipientActor == null)
                    {
                        _logger.LogWarning("Recipient {RecipientId} could not be fetched. Skipping.", recipientId);
                        continue;
                    }

                    // Try to use shared inbox first
                    string? inboxUrl = null;

                    // Check for endpoints.sharedInbox
                    if (recipientActor.Endpoints is Endpoints endpoints)
                    {
                        inboxUrl = endpoints.SharedInbox?.ToString();
                    }

                    // Fall back to individual inbox
                    if (string.IsNullOrEmpty(inboxUrl))
                    {
                        inboxUrl = recipientActor.Inbox?.Href?.ToString();
                    }

                    if (string.IsNullOrEmpty(inboxUrl))
                    {
                        _logger.LogWarning("Could not determine inbox URL for recipient {RecipientId}. Skipping.", recipientId);
                        continue;
                    }

                    // Group by inbox URL (shared inbox groups multiple recipients)
                    if (!inboxGroups.ContainsKey(inboxUrl))
                    {
                        inboxGroups[inboxUrl] = new List<string>();
                    }
                    inboxGroups[inboxUrl].Add(recipientId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error preparing delivery to recipient {RecipientId}", recipientId);
                }
            }

            // Create delivery items for each unique inbox
            var deliveryItems = new List<DeliveryQueueItem>();

            foreach (var (inboxUrl, recipients) in inboxGroups)
            {
                var deliveryItem = new DeliveryQueueItem
                {
                    Activity = activity,
                    InboxUrl = inboxUrl,
                    SenderActorId = senderActorId,
                    SenderUsername = senderUsername,
                    Status = DeliveryStatus.Pending,
                    MaxRetries = 5
                };

                deliveryItems.Add(deliveryItem);

                if (recipients.Count > 1)
                {
                    _logger.LogInformation("Using shared inbox {InboxUrl} for {RecipientCount} recipients",
                        inboxUrl, recipients.Count);
                }
            }

            // Batch enqueue all delivery items
            if (deliveryItems.Any())
            {
                await _deliveryQueue.EnqueueBatchAsync(deliveryItems, cancellationToken);
                _logger.LogInformation("Queued {Count} deliveries for activity {ActivityId} to {InboxCount} inboxes",
                    deliveryItems.Count, activityId, deliveryItems.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queueing activity {ActivityId} for delivery to recipients", activityId);
        }
    }

    /// <summary>
    /// Processes pending deliveries from the queue
    /// </summary>
    public async Task ProcessPendingDeliveriesAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var pendingItems = await _deliveryQueue.GetPendingDeliveriesAsync(batchSize, cancellationToken);
            
            if (!pendingItems.Any())
            {
                return;
            }

            _logger.LogInformation("Processing {Count} pending deliveries", pendingItems.Count());

            // Process deliveries in parallel (with limited concurrency)
            var semaphore = new SemaphoreSlim(10); // Max 10 concurrent deliveries
            var tasks = pendingItems.Select(async item =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await DeliverActivityAsync(item, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending deliveries");
        }
    }

    /// <summary>
    /// Delivers a single activity to a remote inbox
    /// </summary>
    private async Task DeliverActivityAsync(DeliveryQueueItem item, CancellationToken cancellationToken)
    {
        try
        {
            // Load sender credentials first — they are needed for both inbox resolution
            // (signed GET for servers requiring authorized fetch, e.g. Threads) and delivery signing.
            var senderActor = await _actorRepository.GetActorByUsernameAsync(item.SenderUsername, cancellationToken);
            if (senderActor == null)
            {
                await _deliveryQueue.MarkAsFailedAsync(item.Id, "Sender actor not found", cancellationToken);
                return;
            }

            string? privateKeyPem = null;
            if (senderActor.ExtensionData?.TryGetValue("privateKeyPem", out var privateKeyObj) == true)
            {
                if (privateKeyObj is JsonElement privateKeyElement && privateKeyElement.ValueKind == JsonValueKind.String)
                {
                    privateKeyPem = privateKeyElement.GetString();
                }
            }

            if (string.IsNullOrEmpty(privateKeyPem))
            {
                var availableKeys = senderActor.ExtensionData?.Keys.ToList() ?? new List<string>();
                _logger.LogWarning(
                    "Sender {Username} has no private key. Available ExtensionData keys: {Keys}",
                    item.SenderUsername,
                    string.Join(", ", availableKeys));

                await _deliveryQueue.MarkAsFailedAsync(item.Id, "Sender has no private key", cancellationToken);
                return;
            }

            var publicKeyId = $"{item.SenderActorId}#main-key";
            if (senderActor.ExtensionData?.TryGetValue("publicKey", out var publicKeyObj) == true)
            {
                if (publicKeyObj is JsonElement publicKeyElement && publicKeyElement.TryGetProperty("id", out var idElement))
                {
                    publicKeyId = idElement.GetString() ?? publicKeyId;
                }
            }

            // Resolve inbox URL if it wasn't available when the item was queued.
            // Use a signed GET so servers that require authorized fetch (e.g. Threads) respond correctly.
            if (string.IsNullOrEmpty(item.InboxUrl))
            {
                if (string.IsNullOrEmpty(item.TargetActorId))
                {
                    await _deliveryQueue.MarkAsFailedAsync(item.Id, "No inbox URL and no target actor ID", cancellationToken);
                    if (item.AttemptCount >= item.MaxRetries)
                        await ApplyDeliveryDeadLetterSideEffectsAsync(item, cancellationToken);
                    return;
                }

                var targetActor = await FetchActorWithSignatureAsync(item.TargetActorId, publicKeyId, privateKeyPem, cancellationToken);
                var resolved = targetActor?.Inbox?.Href?.ToString();
                if (string.IsNullOrEmpty(resolved))
                {
                    var error = $"Could not resolve inbox for actor {item.TargetActorId}";
                    await _deliveryQueue.MarkAsFailedAsync(item.Id, error, cancellationToken);
                    if (item.AttemptCount >= item.MaxRetries)
                        await ApplyDeliveryDeadLetterSideEffectsAsync(item, cancellationToken);
                    return;
                }

                item.InboxUrl = resolved;
            }

            _logger.LogDebug("Delivering activity to {InboxUrl} (attempt {AttemptCount})",
                item.InboxUrl, item.AttemptCount);

            // Create HTTP client
            using var httpClient = _httpClientFactory.CreateClient("ActivityPub");
            var targetUri = new Uri(item.InboxUrl);

            // Serialize activity
            var activityJson = JsonSerializer.Serialize(item.Activity, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(activityJson, System.Text.Encoding.UTF8, "application/activity+json");

            // Create request message
            using var request = new HttpRequestMessage(HttpMethod.Post, targetUri)
            {
                Content = content
            };

            // Get the actual Content-Type value (including charset) for signature calculation
            var actualContentType = content.Headers.ContentType?.ToString();

            // Apply HTTP signature
            await _signatureService.ApplyHttpSignatureAsync(
                "POST",
                targetUri,
                (name, value) => 
                {
                    // Content-Type is already set by StringContent constructor
                    if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        // Don't override it
                        return;
                    }
                    // Digest is a content header
                    else if (name.Equals("Digest", StringComparison.OrdinalIgnoreCase))
                    {
                        request.Content?.Headers.TryAddWithoutValidation(name, value);
                    }
                    // All other headers are request headers
                    else
                    {
                        request.Headers.TryAddWithoutValidation(name, value);
                    }
                },
                publicKeyId,
                privateKeyPem,
                accept: "application/activity+json",
                contentType: actualContentType,
                getContentFunc: ct => Task.FromResult(System.Text.Encoding.UTF8.GetBytes(activityJson)),
                cancellationToken: cancellationToken
            );

            // Send the request
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                await _deliveryQueue.MarkAsDeliveredAsync(item.Id, cancellationToken);
                _logger.LogInformation("Successfully delivered activity to {InboxUrl}", item.InboxUrl);
                await ApplyDeliverySuccessSideEffectsAsync(item, cancellationToken);
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"HTTP {(int)response.StatusCode}: {errorBody}";
                await _deliveryQueue.MarkAsFailedAsync(item.Id, errorMessage, cancellationToken);
                _logger.LogWarning("Failed to deliver activity to {InboxUrl}: {Error}", item.InboxUrl, errorMessage);
                if (item.AttemptCount >= item.MaxRetries)
                    await ApplyDeliveryDeadLetterSideEffectsAsync(item, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            await _deliveryQueue.MarkAsFailedAsync(item.Id, ex.Message, cancellationToken);
            _logger.LogError(ex, "Error delivering activity to {InboxUrl}", item.InboxUrl);
            if (item.AttemptCount >= item.MaxRetries)
                await ApplyDeliveryDeadLetterSideEffectsAsync(item, cancellationToken);
        }
    }

    private async Task<Actor?> FetchActorWithSignatureAsync(
        string actorId,
        string publicKeyId,
        string privateKeyPem,
        CancellationToken cancellationToken)
    {
        using var httpClient = _httpClientFactory.CreateClient("ActivityPub");
        using var response = await _signatureService.SendSignedGetAsync(
            httpClient, new Uri(actorId), publicKeyId, privateKeyPem, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Signed GET for actor {ActorId} failed with status {StatusCode}",
                actorId, response.StatusCode);
            return null;
        }

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        return await response.Content.ReadFromJsonAsync<Actor>(jsonOptions, cancellationToken);
    }

    private async Task ApplyDeliverySuccessSideEffectsAsync(DeliveryQueueItem item, CancellationToken cancellationToken)
    {
        try
        {
            if (item.Activity is Follow followActivity)
            {
                var followingActorId = ExtractObjectActorId(followActivity);
                if (followingActorId != null)
                {
                    await _actorRepository.AddPendingFollowingAsync(item.SenderUsername, followingActorId, cancellationToken);
                    _logger.LogInformation(
                        "Follow activity for {Username} → {FollowingActorId} delivered; awaiting Accept from remote",
                        item.SenderUsername, followingActorId);
                }
            }
            else if (item.Activity is Undo undoActivity)
            {
                var undoTarget = undoActivity.Object?.FirstOrDefault();
                if (undoTarget is Follow undoneFollow)
                {
                    var followingActorId = ExtractObjectActorId(undoneFollow);
                    if (followingActorId != null)
                    {
                        // Remove from both lists: covers confirmed follows and follows that were still pending
                        await _actorRepository.RemoveFollowingAsync(item.SenderUsername, followingActorId, cancellationToken);
                        await _actorRepository.RemovePendingFollowingAsync(item.SenderUsername, followingActorId, cancellationToken);
                        _logger.LogInformation("User {Username} unfollowed {FollowingActorId} after successful Undo delivery",
                            item.SenderUsername, followingActorId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying post-delivery side effects for activity delivered to {InboxUrl}", item.InboxUrl);
        }
    }

    private async Task ApplyDeliveryDeadLetterSideEffectsAsync(DeliveryQueueItem item, CancellationToken cancellationToken)
    {
        try
        {
            if (item.Activity is Follow followActivity)
            {
                var followingActorId = ExtractObjectActorId(followActivity);
                if (followingActorId != null)
                {
                    _logger.LogWarning(
                        "Follow from {Username} to {TargetActorId} permanently failed after {Attempts} attempts. Removing from pending-following.",
                        item.SenderUsername, followingActorId, item.AttemptCount);
                    await _actorRepository.RemovePendingFollowingAsync(item.SenderUsername, followingActorId, cancellationToken);
                }
            }
            else if (item.Activity is Undo undoActivity)
            {
                var undoTarget = undoActivity.Object?.FirstOrDefault();
                if (undoTarget is Follow undoneFollow)
                {
                    var followingActorId = ExtractObjectActorId(undoneFollow);
                    if (followingActorId != null)
                    {
                        _logger.LogWarning(
                            "Undo Follow from {Username} to {TargetActorId} permanently failed after {Attempts} attempts. User remains following.",
                            item.SenderUsername, followingActorId, item.AttemptCount);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying dead-letter side effects for activity to {InboxUrl}", item.InboxUrl);
        }
    }

    private static string? ExtractObjectActorId(Activity activity)
    {
        var target = activity.Object?.FirstOrDefault();
        return target switch
        {
            ILink link => link.Href?.ToString(),
            IObject obj => obj.Id,
            _ => null
        };
    }
}
