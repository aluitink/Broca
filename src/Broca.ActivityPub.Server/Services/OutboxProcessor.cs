using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Server.Services;

/// <summary>
/// Service for processing outbox activities
/// </summary>
public class OutboxProcessor
{
    private readonly IActivityRepository _activityRepository;
    private readonly IActorRepository _actorRepository;
    private readonly ActivityDeliveryService _deliveryService;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IActivityRepository activityRepository,
        IActorRepository actorRepository,
        ActivityDeliveryService deliveryService,
        IOptions<ActivityPubServerOptions> options,
        ILogger<OutboxProcessor> logger)
    {
        _activityRepository = activityRepository;
        _actorRepository = actorRepository;
        _deliveryService = deliveryService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Processes an outgoing activity
    /// </summary>
    public async Task<string> ProcessActivityAsync(string username, IObjectOrLink activity, CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate activity ID if not present
            var activityObj = activity as IObject;
            if (string.IsNullOrEmpty(activityObj?.Id))
            {
                var baseUrl = (_options.BaseUrl ?? "http://localhost").TrimEnd('/');
                var routePrefix = _options.NormalizedRoutePrefix;
                var generatedActivityId = $"{baseUrl}{routePrefix}/activities/{Guid.NewGuid()}";
                if (activityObj != null)
                {
                    activityObj.Id = generatedActivityId;
                }
            }
            
            var activityId = activityObj?.Id ?? throw new InvalidOperationException("Activity ID could not be determined");

            // Extract activity type
            var activityType = activity.Type?.FirstOrDefault();
            if (string.IsNullOrEmpty(activityType))
            {
                throw new InvalidOperationException("Activity missing type property");
            }
            
            _logger.LogInformation("Processing outgoing {ActivityType} activity {ActivityId} for user {Username}",
                activityType, activityId, username);

            // Save to outbox
            await _activityRepository.SaveOutboxActivityAsync(username, activityId, activity, cancellationToken);

            // Process side effects based on activity type
            await ProcessActivitySideEffectsAsync(username, activityType, activityObj, cancellationToken);

            // Queue activity for delivery
            await QueueActivityDeliveryAsync(username, activityId, activityType, activityObj, cancellationToken);

            return activityId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing outbox activity for user {Username}", username);
            throw;
        }
    }

    private async Task QueueActivityDeliveryAsync(string username, string activityId, string activityType, IObject? activity, CancellationToken cancellationToken)
    {
        // For activities that target a specific actor (Follow, Like, Announce, etc.),
        // deliver directly to that actor's inbox
        if (activity is Follow or Like or Announce or Accept or Reject or Undo)
        {
            var typedActivity = (Activity)activity;
            // Extract target actor ID from the activity's object
            var targetActorId = ExtractTargetActorId(typedActivity);
            if (!string.IsNullOrEmpty(targetActorId))
            {
                await _deliveryService.QueueActivityToTargetAsync(username, activityId, activity, targetActorId, cancellationToken);
                return;
            }
        }

        if (activity is Activity typedActivity2)
        {

            // For Create, Update, Delete, etc., check if there are explicit recipients
            var recipients = ExtractRecipients(typedActivity2);
            if (recipients.Any())
            {
                // Deliver to explicit recipients (To, Cc, Bcc, Bto, Audience)
                await _deliveryService.QueueActivityToRecipientsAsync(username, activityId, activity, recipients, cancellationToken);
                return;
            }
        }

        // For all other activities without explicit recipients, deliver to followers
        if (activity != null)
        {
            await _deliveryService.QueueActivityForDeliveryAsync(username, activityId, activity, cancellationToken);
        }
    }

    private List<string> ExtractRecipients(Activity activity)
    {
        var recipients = new HashSet<string>();

        void AddRecipients(IEnumerable<IObjectOrLink>? items)
        {
            if (items == null) return;

            foreach (var item in items)
            {
                string? recipientId = item switch
                {
                    Link link => link.Href?.ToString(),
                    IObject obj => obj.Id,
                    _ => null
                };

                if (!string.IsNullOrEmpty(recipientId))
                {
                    recipients.Add(recipientId);
                }
            }
        }

        AddRecipients(activity.To);
        AddRecipients(activity.Cc);
        AddRecipients(activity.Bcc);
        AddRecipients(activity.Bto);
        AddRecipients(activity.Audience);

        return recipients.ToList();
    }

    private string? ExtractTargetActorId(Activity activity)
    {
        // For most activities, the target is in the 'object' property
        var targetObject = activity.Object?.FirstOrDefault();
        
        // For Undo, we need to look at the wrapped activity's object
        if (activity.Type?.Contains("Undo") == true && targetObject is Activity wrappedActivity)
        {
            targetObject = wrappedActivity.Object?.FirstOrDefault();
        }
        
        return targetObject switch
        {
            Link link => link.Href?.ToString(),
            IObject obj => obj.Id,
            _ => null
        };
    }

    private async Task ProcessActivitySideEffectsAsync(string username, string activityType, IObject? activity, CancellationToken cancellationToken)
    {
        switch (activity)
        {
            case Create createActivity:
                await HandleOutgoingCreateAsync(username, createActivity, cancellationToken);
                break;
            case Update updateActivity:
                await HandleOutgoingUpdateAsync(username, updateActivity, cancellationToken);
                break;
            case Follow followActivity:
                await HandleOutgoingFollowAsync(username, followActivity, cancellationToken);
                break;
            case Undo undoActivity:
                await HandleOutgoingUndoAsync(username, undoActivity, cancellationToken);
                break;
            case Add addActivity:
                await HandleOutgoingAddAsync(username, addActivity, cancellationToken);
                break;
            case Remove removeActivity:
                await HandleOutgoingRemoveAsync(username, removeActivity, cancellationToken);
                break;
            default:
                // No side effects for other activity types
                break;
        }
    }

    private async Task HandleOutgoingFollowAsync(string username, Follow followActivity, CancellationToken cancellationToken)
    {
        if (followActivity.Object != null)
        {
            var followTarget = followActivity.Object.FirstOrDefault();
            var followingActorId = followTarget switch
            {
                Link link => link.Href?.ToString(),
                IObject obj => obj.Id,
                _ => null
            };
            
            if (followingActorId != null)
            {
                await _actorRepository.AddFollowingAsync(username, followingActorId, cancellationToken);
                _logger.LogInformation("User {Username} now following {FollowingActorId}", username, followingActorId);
            }
        }
    }

    private async Task HandleOutgoingUpdateAsync(string username, Update updateActivity, CancellationToken cancellationToken)
    {
        if (updateActivity.Object == null)
        {
            return;
        }

        var updatedObject = updateActivity.Object.FirstOrDefault();
        if (updatedObject == null)
        {
            return;
        }

        // Check if the updated object is an Actor/Person (profile update)
        if (updatedObject is Actor updatedActor)
        {
            await HandleOutgoingUpdateActorAsync(username, updatedActor, cancellationToken);
        }
    }

    private async Task HandleOutgoingUpdateActorAsync(string username, Actor updatedActor, CancellationToken cancellationToken)
    {
        // Verify the actor being updated matches the authenticated user
        if (updatedActor.PreferredUsername != username)
        {
            _logger.LogWarning("User {Username} attempted to update actor with preferredUsername {PreferredUsername}",
                username, updatedActor.PreferredUsername);
            return;
        }

        // Get the existing actor
        var existingActor = await _actorRepository.GetActorByUsernameAsync(username, cancellationToken);
        if (existingActor == null)
        {
            _logger.LogWarning("Cannot update actor - actor {Username} not found", username);
            return;
        }

        // Preserve non-editable fields from existing actor
        updatedActor.Id = existingActor.Id;
        updatedActor.Type = existingActor.Type;
        updatedActor.PreferredUsername = existingActor.PreferredUsername;
        updatedActor.Inbox = existingActor.Inbox;
        updatedActor.Outbox = existingActor.Outbox;
        updatedActor.Following = existingActor.Following;
        updatedActor.Followers = existingActor.Followers;
        // Note: PublicKey is stored in extension data, will be preserved below
        updatedActor.Endpoints = existingActor.Endpoints;
        updatedActor.Published = existingActor.Published;

        // Preserve private key from extension data
        if (existingActor.ExtensionData?.ContainsKey("privateKeyPem") == true)
        {
            updatedActor.ExtensionData ??= new Dictionary<string, JsonElement>();
            updatedActor.ExtensionData["privateKeyPem"] = existingActor.ExtensionData["privateKeyPem"];
        }

        // Preserve publicKey from extension data if present
        if (existingActor.ExtensionData?.ContainsKey("publicKey") == true)
        {
            updatedActor.ExtensionData ??= new Dictionary<string, JsonElement>();
            updatedActor.ExtensionData["publicKey"] = existingActor.ExtensionData["publicKey"];
        }

        // Update the timestamp
        updatedActor.Updated = DateTime.UtcNow;

        // Save the updated actor
        await _actorRepository.SaveActorAsync(username, updatedActor, cancellationToken);
        
        _logger.LogInformation("Updated actor profile for user {Username}", username);
    }

    private async Task HandleOutgoingCreateAsync(string username, Create createActivity, CancellationToken cancellationToken)
    {
        if (createActivity.Object == null)
        {
            return;
        }

        var createdObject = createActivity.Object.FirstOrDefault();
        if (createdObject == null)
        {
            return;
        }

        // Check if the created object is a Collection
        if (createdObject is Collection or OrderedCollection)
        {
            await HandleOutgoingCreateCollectionAsync(username, (IObject)createdObject, cancellationToken);
        }
    }

    private async Task HandleOutgoingCreateCollectionAsync(string username, IObject collectionObject, CancellationToken cancellationToken)
    {
        // Extract collection definition from extension data
        CustomCollectionDefinition? definition = null;
        
        if (collectionObject.ExtensionData != null)
        {
            // Look for collectionDefinition in extension data
            if (collectionObject.ExtensionData.TryGetValue("collectionDefinition", out var defElement) ||
                collectionObject.ExtensionData.TryGetValue("broca:collectionDefinition", out defElement))
            {
                try
                {
                    definition = JsonSerializer.Deserialize<CustomCollectionDefinition>(defElement.GetRawText());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize collection definition from extension data");
                    return;
                }
            }
        }

        // If no extension data found, construct from standard Collection properties
        if (definition == null)
        {
            var collectionId = collectionObject.Name?.FirstOrDefault() ?? Guid.NewGuid().ToString();
            definition = new CustomCollectionDefinition
            {
                Id = collectionId,
                Name = collectionObject.Name?.FirstOrDefault() ?? collectionId,
                Description = collectionObject.Summary?.FirstOrDefault()?.ToString(),
                Type = CollectionType.Manual,
                Visibility = CollectionVisibility.Public,
                Created = DateTimeOffset.UtcNow,
                Updated = DateTimeOffset.UtcNow
            };
        }

        // Save the collection definition
        try
        {
            await _actorRepository.SaveCollectionDefinitionAsync(username, definition, cancellationToken);
            _logger.LogInformation("Created collection {CollectionId} for user {Username} from outbox Create activity", 
                definition.Id, username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save collection {CollectionId} for user {Username}", 
                definition.Id, username);
        }
    }

    private async Task HandleOutgoingUndoAsync(string username, Undo undoActivity, CancellationToken cancellationToken)
    {
        if (undoActivity.Object == null)
        {
            return;
        }

        var undoObject = undoActivity.Object.FirstOrDefault();
        
        // Handle Undo Follow
        if (undoObject is Follow followActivity && followActivity.Object != null)
        {
            var followTarget = followActivity.Object.FirstOrDefault();
            var followingActorId = followTarget switch
            {
                Link link => link.Href?.ToString(),
                IObject obj => obj.Id,
                _ => null
            };
            
            if (followingActorId != null)
            {
                await _actorRepository.RemoveFollowingAsync(username, followingActorId, cancellationToken);
                _logger.LogInformation("User {Username} unfollowed {FollowingActorId}", username, followingActorId);
            }
        }
    }

    private async Task HandleOutgoingAddAsync(string username, Add addActivity, CancellationToken cancellationToken)
    {
        if (addActivity.Object == null || addActivity.Target == null)
        {
            _logger.LogWarning("Add activity missing object or target");
            return;
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
            return;
        }

        // Extract collection ID from URL (e.g., .../collections/featured -> featured)
        var collectionId = ExtractCollectionIdFromUrl(collectionUrl);
        if (string.IsNullOrEmpty(collectionId))
        {
            _logger.LogWarning("Could not extract collection ID from URL {CollectionUrl}", collectionUrl);
            return;
        }

        // Get the object to add
        var objectRef = addActivity.Object.FirstOrDefault();
        if (objectRef == null)
        {
            _logger.LogWarning("Add activity has no object");
            return;
        }

        // Handle both full objects and links
        string? objectId = null;
        
        if (objectRef is IObject obj)
        {
            // Full object provided - generate ID if needed and store it
            objectId = obj.Id;
            if (string.IsNullOrEmpty(objectId))
            {
                var baseUrl = (_options.BaseUrl ?? "http://localhost").TrimEnd('/');
                var routePrefix = _options.NormalizedRoutePrefix;
                objectId = $"{baseUrl}{routePrefix}/users/{username}/objects/{Guid.NewGuid()}";
                obj.Id = objectId;
            }

            // Store the object in the outbox
            await _activityRepository.SaveOutboxActivityAsync(username, objectId, obj, cancellationToken);
            _logger.LogInformation("Stored object {ObjectId} for user {Username}", objectId, username);
        }
        else if (objectRef is Link link)
        {
            // Link reference - use the href as the object ID
            objectId = link.Href?.ToString();
            if (string.IsNullOrEmpty(objectId))
            {
                _logger.LogWarning("Add activity link has no href");
                return;
            }

            // TODO: Optionally fetch and cache the remote object
            // For now, we'll just store the reference
        }

        if (string.IsNullOrEmpty(objectId))
        {
            _logger.LogWarning("Add activity object has no ID");
            return;
        }

        // Add to collection
        await _actorRepository.AddToCollectionAsync(username, collectionId, objectId, cancellationToken);
        _logger.LogInformation("Added {ObjectId} to collection {CollectionId} for user {Username}", 
            objectId, collectionId, username);
    }

    private async Task HandleOutgoingRemoveAsync(string username, Remove removeActivity, CancellationToken cancellationToken)
    {
        if (removeActivity.Object == null || removeActivity.Target == null)
        {
            _logger.LogWarning("Remove activity missing object or target");
            return;
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
            return;
        }

        var collectionId = ExtractCollectionIdFromUrl(collectionUrl);
        if (string.IsNullOrEmpty(collectionId))
        {
            _logger.LogWarning("Could not extract collection ID from URL {CollectionUrl}", collectionUrl);
            return;
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
            return;
        }

        // Remove from collection
        await _actorRepository.RemoveFromCollectionAsync(username, collectionId, objectId, cancellationToken);
        _logger.LogInformation("Removed {ObjectId} from collection {CollectionId} for user {Username}", 
            objectId, collectionId, username);
    }

    private string? ExtractCollectionIdFromUrl(string url)
    {
        // Extract collection ID from URL pattern: .../users/{username}/collections/{collectionId}
        var match = System.Text.RegularExpressions.Regex.Match(url, @"/collections/([^/?]+)");
        return match.Success ? match.Groups[1].Value : null;
    }
}
