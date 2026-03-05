using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Server.Services;

/// <summary>
/// Provides convenience methods for creating authenticated ActivityPub clients
/// </summary>
public class SignedClientProvider
{
    private readonly IActivityPubClientFactory _clientFactory;
    private readonly IActorRepository _actorRepository;
    private readonly ISystemIdentityService _systemIdentityService;
    private readonly ILogger<SignedClientProvider> _logger;

    public SignedClientProvider(
        IActivityPubClientFactory clientFactory,
        IActorRepository actorRepository,
        ISystemIdentityService systemIdentityService,
        ILogger<SignedClientProvider> logger)
    {
        _clientFactory = clientFactory;
        _actorRepository = actorRepository;
        _systemIdentityService = systemIdentityService;
        _logger = logger;
    }

    /// <summary>
    /// Creates an authenticated client for the specified actor by loading from repository
    /// </summary>
    public async Task<IActivityPubClient?> CreateForActorIdAsync(string actorId, CancellationToken cancellationToken = default)
    {
        var actor = await _actorRepository.GetActorByIdAsync(actorId, cancellationToken);
        if (actor == null)
        {
            _logger.LogWarning("Cannot create signed client: actor {ActorId} not found", actorId);
            return null;
        }

        return CreateForActor(actor, actorId);
    }

    /// <summary>
    /// Creates an authenticated client for the specified actor object
    /// </summary>
    public IActivityPubClient? CreateForActor(Actor actor, string actorId)
    {
        // Extract private key from actor's extension data
        string? privateKeyPem = null;
        if (actor.ExtensionData?.TryGetValue("privateKeyPem", out var privateKeyObj) == true
            && privateKeyObj is JsonElement privateKeyElement
            && privateKeyElement.ValueKind == JsonValueKind.String)
        {
            privateKeyPem = privateKeyElement.GetString();
        }

        if (string.IsNullOrEmpty(privateKeyPem))
        {
            _logger.LogWarning("Cannot create signed client: actor {ActorId} has no private key", actorId);
            return null;
        }

        // Determine public key ID
        var publicKeyId = $"{actorId}#main-key";
        if (actor.ExtensionData?.TryGetValue("publicKey", out var publicKeyObj) == true
            && publicKeyObj is JsonElement publicKeyElement
            && publicKeyElement.TryGetProperty("id", out var idElement))
        {
            publicKeyId = idElement.GetString() ?? publicKeyId;
        }

        return _clientFactory.CreateForActor(actorId, publicKeyId, privateKeyPem);
    }

    /// <summary>
    /// Creates an authenticated client for the system actor
    /// </summary>
    public async Task<IActivityPubClient> CreateForSystemActorAsync(CancellationToken cancellationToken = default)
    {
        var systemActor = await _systemIdentityService.GetSystemActorAsync(cancellationToken);
        var privateKey = await _systemIdentityService.GetSystemPrivateKeyAsync(cancellationToken);
        var publicKeyId = $"{systemActor.Id}#main-key";

        return _clientFactory.CreateForActor(systemActor.Id!, publicKeyId, privateKey);
    }
}
