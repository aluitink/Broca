using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Server.Services;

/// <summary>
/// Service for processing inbox activities
/// </summary>
public class InboxProcessor : IInboxHandler
{
    private readonly IActivityRepository _activityRepository;
    private readonly IActorRepository _actorRepository;
    private readonly IActivityBuilderFactory _activityBuilderFactory;
    private readonly AdminOperationsHandler _adminOperationsHandler;
    private readonly AttachmentProcessingService _attachmentProcessingService;
    private readonly ActivityDeliveryService _deliveryService;
    private readonly IActivityPubClient _activityPubClient;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<InboxProcessor> _logger;

    public InboxProcessor(
        IActivityRepository activityRepository,
        IActorRepository actorRepository,
        IActivityBuilderFactory activityBuilderFactory,
        AdminOperationsHandler adminOperationsHandler,
        AttachmentProcessingService attachmentProcessingService,
        ActivityDeliveryService deliveryService,
        IActivityPubClient activityPubClient,
        IOptions<ActivityPubServerOptions> options,
        ILogger<InboxProcessor> logger)
    {
        _activityRepository = activityRepository;
        _actorRepository = actorRepository;
        _activityBuilderFactory = activityBuilderFactory;
        _adminOperationsHandler = adminOperationsHandler;
        _attachmentProcessingService = attachmentProcessingService;
        _deliveryService = deliveryService;
        _activityPubClient = activityPubClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> HandleActivityAsync(string username, IObjectOrLink activity, bool isBearerTokenAuthenticated = false, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if this is the system actor's inbox and admin operations are enabled
            if (username == _options.SystemActorUsername && _options.EnableAdminOperations)
            {
                _logger.LogInformation("Activity received at system inbox - checking for admin operations");
                
                // Try to handle as an administrative operation
                var adminHandled = await _adminOperationsHandler.HandleAdminActivityAsync(activity, isBearerTokenAuthenticated, cancellationToken);
                
                if (adminHandled)
                {
                    _logger.LogInformation("Activity successfully handled as admin operation");
                    return true;
                }
                
                // If not handled as admin operation, continue with normal processing
                _logger.LogDebug("Activity not handled as admin operation, processing normally");
            }

            // Extract activity type and ID
            var activityType = activity.Type?.FirstOrDefault();
            if (string.IsNullOrEmpty(activityType))
            {
                _logger.LogWarning("Activity missing type property");
                return false;
            }

            var activityId = (activity as IObject)?.Id ?? Guid.NewGuid().ToString();

            _logger.LogInformation("Processing {ActivityType} activity {ActivityId} for user {Username}. ConcreteType={ConcreteType}",
                activityType, activityId, username, activity.GetType().Name);

            // Process attachments in the activity if it's an object with attachments
            if (activity is IObject activityObject)
            {
                await ProcessActivityAttachmentsAsync(username, activityObject, cancellationToken);
            }

            // Save to inbox
            await _activityRepository.SaveInboxActivityAsync(username, activityId, activity, cancellationToken);

            // Process based on activity type
            var handled = activityType switch
            {
                "Follow" => await HandleFollowAsync(username, activity as IObject, cancellationToken),
                "Undo" => await HandleUndoAsync(username, activity as IObject, cancellationToken),
                "Accept" => await HandleAcceptAsync(username, activity as IObject, cancellationToken),
                "Reject" => await HandleRejectAsync(username, activity as IObject, cancellationToken),
                "Create" => await HandleCreateAsync(username, activity as IObject, cancellationToken),
                "Delete" => await HandleDeleteAsync(username, activity as IObject, cancellationToken),
                "Like" => await HandleLikeAsync(username, activity as IObject, cancellationToken),
                "Announce" => await HandleAnnounceAsync(username, activity as IObject, cancellationToken),
                "Add" => await HandleAddAsync(username, activity as IObject, cancellationToken),
                "Remove" => await HandleRemoveAsync(username, activity as IObject, cancellationToken),
                "Update" => await HandleIncomingUpdateAsync(username, activity as IObject, cancellationToken),
                "Move" => await HandleMoveAsync(username, activity as IObject, cancellationToken),
                _ => true // Unknown types are accepted but not processed
            };

            return handled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling activity for user {Username}", username);
            return false;
        }
    }

