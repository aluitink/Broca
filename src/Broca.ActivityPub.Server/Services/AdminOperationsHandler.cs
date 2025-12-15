using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using KristofferStrube.ActivityStreams.JsonLD;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Server.Services;

/// <summary>
/// Handles administrative operations received via ActivityPub protocol
/// </summary>
/// <remarks>
/// This service provides a back-channel administrative interface using ActivityPub.
/// Operations are sent to the system actor's inbox and must be properly signed
/// by authorized admin actors.
/// </remarks>
public class AdminOperationsHandler
{
    private readonly IActorRepository _actorRepository;
    private readonly IActivityRepository _activityRepository;
    private readonly ICollectionService _collectionService;
    private readonly CryptographyService _cryptographyService;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<AdminOperationsHandler> _logger;

    public AdminOperationsHandler(
        IActorRepository actorRepository,
        IActivityRepository activityRepository,
        ICollectionService collectionService,
        CryptographyService cryptographyService,
        IOptions<ActivityPubServerOptions> options,
        ILogger<AdminOperationsHandler> logger)
    {
        _actorRepository = actorRepository;
        _activityRepository = activityRepository;
        _collectionService = collectionService;
        _cryptographyService = cryptographyService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Checks if an actor is authorized to perform administrative operations
    /// </summary>
    public bool IsAuthorizedAdminActor(string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return false;
        }

        // System actor is always authorized
        if (actorId == _options.SystemActorId)
        {
            return true;
        }

        // Check configured authorized admin actors
        if (_options.AuthorizedAdminActors?.Contains(actorId) == true)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles administrative activities sent to the system inbox
    /// </summary>
    /// <param name="activity">The activity to handle</param>
    /// <param name="isBearerTokenAuthenticated">True if request was authenticated via bearer token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if handled successfully</returns>
    public async Task<bool> HandleAdminActivityAsync(IObjectOrLink activity, bool isBearerTokenAuthenticated = false, CancellationToken cancellationToken = default)
    {
        var activityType = activity.Type?.FirstOrDefault();
        if (string.IsNullOrEmpty(activityType))
        {
            _logger.LogWarning("Administrative activity missing type property");
            return false;
        }

        // Extract actor who sent the activity
        var activityObj = activity as IObject;
        var actorId = ExtractActorId(activityObj);
        
        // If authenticated via bearer token, skip actor validation
        // The bearer token itself provides authentication and authorization
        if (!isBearerTokenAuthenticated)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                _logger.LogWarning("Administrative activity missing actor property");
                return false;
            }

            // Verify authorization via actor ID
            if (!IsAuthorizedAdminActor(actorId))
            {
                _logger.LogWarning("Unauthorized administrative operation from {ActorId}", actorId);
                return false;
            }

            _logger.LogInformation("Processing administrative {ActivityType} from {ActorId}", activityType, actorId);
        }
        else
        {
            _logger.LogInformation("Processing administrative {ActivityType} authenticated via bearer token", activityType);
        }

