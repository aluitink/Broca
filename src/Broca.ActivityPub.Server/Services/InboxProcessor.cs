using System.Text.Json;
using Broca.ActivityPub.Client.Services;
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
    private readonly HttpSignatureService _signatureService;
    private readonly AdminOperationsHandler _adminOperationsHandler;
    private readonly AttachmentProcessingService _attachmentProcessingService;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<InboxProcessor> _logger;

    public InboxProcessor(
        IActivityRepository activityRepository,
        IActorRepository actorRepository,
        IActivityBuilderFactory activityBuilderFactory,
        HttpSignatureService signatureService,
        AdminOperationsHandler adminOperationsHandler,
        AttachmentProcessingService attachmentProcessingService,
        IOptions<ActivityPubServerOptions> options,
        ILogger<InboxProcessor> logger)
    {
        _activityRepository = activityRepository;
        _actorRepository = actorRepository;
        _activityBuilderFactory = activityBuilderFactory;
        _signatureService = signatureService;
        _adminOperationsHandler = adminOperationsHandler;
        _attachmentProcessingService = attachmentProcessingService;
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

            _logger.LogInformation("Processing {ActivityType} activity {ActivityId} for user {Username}",
                activityType, activityId, username);

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
                "Create" => await HandleCreateAsync(username, activity as IObject, cancellationToken),
                "Delete" => await HandleDeleteAsync(username, activity as IObject, cancellationToken),
                "Like" => await HandleLikeAsync(username, activity as IObject, cancellationToken),
                "Announce" => await HandleAnnounceAsync(username, activity as IObject, cancellationToken),
                "Add" => await HandleAddAsync(username, activity as IObject, cancellationToken),
                "Remove" => await HandleRemoveAsync(username, activity as IObject, cancellationToken),
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
            Link link => link.Href?.ToString(),
            IObject obj => obj.Id,
            _ => null
        };

        if (followerActorId == null)
        {
            return false;
        }

        // Add follower
        await _actorRepository.AddFollowerAsync(username, followerActorId, cancellationToken);

        _logger.LogInformation("Added follower {FollowerActorId} to {Username}", followerActorId, username);

        // TODO: Send Accept activity back to the follower
        // This would typically be done by posting to the outbox

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

        // Check if it's an Undo Follow
        if (objectToUndo is IObject obj && obj.Type?.Contains("Follow") == true)
        {
            var originalFollow = obj as Activity;
            if (originalFollow?.Actor != null)
            {
                var followerActorId = originalFollow.Actor.FirstOrDefault() switch
                {
                    Link link => link.Href?.ToString(),
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

        return false;
    }

    private Task<bool> HandleAcceptAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        // Accept activities are informational
        // The follower has accepted our follow request
        return Task.FromResult(true);
    }

    private Task<bool> HandleCreateAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        // Create activities are already stored in the inbox
        // Additional processing (e.g., notification) could be done here
        return Task.FromResult(true);
    }

    private Task<bool> HandleDeleteAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        // Delete activities indicate content should be removed
        // This would typically mark objects as deleted in the repository
        return Task.FromResult(true);
    }

    private Task<bool> HandleLikeAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        // Like activities are informational
        // Could update like counts on objects
        return Task.FromResult(true);
    }

    private Task<bool> HandleAnnounceAsync(string username, IObject? activity, CancellationToken cancellationToken)
    {
        // Announce activities (shares/boosts) are informational
        // Could update share counts on objects
        return Task.FromResult(true);
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
                objectId = $"https://localhost/users/{username}/objects/{Guid.NewGuid()}";
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

    /// <summary>
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