    public Task<bool> VerifySignatureAsync(string signature, string requestTarget, string host, string date, string digest, CancellationToken cancellationToken = default)
    {
        // Note: HTTP signature verification is now performed at the InboxController level
        // before activities reach this processor. This method is kept for interface compatibility
        // but is no longer used in the signature verification flow.
        _logger.LogDebug("VerifySignatureAsync called - signature verification handled at controller level");
        return Task.FromResult(true);
    }

    private async Task<bool> HandleFollowAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        if (activity is not Activity followActivity || followActivity.Actor == null)
        {
            return false;
        }

        var followerActorId = followActivity.Actor.FirstOrDefault() switch
        {
            ILink link => link.Href?.ToString(),
            IObject obj => obj.Id,
            _ => null
        };

        if (followerActorId == null)
        {
            return false;
        }

        var actor = await _actorRepository.GetActorByUsernameAsync(username, cancellationToken);
        var manuallyApprovesFollowers = actor?.ExtensionData?.TryGetValue("manuallyApprovesFollowers", out var flagElement) == true
            && flagElement.ValueKind == System.Text.Json.JsonValueKind.True;

        if (manuallyApprovesFollowers)
        {
            await _actorRepository.AddPendingFollowerAsync(username, followerActorId, cancellationToken);
            _logger.LogInformation("Follow from {FollowerActorId} for {Username} added to pending - awaiting manual approval",
                followerActorId, username);
        }
        else
        {
            await _actorRepository.AddFollowerAsync(username, followerActorId, cancellationToken);
            _logger.LogInformation("Auto-accepted follow from {FollowerActorId} for {Username}", followerActorId, username);

            await SendAcceptActivityAsync(username, followActivity, followerActorId, cancellationToken);
        }