        // Route to appropriate handler
        return activityType switch
        {
            "Create" => await HandleCreateActorAsync(activityObj, cancellationToken),
            "Update" => await HandleUpdateActorAsync(activityObj, cancellationToken),
            "Delete" => await HandleDeleteActorAsync(activityObj, cancellationToken),
            _ => await HandleUnknownAdminActivityAsync(activityType, activityObj, cancellationToken)
        };
    }

    /// <summary>
    /// Handles Create activity with an Actor object to create a new user
    /// </summary>
    private async Task<bool> HandleCreateActorAsync(IObject? activity, CancellationToken cancellationToken)
    {
        if (activity is not Activity createActivity)
        {
            _logger.LogWarning("Create admin activity is not an Activity type");
            return false;
        }

        var actorObject = createActivity.Object?.FirstOrDefault();
        if (actorObject == null)
        {
            _logger.LogWarning("Create admin activity missing object property");
            return false;
        }

        // Check if this is a Collection object (for custom collections)
        if (actorObject is Collection || actorObject.Type?.Contains("Collection") == true)
        {
            return await HandleCreateCollectionAsync(createActivity, cancellationToken);
        }

        // Check if object is an Actor type
        Actor? newActor = actorObject switch
        {
            Person person => person,
            Application app => app,
            Organization org => org,
            Service service => service,
            Group group => group,
            Actor actor => actor,
            _ => null
        };

        if (newActor == null)
        {
            _logger.LogWarning("Create admin activity object is not an Actor type. Type: {Type}", 
                actorObject.Type?.FirstOrDefault());
            return false;
        }

        // Extract username from preferredUsername
        var username = newActor.PreferredUsername;
        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning("Actor object missing preferredUsername");
            return false;
        }

        // Check if actor already exists
        var existingActor = await _actorRepository.GetActorByUsernameAsync(username, cancellationToken);
        if (existingActor != null)
        {
            _logger.LogWarning("Actor with username {Username} already exists", username);
            return false;
        }

        // Generate RSA key pair for the new actor
        using var rsa = new RSACryptoServiceProvider(2048);
        var privateKeyPem = ExportPrivateKey(rsa);
        var publicKeyPem = ExportPublicKey(rsa);

        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var routePrefix = _options.NormalizedRoutePrefix;
        var actorId = $"{baseUrl}{routePrefix}/users/{username}";

        // Ensure the actor has proper ActivityPub structure
        newActor.Id = actorId;
        newActor.Inbox = new Link { Href = new Uri($"{actorId}/inbox") };
        newActor.Outbox = new Link { Href = new Uri($"{actorId}/outbox") };
        newActor.Following = new Link { Href = new Uri($"{actorId}/following") };
        newActor.Followers = new Link { Href = new Uri($"{actorId}/followers") };
        
        if (newActor.JsonLDContext == null || !newActor.JsonLDContext.Any())
        {
            newActor.JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")),
                new ReferenceTermDefinition(new Uri("https://w3id.org/security/v1"))
            };
        }

        // Add public key to extension data
        var publicKeyData = new Dictionary<string, object>
        {
            ["id"] = $"{actorId}#main-key",
            ["owner"] = actorId,
            ["publicKeyPem"] = publicKeyPem
        };

        newActor.ExtensionData ??= new Dictionary<string, JsonElement>();
        newActor.ExtensionData["publicKey"] = JsonSerializer.SerializeToElement(publicKeyData);
        newActor.ExtensionData["privateKeyPem"] = JsonSerializer.SerializeToElement(privateKeyPem);

        // Save the actor
        await _actorRepository.SaveActorAsync(username, newActor, cancellationToken);

        _logger.LogInformation("Created new actor via admin operation: {Username} ({ActorId})", username, actorId);
        
        return true;
    }

    /// <summary>
    /// Handles Update activity to modify an existing actor
    /// </summary>
    private async Task<bool> HandleUpdateActorAsync(IObject? activity, CancellationToken cancellationToken)
    {
        if (activity is not Activity updateActivity)
        {
            return false;
        }

        var actorObject = updateActivity.Object?.FirstOrDefault();
        if (actorObject is not Actor updatedActor)
        {
            _logger.LogWarning("Update admin activity object is not an Actor type");
            return false;
        }

        var username = updatedActor.PreferredUsername;
        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning("Actor object missing preferredUsername");
            return false;
        }

        // Check if actor exists
        var existingActor = await _actorRepository.GetActorByUsernameAsync(username, cancellationToken);
        if (existingActor == null)
        {
            _logger.LogWarning("Actor with username {Username} not found", username);
            return false;
        }

        // Prevent updating system actor via admin operations
        if (username == _options.SystemActorUsername)
        {
            _logger.LogWarning("Cannot update system actor via admin operations");
            return false;
        }

        // Update the actor (preserving private key if not provided)
        if (updatedActor.ExtensionData?.ContainsKey("privateKeyPem") != true &&
            existingActor.ExtensionData?.ContainsKey("privateKeyPem") == true)
        {
            updatedActor.ExtensionData ??= new Dictionary<string, JsonElement>();
            updatedActor.ExtensionData["privateKeyPem"] = existingActor.ExtensionData["privateKeyPem"];
        }

        await _actorRepository.SaveActorAsync(username, updatedActor, cancellationToken);

        _logger.LogInformation("Updated actor via admin operation: {Username}", username);
        
        return true;
    }

    /// <summary>
    /// Handles Delete activity to remove an actor
    /// </summary>
    private async Task<bool> HandleDeleteActorAsync(IObject? activity, CancellationToken cancellationToken)
    {
        if (activity is not Activity deleteActivity)
        {
            return false;
        }

        var objectToDelete = deleteActivity.Object?.FirstOrDefault();
        if (objectToDelete == null)
        {
            return false;
        }

        // Extract username from the object
        string? username = null;
        string? actorIdToDelete = null;
        
        if (objectToDelete is Actor actor)
        {
            username = actor.PreferredUsername;
            actorIdToDelete = actor.Id;
        }
        else if (objectToDelete is Link link && link.Href != null)
        {
            // Extract from Link object (e.g., from ActivityBuilder.Delete())
            actorIdToDelete = link.Href.ToString();
        }
        else if (objectToDelete is IObject obj && !string.IsNullOrWhiteSpace(obj.Id))
        {
            // Extract from IObject with Id property
            actorIdToDelete = obj.Id;
        }

        // If we have an actorId but no username yet, try to extract it from the ID
        if (string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(actorIdToDelete))
        {
            // Try to extract username from actor ID (e.g., https://domain.com/users/username)
            var parts = actorIdToDelete.Split('/');
            if (parts.Length >= 2 && parts[^2] == "users")
            {
                username = parts[^1];
            }
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning("Could not extract username from Delete admin activity");
            return false;
        }

        // Prevent deleting system actor
        if (username == _options.SystemActorUsername)
        {
            _logger.LogWarning("Cannot delete system actor via admin operations");
            return false;
        }

        // Check if actor exists
        var existingActor = await _actorRepository.GetActorByUsernameAsync(username, cancellationToken);
        if (existingActor == null)
        {
            _logger.LogWarning("Actor with username {Username} not found", username);
            return false;
        }

        await _actorRepository.DeleteActorAsync(username, cancellationToken);

        _logger.LogInformation("Deleted actor via admin operation: {Username}", username);
        
        return true;
    }

    /// <summary>
    /// Handles Create activity with a Collection object to create a custom collection
    /// </summary>
    private async Task<bool> HandleCreateCollectionAsync(Activity createActivity, CancellationToken cancellationToken)
    {
        var collectionObject = createActivity.Object?.FirstOrDefault();
        if (collectionObject == null)
        {
            _logger.LogWarning("Create collection activity missing object property");
            return false;
        }

        // Extract the target actor from the 'attributedTo' or 'actor' field
        string? targetUsername = null;
        
        // Check attributedTo on the collection object
        if (collectionObject is IObject obj && obj.AttributedTo != null)
        {
            var attributedToRef = obj.AttributedTo.FirstOrDefault();
            var actorId = attributedToRef switch
            {
                Link link => link.Href?.ToString(),
                IObject o => o.Id,
                _ => null
            };

            if (actorId != null)
            {
                var actor = await _actorRepository.GetActorByIdAsync(actorId, cancellationToken);
                targetUsername = actor?.PreferredUsername;
            }
        }

        // If not found, use the actor who sent the Create activity
        if (targetUsername == null)
        {
            var actorId = ExtractActorId(createActivity);
            if (actorId != null)
            {
                var actor = await _actorRepository.GetActorByIdAsync(actorId, cancellationToken);
                targetUsername = actor?.PreferredUsername;
            }
        }

        if (string.IsNullOrWhiteSpace(targetUsername))
        {
            _logger.LogWarning("Could not determine target actor for collection creation");
            return false;
        }

        // Extract collection definition from extension data
        // Look for Broca-specific extension: "broca:collectionDefinition"
        CustomCollectionDefinition? definition = null;
        
        if (collectionObject is IObject collectionObj && collectionObj.ExtensionData != null)
        {
            // Try to find broca:collectionDefinition or collectionDefinition
            if (collectionObj.ExtensionData.TryGetValue("collectionDefinition", out var defElement) ||
                collectionObj.ExtensionData.TryGetValue("broca:collectionDefinition", out defElement))
            {
                try
                {
                    definition = JsonSerializer.Deserialize<CustomCollectionDefinition>(defElement.GetRawText());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize collection definition from extension data");
                    return false;
                }
            }
        }

        // If no extension data found, try to construct from standard Collection properties
        if (definition == null)
        {
            if (collectionObject is not IObject collObj)
            {
                _logger.LogWarning("Collection object is not an IObject");
                return false;
            }

            var collectionId = collObj.Name?.FirstOrDefault() ?? Guid.NewGuid().ToString();
            definition = new CustomCollectionDefinition
            {
                Id = collectionId,
                Name = collObj.Name?.FirstOrDefault() ?? collectionId,
                Description = collObj.Summary?.FirstOrDefault()?.ToString(),
                Type = CollectionType.Manual, // Default to manual
                Visibility = CollectionVisibility.Public, // Default to public
                Created = DateTimeOffset.UtcNow,
                Updated = DateTimeOffset.UtcNow
            };
        }

        // Create the collection
        try
        {
            await _collectionService.CreateCollectionAsync(targetUsername, definition, cancellationToken);
            _logger.LogInformation("Created custom collection {CollectionId} for {Username} via admin operation", 
                definition.Id, targetUsername);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create collection {CollectionId} for {Username}", 
                definition.Id, targetUsername);
            return false;
        }
    }

    /// <summary>
    /// Handles unknown administrative activity types (extensibility point)
    /// </summary>
    private Task<bool> HandleUnknownAdminActivityAsync(string activityType, IObject? activity, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Unknown administrative activity type: {ActivityType}", activityType);
        // Could be extended to handle custom admin operations
        return Task.FromResult(false);
    }

    private string? ExtractActorId(IObject? activity)
    {
        if (activity is Activity act && act.Actor != null)
        {
            var actorRef = act.Actor.FirstOrDefault();
            return actorRef switch
            {
                Link link => link.Href?.ToString(),
                IObject obj => obj.Id,
                _ => null
            };
        }
        return null;
    }

    private string ExportPrivateKey(RSACryptoServiceProvider rsa)
    {
        var privateKeyBytes = rsa.ExportRSAPrivateKey();
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN PRIVATE KEY-----");
        sb.AppendLine(Convert.ToBase64String(privateKeyBytes, Base64FormattingOptions.InsertLineBreaks));
        sb.AppendLine("-----END PRIVATE KEY-----");
        return sb.ToString();
    }

    private string ExportPublicKey(RSACryptoServiceProvider rsa)
    {
        var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN PUBLIC KEY-----");
        sb.AppendLine(Convert.ToBase64String(publicKeyBytes, Base64FormattingOptions.InsertLineBreaks));
        sb.AppendLine("-----END PUBLIC KEY-----");
        return sb.ToString();
    }
}
