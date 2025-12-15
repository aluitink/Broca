using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Server.Services;

/// <summary>
/// Service for processing outbox activities
/// </summary>
public class OutboxProcessor
{
    private readonly IActivityRepository _activityRepository;
    private readonly IActorRepository _actorRepository;
    private readonly ActivityDeliveryService _deliveryService;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IActivityRepository activityRepository,
        IActorRepository actorRepository,
        ActivityDeliveryService deliveryService,
        ILogger<OutboxProcessor> logger)
    {
        _activityRepository = activityRepository;
        _actorRepository = actorRepository;
        _deliveryService = deliveryService;
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
            var activityId = activityObj?.Id ?? $"https://localhost/users/{username}/activities/{Guid.NewGuid()}";

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
        if (activity is Activity typedActivity)
        {
            switch (activityType)
            {
                case "Follow":
                case "Like":
                case "Announce":
                case "Accept":
                case "Reject":
                case "Undo":
                    // Extract target actor ID from the activity's object
                    var targetActorId = ExtractTargetActorId(typedActivity);
                    if (!string.IsNullOrEmpty(targetActorId))
                    {
                        await _deliveryService.QueueActivityToTargetAsync(username, activityId, activity, targetActorId, cancellationToken);
                        return;
                    }
                    break;
            }

            // For Create, Update, Delete, etc., check if there are explicit recipients
            var recipients = ExtractRecipients(typedActivity);
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
        switch (activityType)
        {
            case "Follow":
                await HandleOutgoingFollowAsync(username, activity, cancellationToken);
                break;
            case "Undo":
                await HandleOutgoingUndoAsync(username, activity, cancellationToken);
                break;
            case "Add":
                await HandleOutgoingAddAsync(username, activity, cancellationToken);
                break;
            case "Remove":
                await HandleOutgoingRemoveAsync(username, activity, cancellationToken);
                break;
            default:
                // No side effects for other activity types
                break;
        }
    }

    private async Task HandleOutgoingFollowAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        if (activity is Activity followActivity && followActivity.Object != null)
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

    private async Task HandleOutgoingUndoAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        if (activity is not Activity undoActivity || undoActivity.Object == null)
        {
            return;
        }

        var undoObject = undoActivity.Object.FirstOrDefault();
        
        // Handle Undo Follow
        if (undoObject is Activity nestedActivity && 
            nestedActivity.Type?.Contains("Follow") == true &&
            nestedActivity.Object != null)
        {
            var followTarget = nestedActivity.Object.FirstOrDefault();
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

    private async Task HandleOutgoingAddAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        if (activity is not Activity addActivity || addActivity.Object == null || addActivity.Target == null)
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
                objectId = $"https://localhost/users/{username}/objects/{Guid.NewGuid()}";
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

    private async Task HandleOutgoingRemoveAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        if (activity is not Activity removeActivity || removeActivity.Object == null || removeActivity.Target == null)
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