        return true;
    }

    private async Task<bool> HandleUndoAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        if (activity is not Activity undoActivity || undoActivity.Object == null)
        {
            return false;
        }

        var objectToUndo = undoActivity.Object.FirstOrDefault();
        if (objectToUndo == null)
        {
            return false;
        }

        // Handle ILink references (plain IRI strings or Link objects)
        if (objectToUndo is ILink link && link.Href != null)
        {
            var activityId = link.Href.ToString();
            var originalActivity = await _activityRepository.GetActivityByIdAsync(activityId, cancellationToken);
            if (originalActivity is IObject linkedObj)
            {
                objectToUndo = linkedObj;
            }
            else
            {
                _logger.LogWarning("Could not resolve linked activity {ActivityId} for Undo", activityId);
                return false;
            }
        }

        if (objectToUndo is not IObject obj || obj.Type == null)
        {
            return false;
        }

        // Check if it's an Undo Follow
        if (obj.Type.Contains("Follow"))
        {
            var originalFollow = obj as Activity;
            if (originalFollow?.Actor != null)
            {
                var followerActorId = originalFollow.Actor.FirstOrDefault() switch
                {
                    ILink actorLink => actorLink.Href?.ToString(),
                    IObject actorObj => actorObj.Id,
                    _ => null
                };

                if (followerActorId != null)
                {
                    await _actorRepository.RemoveFollowerAsync(username, followerActorId, cancellationToken);
                    _logger.LogInformation("Removed follower {FollowerActorId} from {Username}", followerActorId, username);
                    return true;
                }
            }
        }
        // Check if it's an Undo Like
        else if (obj.Type.Contains("Like"))
        {
            var originalLike = obj as Activity;
            if (originalLike?.Id != null)
            {
                // Delete the Like activity - the repository will automatically update indexes
                await _activityRepository.DeleteActivityAsync(originalLike.Id, cancellationToken);
                _logger.LogInformation("Removed Like {LikeId} from {Username}'s inbox", originalLike.Id, username);
                return true;
            }
        }
        // Check if it's an Undo Announce
        else if (obj.Type.Contains("Announce"))
        {
            var originalAnnounce = obj as Activity;
            if (originalAnnounce?.Id != null)
            {
                // Delete the Announce activity - the repository will automatically update indexes
                await _activityRepository.DeleteActivityAsync(originalAnnounce.Id, cancellationToken);
                _logger.LogInformation("Removed Announce {AnnounceId} from {Username}'s inbox", originalAnnounce.Id, username);
                return true;
            }
        }

        return false;
    }

    private async Task<bool> HandleAcceptAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        if (activity is not Activity acceptActivity)
            return true;

        var acceptingActorId = acceptActivity.Actor?.FirstOrDefault() switch
        {
            ILink link => link.Href?.ToString(),
            IObject obj => obj.Id,
            _ => null
        };

        if (acceptingActorId != null)
        {
            await _actorRepository.RemovePendingFollowingAsync(username, acceptingActorId, cancellationToken);
            await _actorRepository.AddFollowingAsync(username, acceptingActorId, cancellationToken);
            _logger.LogInformation("Follow of {AcceptingActorId} by {Username} accepted; added to following", acceptingActorId, username);
        }

        return true;
    }

    private async Task<bool> HandleRejectAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        if (activity is not Activity rejectActivity || rejectActivity.Object == null)
        {
            return false;
        }

        var rejectedObject = rejectActivity.Object.FirstOrDefault();
        
        // Handle ILink references (plain IRI strings or Link objects)
        if (rejectedObject is ILink link && link.Href != null)
        {
            var activityId = link.Href.ToString();
            var originalActivity = await _activityRepository.GetActivityByIdAsync(activityId, cancellationToken);
            if (originalActivity is IObject linkedObj)
            {
                rejectedObject = linkedObj;
            }
        }
        
        if (rejectedObject is IObject obj && obj.Type?.Contains("Follow") == true)
        {
            var rejectingActorId = rejectActivity.Actor?.FirstOrDefault() switch
            {
                ILink actorLink => actorLink.Href?.ToString(),
                IObject actorObj => actorObj.Id,
                _ => null
            };

            if (rejectingActorId != null)
            {
                await _actorRepository.RemovePendingFollowingAsync(username, rejectingActorId, cancellationToken);
                await _actorRepository.RemoveFollowingAsync(username, rejectingActorId, cancellationToken);
                _logger.LogInformation("Follow of {RejectingActorId} by {Username} was rejected - removed from pending-following",
                    rejectingActorId, username);
            }
        }

        return true;
    }

    private Task<bool> HandleCreateAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        // Create activities are already stored in the inbox
        // Additional processing (e.g., notification) could be done here
        return Task.FromResult(true);
    }

    private async Task<bool> HandleDeleteAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        if (activity is not Activity deleteActivity)
        {
            return false;
        }

        var objectToDelete = deleteActivity.Object?.FirstOrDefault();
        if (objectToDelete == null)
        {
            _logger.LogWarning("Delete activity has no object");
            return false;
        }

        string? objectId = objectToDelete switch
        {
            ILink link => link.Href?.ToString(),
            Tombstone tombstone => tombstone.Id,
            IObject obj => obj.Id,
            _ => null
        };

        if (string.IsNullOrEmpty(objectId))
        {
            _logger.LogWarning("Delete activity has invalid object reference");
            return false;
        }

        await _activityRepository.MarkObjectAsDeletedAsync(objectId, cancellationToken);
        _logger.LogInformation("Marked object {ObjectId} as deleted for user {Username}", objectId, username);

        return true;
    }

    private Task<bool> HandleLikeAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processed Like activity for {Username}", username);
        return Task.FromResult(true);
    }

    private Task<bool> HandleAnnounceAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processed Announce activity for {Username}", username);
        return Task.FromResult(true);
    }

    private async Task<bool> HandleMoveAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        if (activity is not Activity moveActivity || moveActivity.Object == null || moveActivity.Target == null)
        {
            _logger.LogWarning("Move activity missing required properties");
            return false;
        }

        // Extract old actor ID (from object - the actor being moved from)
        var oldActorRef = moveActivity.Object.FirstOrDefault();
        var oldActorId = oldActorRef switch
        {
            ILink link => link.Href?.ToString(),
            IObject obj => obj.Id,
            _ => null
        };

        if (string.IsNullOrEmpty(oldActorId))
        {
            _logger.LogWarning("Move  activity has invalid object reference");
            return false;
        }

        // Extract new actor ID (from target - the actor being moved to)
        var newActorRef = moveActivity.Target.FirstOrDefault();
        var newActorId = newActorRef switch
        {
            ILink link => link.Href?.ToString(),
            IObject obj => obj.Id,
            _ => null
        };

        if (string.IsNullOrEmpty(newActorId))
        {
            _logger.LogWarning("Move activity has invalid target reference");
            return false;
        }

        _logger.LogInformation("Processing Move activity: {OldActorId} -> {NewActorId} for user {Username}", 
            oldActorId, newActorId, username);

        // Check if this user follows the old actor
        var following = await _actorRepository.GetFollowingAsync(username, cancellationToken);
        if (!following.Contains(oldActorId))
        {
            _logger.LogDebug("User {Username} does not follow {OldActorId}, skipping Move", username, oldActorId);
            return true; // Not an error, just not relevant to this user
        }

        // Fetch the new actor to verify alsoKnownAs
        try
        {
            var newActor = await _activityPubClient.GetAsync<Actor>(new Uri(newActorId), useCache: false, cancellationToken);
            
            if (newActor == null)
            {
                _logger.LogWarning("Could not fetch new actor {NewActorId} for Move verification", newActorId);
                return false;
            }

            // Security check: verify alsoKnownAs contains the old actor ID
            var alsoKnownAs = new List<string>();
            if (newActor.ExtensionData?.TryGetValue("alsoKnownAs", out var alsoKnownAsElement) == true)
            {
                try
                {
                    if (alsoKnownAsElement.ValueKind == JsonValueKind.Array)
                    {
                        alsoKnownAs = JsonSerializer.Deserialize<List<string>>(alsoKnownAsElement.GetRawText()) ?? new List<string>();
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse alsoKnownAs for {NewActorId}", newActorId);
                }
            }

            if (!alsoKnownAs.Contains(oldActorId))
            {
                _logger.LogWarning("Security check failed: {NewActorId} does not list {OldActorId} in alsoKnownAs. Rejecting Move.",
                    newActorId, oldActorId);
                return false;
            }

            // Valid migration - update following list
            await _actorRepository.RemoveFollowingAsync(username, oldActorId, cancellationToken);
            await _actorRepository.AddFollowingAsync(username, newActorId, cancellationToken);
            
            _logger.LogInformation("Successfully migrated follow for {Username}: {OldActorId} -> {NewActorId}",
                username, oldActorId, newActorId);

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error fetching new actor {NewActorId} for Move verification", newActorId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Move activity for {Username}", username);
            return false;
        }
    }

    private async Task<bool> HandleAddAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        if (activity is not Activity addActivity || addActivity.Object == null || addActivity.Target == null)
        {
            _logger.LogWarning("Add activity missing object or target");
            return false;
        }

        // Extract the collection ID from the target
        var targetRef = addActivity.Target.FirstOrDefault();
        var collectionUrl = targetRef switch
        {
            Link link => link.Href?.ToString(),
            IObject targetObj => targetObj.Id,
            _ => null
        };

        if (string.IsNullOrEmpty(collectionUrl))
        {
            _logger.LogWarning("Add activity has invalid target");
            return false;
        }

        // Verify this is for the inbox owner's collection
        if (!collectionUrl.Contains($"/users/{username}/collections/"))
        {
            _logger.LogWarning("Add activity target {Target} does not belong to {Username}", collectionUrl, username);
            return false;
        }

        // Extract collection ID from URL
        var collectionId = ExtractCollectionIdFromUrl(collectionUrl);
        if (string.IsNullOrEmpty(collectionId))
        {
            _logger.LogWarning("Could not extract collection ID from URL {CollectionUrl}", collectionUrl);
            return false;
        }

        // Get the object to add
        var objectRef = addActivity.Object.FirstOrDefault();
        if (objectRef == null)
        {
            _logger.LogWarning("Add activity has no object");
            return false;
        }

        // Extract object ID
        string? objectId = null;
        
        if (objectRef is IObject obj)
        {
            objectId = obj.Id;
            if (string.IsNullOrEmpty(objectId))
            {
                // Generate an ID for the object
                var baseUrl = (_options.BaseUrl ?? "http://localhost").TrimEnd('/');
                var routePrefix = _options.NormalizedRoutePrefix;
                objectId = $"{baseUrl}{routePrefix}/users/{username}/objects/{Guid.NewGuid()}";
                obj.Id = objectId;
            }

            // Store the object
            await _activityRepository.SaveInboxActivityAsync(username, objectId, obj, cancellationToken);
            _logger.LogInformation("Stored object {ObjectId} for user {Username}", objectId, username);
        }
        else if (objectRef is Link link)
        {
            objectId = link.Href?.ToString();
            if (string.IsNullOrEmpty(objectId))
            {
                _logger.LogWarning("Add activity link has no href");
                return false;
            }

            // TODO: Optionally fetch and cache the remote object
        }

        if (string.IsNullOrEmpty(objectId))
        {
            _logger.LogWarning("Add activity object has no ID");
            return false;
        }

        // Add to collection
        await _actorRepository.AddToCollectionAsync(username, collectionId, objectId, cancellationToken);
        _logger.LogInformation("Added {ObjectId} to collection {CollectionId} for user {Username}", 
            objectId, collectionId, username);

        return true;
    }

    private async Task<bool> HandleRemoveAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        if (activity is not Activity removeActivity || removeActivity.Object == null || removeActivity.Target == null)
        {
            _logger.LogWarning("Remove activity missing object or target");
            return false;
        }

        // Extract the collection ID from the target
        var targetRef = removeActivity.Target.FirstOrDefault();
        var collectionUrl = targetRef switch
        {
            Link link => link.Href?.ToString(),
            IObject obj => obj.Id,
            _ => null
        };

        if (string.IsNullOrEmpty(collectionUrl))
        {
            _logger.LogWarning("Remove activity has invalid target");
            return false;
        }

        // Verify this is for the inbox owner's collection
        if (!collectionUrl.Contains($"/users/{username}/collections/"))
        {
            _logger.LogWarning("Remove activity target {Target} does not belong to {Username}", collectionUrl, username);
            return false;
        }

        var collectionId = ExtractCollectionIdFromUrl(collectionUrl);
        if (string.IsNullOrEmpty(collectionId))
        {
            _logger.LogWarning("Could not extract collection ID from URL {CollectionUrl}", collectionUrl);
            return false;
        }

        // Get the object to remove
        var objectRef = removeActivity.Object.FirstOrDefault();
        var objectId = objectRef switch
        {
            Link link => link.Href?.ToString(),
            IObject obj => obj.Id,
            _ => null
        };

        if (string.IsNullOrEmpty(objectId))
        {
            _logger.LogWarning("Remove activity has invalid object");
            return false;
        }

        // Remove from collection
        await _actorRepository.RemoveFromCollectionAsync(username, collectionId, objectId, cancellationToken);
        _logger.LogInformation("Removed {ObjectId} from collection {CollectionId} for user {Username}", 
            objectId, collectionId, username);

        return true;
    }

    private string? ExtractCollectionIdFromUrl(string url)
    {
        // Extract collection ID from URL pattern: .../users/{username}/collections/{collectionId}
        var match = System.Text.RegularExpressions.Regex.Match(url, @"/collections/([^/?]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private async Task SendAcceptActivityAsync(string username, Activity followActivity, string followerActorId, CancellationToken cancellationToken)
    {
        try
        {
            var builder = _activityBuilderFactory.CreateForUsername(username);
            var acceptActivity = builder.Accept(followActivity);

            var baseUrl = (_options.BaseUrl ?? "http://localhost").TrimEnd('/');
            var routePrefix = _options.NormalizedRoutePrefix;
            acceptActivity.Id = $"{baseUrl}{routePrefix}/activities/{Guid.NewGuid()}";

            await _activityRepository.SaveOutboxActivityAsync(username, acceptActivity.Id, acceptActivity, cancellationToken);
            await _deliveryService.QueueActivityToTargetAsync(username, acceptActivity.Id, acceptActivity, followerActorId, cancellationToken);

            _logger.LogInformation("Queued Accept activity for delivery to {FollowerActorId}", followerActorId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send Accept activity to {FollowerActorId} - follow is still recorded", followerActorId);
        }
    }

    /// <summary>
    private async Task<bool> HandleIncomingUpdateAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        if (activity is not Activity updateActivity || updateActivity.Object == null)
            return true;

        var wrappedObject = updateActivity.Object.FirstOrDefault();

        if (wrappedObject is ILink)
        {
            _logger.LogDebug("Ignoring Update whose object is an unresolved IRI reference");
            return true;
        }

        if (wrappedObject is not Actor updatedActor)
            return true;

        var updatedActorId = updatedActor.Id;
        if (string.IsNullOrEmpty(updatedActorId))
        {
            _logger.LogWarning("Update activity contains an Actor with no Id");
            return true;
        }

        // Security: the sender must be the same actor being updated
        var senderId = updateActivity.Actor?.FirstOrDefault() switch
        {
            ILink l => l.Href?.ToString(),
            IObject o => o.Id,
            _ => null
        };

        if (senderId != updatedActorId)
        {
            _logger.LogWarning("Update sender {SenderId} does not match updated actor {ActorId} — ignoring",
                senderId, updatedActorId);
            return true;
        }

        // Only update actors we have already cached locally
        var existingActor = await _actorRepository.GetActorByIdAsync(updatedActorId, cancellationToken);
        if (existingActor == null)
        {
            _logger.LogDebug("Received Update for unknown remote actor {ActorId} — ignoring", updatedActorId);
            return true;
        }

        // Refuse to overwrite local actors via S2S
        var baseUrl = (_options.BaseUrl ?? string.Empty).TrimEnd('/');
        if (!string.IsNullOrEmpty(baseUrl) && updatedActorId.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Ignoring Update targeting local actor {ActorId}", updatedActorId);
            return true;
        }

        await _actorRepository.SaveActorAsync(existingActor.PreferredUsername!, updatedActor, cancellationToken);
        _logger.LogInformation("Updated cached remote actor {ActorId}", updatedActorId);
        return true;
    }

    /// Processes attachments in an activity, downloading remote resources and rewriting URLs
    /// </summary>
    private async Task ProcessActivityAttachmentsAsync(string username, IObject activityObject, CancellationToken cancellationToken)
    {
        try
        {
            // Process the activity's direct attachments and images
            await _attachmentProcessingService.ProcessAttachmentsAsync(activityObject, username, cancellationToken);
            await _attachmentProcessingService.ProcessImagesAsync(activityObject, username, cancellationToken);

            // If this is a Create/Update/Announce activity, also process the embedded object
            if (activityObject is Activity activity && activity.Object != null)
            {
                foreach (var obj in activity.Object)
                {
                    if (obj is IObject embeddedObject)
                    {
                        await _attachmentProcessingService.ProcessAttachmentsAsync(embeddedObject, username, cancellationToken);
                        await _attachmentProcessingService.ProcessImagesAsync(embeddedObject, username, cancellationToken);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process attachments for activity, continuing without attachment processing");
            // Don't fail the entire activity processing if attachment processing fails
        }
    }
}
