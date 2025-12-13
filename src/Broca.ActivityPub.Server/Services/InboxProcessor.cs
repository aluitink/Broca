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
