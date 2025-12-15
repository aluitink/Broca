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

            _logger.LogInformation("Queueing activity {ActivityId} for direct delivery to {TargetActorId}", 
                activityId, targetActorId);

            // Fetch the target actor to get their inbox
            var targetActor = await _activityPubClient.GetActorAsync(new Uri(targetActorId), cancellationToken);
            
            if (targetActor?.Inbox == null)
            {
                _logger.LogWarning("Target actor {TargetActorId} has no inbox. Skipping delivery.", targetActorId);
                return;
            }

            var inboxUrl = targetActor.Inbox switch
            {
                Link link => link.Href?.ToString(),
                KristofferStrube.ActivityStreams.Object obj => obj.Id,
                _ => null
            };

            if (string.IsNullOrEmpty(inboxUrl))
            {
                _logger.LogWarning("Could not determine inbox URL for target actor {TargetActorId}. Skipping.", targetActorId);
                return;
            }

            var deliveryItem = new DeliveryQueueItem
            {
                Activity = activity,
                InboxUrl = inboxUrl,
                SenderActorId = senderActorId,
                SenderUsername = senderUsername,
                Status = DeliveryStatus.Pending,
                MaxRetries = 5
            };

            await _deliveryQueue.EnqueueAsync(deliveryItem, cancellationToken);
            _logger.LogInformation("Queued direct delivery for activity {ActivityId} to {TargetActorId}", activityId, targetActorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queueing activity {ActivityId} for direct delivery to {TargetActorId}", 
                activityId, targetActorId);
        }
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

            // Create delivery items for each follower's inbox
            var deliveryItems = new List<DeliveryQueueItem>();
            
            foreach (var followerActorId in followers)
            {
                try
                {
                    // Fetch the follower's actor to get their inbox
                    var followerActor = await _activityPubClient.GetActorAsync(new Uri(followerActorId), cancellationToken);
                    
                    if (followerActor?.Inbox == null)
                    {
                        _logger.LogWarning("Follower {FollowerActorId} has no inbox. Skipping.", followerActorId);
                        continue;
                    }

                    var inboxUrl = followerActor.Inbox switch
                    {
                        Link link => link.Href?.ToString(),
                        KristofferStrube.ActivityStreams.Object obj => obj.Id,
                        _ => null
                    };

                    if (string.IsNullOrEmpty(inboxUrl))
                    {
                        _logger.LogWarning("Could not determine inbox URL for follower {FollowerActorId}. Skipping.", followerActorId);
                        continue;
                    }

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
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error preparing delivery to follower {FollowerActorId}", followerActorId);
                }
            }

            // Batch enqueue all delivery items
            if (deliveryItems.Any())
            {
                await _deliveryQueue.EnqueueBatchAsync(deliveryItems, cancellationToken);
                _logger.LogInformation("Queued {Count} deliveries for activity {ActivityId}", deliveryItems.Count, activityId);
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

            // Group recipients by their server's shared inbox
            var inboxGroups = new Dictionary<string, List<string>>();

            foreach (var recipientId in recipientIds)
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
                    if (recipientActor.Endpoints != null)
                    {
                        var sharedInbox = recipientActor.Endpoints switch
                        {
                            KristofferStrube.ActivityStreams.Object obj when obj.ExtensionData?.ContainsKey("sharedInbox") == true => 
                                obj.ExtensionData["sharedInbox"] switch
                                {
                                    JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String => 
                                        jsonElement.GetString(),
                                    _ => null
                                },
                            _ => null
                        };

                        if (!string.IsNullOrEmpty(sharedInbox))
                        {
                            inboxUrl = sharedInbox;
                        }
                    }

                    // Fall back to individual inbox
                    if (string.IsNullOrEmpty(inboxUrl) && recipientActor.Inbox != null)
                    {
                        inboxUrl = recipientActor.Inbox switch
                        {
                            Link link => link.Href?.ToString(),
                            KristofferStrube.ActivityStreams.Object obj => obj.Id,
                            _ => null
                        };
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
            _logger.LogDebug("Delivering activity to {InboxUrl} (attempt {AttemptCount})", 
                item.InboxUrl, item.AttemptCount);

            // Get sender's private key for signing
            var senderActor = await _actorRepository.GetActorByUsernameAsync(item.SenderUsername, cancellationToken);
            if (senderActor == null)
            {
                await _deliveryQueue.MarkAsFailedAsync(item.Id, "Sender actor not found", cancellationToken);
                return;
            }

            // Extract private key from actor's extension data
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
                // Log diagnostic information about what keys are available
                var availableKeys = senderActor.ExtensionData?.Keys.ToList() ?? new List<string>();
                _logger.LogWarning(
                    "Sender {Username} has no private key. Available ExtensionData keys: {Keys}", 
                    item.SenderUsername, 
                    string.Join(", ", availableKeys));
                    
                await _deliveryQueue.MarkAsFailedAsync(item.Id, "Sender has no private key", cancellationToken);
                return;
            }

            // Determine public key ID
            var publicKeyId = $"{item.SenderActorId}#main-key";
            if (senderActor.ExtensionData?.TryGetValue("publicKey", out var publicKeyObj) == true)
            {
                if (publicKeyObj is JsonElement publicKeyElement && publicKeyElement.TryGetProperty("id", out var idElement))
                {
                    publicKeyId = idElement.GetString() ?? publicKeyId;
                }
            }

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
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"HTTP {(int)response.StatusCode}: {errorBody}";
                await _deliveryQueue.MarkAsFailedAsync(item.Id, errorMessage, cancellationToken);
                _logger.LogWarning("Failed to deliver activity to {InboxUrl}: {Error}", item.InboxUrl, errorMessage);
            }
        }
        catch (Exception ex)
        {
            await _deliveryQueue.MarkAsFailedAsync(item.Id, ex.Message, cancellationToken);
            _logger.LogError(ex, "Error delivering activity to {InboxUrl}", item.InboxUrl);
        }
    }
}
