using System.Security.Cryptography;
using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using KristofferStrube.ActivityStreams.JsonLD;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Server.Services;

/// <summary>
/// Service that manages identity provider integration and actor lifecycle
/// </summary>
/// <remarks>
/// This service bridges the IIdentityProvider abstraction with the ActivityPub server,
/// automatically creating and managing actors based on the identity provider's data.
/// </remarks>
public class IdentityProviderService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IActorRepository _actorRepository;
    private readonly ActivityPubServerOptions _serverOptions;
    private readonly CryptographyService _cryptographyService;
    private readonly ILogger<IdentityProviderService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public IdentityProviderService(
        IIdentityProvider identityProvider,
        IActorRepository actorRepository,
        IOptions<ActivityPubServerOptions> serverOptions,
        CryptographyService cryptographyService,
        ILogger<IdentityProviderService> logger)
    {
        _identityProvider = identityProvider ?? throw new ArgumentNullException(nameof(identityProvider));
        _actorRepository = actorRepository ?? throw new ArgumentNullException(nameof(actorRepository));
        _serverOptions = serverOptions?.Value ?? throw new ArgumentNullException(nameof(serverOptions));
        _cryptographyService = cryptographyService ?? throw new ArgumentNullException(nameof(cryptographyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes all identities from the provider
    /// </summary>
    public async Task InitializeIdentitiesAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            _logger.LogInformation("Initializing identities from provider");

            var usernames = await _identityProvider.GetUsernamesAsync(cancellationToken);
            var initTasks = usernames.Select(username => 
                EnsureActorExistsAsync(username, cancellationToken));

            await Task.WhenAll(initTasks);

            _initialized = true;
            _logger.LogInformation("Initialized {Count} identities", usernames.Count());
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Ensures an actor exists for the given username
    /// </summary>
    public async Task<Actor?> EnsureActorExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        // Check if actor already exists
        var existingActor = await _actorRepository.GetActorByUsernameAsync(username, cancellationToken);
        if (existingActor != null)
        {
            _logger.LogDebug("Actor {Username} already exists", username);
            return existingActor;
        }

        // Get identity details from provider
        var identityDetails = await _identityProvider.GetIdentityDetailsAsync(username, cancellationToken);
        if (identityDetails == null)
        {
            _logger.LogWarning("Identity provider returned null for username {Username}", username);
            return null;
        }

        _logger.LogInformation("Creating new actor for {Username}", username);
        return await CreateActorFromIdentityAsync(identityDetails, cancellationToken);
    }

    /// <summary>
    /// Gets an actor, creating it if it doesn't exist
    /// </summary>
    public async Task<Actor?> GetOrCreateActorAsync(string username, CancellationToken cancellationToken = default)
    {
        var actor = await _actorRepository.GetActorByUsernameAsync(username, cancellationToken);
        if (actor != null)
        {
            return actor;
        }

        // Check if the identity exists
        var exists = await _identityProvider.ExistsAsync(username, cancellationToken);
        if (!exists)
        {
            return null;
        }

        return await EnsureActorExistsAsync(username, cancellationToken);
    }

    private async Task<Actor> CreateActorFromIdentityAsync(IdentityDetails identity, CancellationToken cancellationToken)
    {
        var baseUrl = (_serverOptions.BaseUrl ?? "http://localhost").TrimEnd('/');
        var routePrefix = _serverOptions.NormalizedRoutePrefix;
        var username = identity.Username;
        var actorId = $"{baseUrl}{routePrefix}/users/{username}";

        // Generate or use provided keys
        string privateKeyPem;
        string publicKeyPem;

        if (identity.Keys != null)
        {
            privateKeyPem = identity.Keys.PrivateKey;
            publicKeyPem = identity.Keys.PublicKey;
            _logger.LogDebug("Using provided keys for {Username}", username);
        }
        else
        {
            // Generate new RSA key pair
            using var rsa = new RSACryptoServiceProvider(2048);
            privateKeyPem = ExportPrivateKey(rsa);
            publicKeyPem = ExportPublicKey(rsa);
            _logger.LogDebug("Generated new keys for {Username}", username);
        }

        // Create actor based on type
        Actor actor = identity.ActorType switch
        {
            ActorType.Organization => new Organization(),
            ActorType.Service => new Service(),
            ActorType.Application => new Application(),
            ActorType.Group => new Group(),
            _ => new Person()
        };

        // Set common properties
        actor.JsonLDContext = new List<ITermDefinition>
        {
            new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")),
            new ReferenceTermDefinition(new Uri("https://w3id.org/security/v1"))
        };
        actor.Id = actorId;
        actor.Type = new[] { identity.ActorType.ToString() };
        actor.PreferredUsername = username;
        actor.Name = string.IsNullOrWhiteSpace(identity.DisplayName) 
            ? new[] { username } 
            : new[] { identity.DisplayName };
        
        if (!string.IsNullOrWhiteSpace(identity.Summary))
        {
            actor.Summary = new[] { identity.Summary };
        }

        actor.Inbox = new Link { Href = new Uri($"{actorId}/inbox") };
        actor.Outbox = new Link { Href = new Uri($"{actorId}/outbox") };
        actor.Following = new Link { Href = new Uri($"{actorId}/following") };
        actor.Followers = new Link { Href = new Uri($"{actorId}/followers") };
        actor.Published = DateTime.UtcNow;

        // Add avatar/icon if provided
        if (!string.IsNullOrWhiteSpace(identity.AvatarUrl))
        {
            actor.Icon = new List<IImageOrLink>
            {
                new Image 
                { 
                    Url = new Link[] { new() { Href = new Uri(identity.AvatarUrl) } }
                }
            };
        }

        // Add header/banner if provided
        if (!string.IsNullOrWhiteSpace(identity.HeaderUrl))
        {
            actor.Image = new List<IImageOrLink>
            {
                new Image 
                { 
                    Url = new Link[] { new() { Href = new Uri(identity.HeaderUrl) } }
                }
            };
        }

        // Build extension data
        var extensionData = new Dictionary<string, JsonElement>
        {
            { "manuallyApprovesFollowers", JsonSerializer.SerializeToElement(identity.IsLocked) },
            { "discoverable", JsonSerializer.SerializeToElement(identity.IsDiscoverable) },
            {
                "publicKey",
                JsonSerializer.SerializeToElement(new
                {
                    id = $"{actorId}#main-key",
                    owner = actorId,
                    publicKeyPem = publicKeyPem
                })
            },
            // Store private key (Note: In production, consider using a secure key store)
            { "privateKeyPem", JsonSerializer.SerializeToElement(privateKeyPem) }
        };

        if (identity.IsBot)
        {
            extensionData["type"] = JsonSerializer.SerializeToElement("Bot");
        }

        // Add custom fields if provided
        if (identity.Fields?.Any() == true)
        {
            var attachments = identity.Fields.Select(field => new
            {
                type = "PropertyValue",
                name = field.Key,
                value = field.Value
            }).ToArray();

            extensionData["attachment"] = JsonSerializer.SerializeToElement(attachments);
        }

        actor.ExtensionData = extensionData;

        // Save to repository
        await _actorRepository.SaveActorAsync(username, actor, cancellationToken);

        _logger.LogInformation("Created actor: {ActorId}", actorId);
        return actor;
    }

    private string ExportPrivateKey(RSACryptoServiceProvider rsa)
    {
        using var writer = new StringWriter();
        _cryptographyService.ExportPrivateKeyPem(rsa, writer);
        return writer.ToString();
    }

    private string ExportPublicKey(RSACryptoServiceProvider rsa)
    {
        using var writer = new StringWriter();
        _cryptographyService.ExportPublicKey(rsa, writer);
        return writer.ToString();
    }
}
