using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using KristofferStrube.ActivityStreams.JsonLD;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Server.Services;

/// <summary>
/// Service for managing the server's system identity
/// </summary>
public class SystemIdentityService : ISystemIdentityService
{
    private readonly ActivityPubServerOptions _options;
    private readonly IActorRepository _actorRepository;
    private readonly ILogger<SystemIdentityService> _logger;
    private readonly CryptographyService _cryptographyService;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private Actor? _systemActor;
    private string? _systemPrivateKey;

    public string SystemActorId => _options.SystemActorId;
    public string SystemActorAlias => _options.SystemActorAlias;

    public SystemIdentityService(
        IOptions<ActivityPubServerOptions> options,
        IActorRepository actorRepository,
        ILogger<SystemIdentityService> logger,
        CryptographyService cryptographyService)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _actorRepository = actorRepository ?? throw new ArgumentNullException(nameof(actorRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cryptographyService = cryptographyService ?? throw new ArgumentNullException(nameof(cryptographyService));
    }

    public async Task<Actor> GetSystemActorAsync(CancellationToken cancellationToken = default)
    {
        if (_systemActor != null)
        {
            return _systemActor;
        }

        await EnsureSystemActorAsync(cancellationToken);
        return _systemActor!;
    }

    public async Task<string> GetSystemPrivateKeyAsync(CancellationToken cancellationToken = default)
    {
        if (_systemPrivateKey != null)
        {
            return _systemPrivateKey;
        }

        await EnsureSystemActorAsync(cancellationToken);
        return _systemPrivateKey!;
    }

    public async Task EnsureSystemActorAsync(CancellationToken cancellationToken = default)
    {
        if (_systemActor != null && _systemPrivateKey != null)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_systemActor != null && _systemPrivateKey != null)
            {
                return;
            }

            _logger.LogInformation("Initializing system actor: {SystemActorId}", SystemActorId);

            // Try to load existing system actor
            var existingActor = await _actorRepository.GetActorByUsernameAsync(_options.SystemActorUsername, cancellationToken);
            
            if (existingActor != null)
            {
                _logger.LogInformation("Found existing system actor");
                _systemActor = existingActor;

                // Extract private key from extension data if available
                if (existingActor.ExtensionData?.ContainsKey("privateKeyPem") == true)
                {
                    var privateKeyElement = existingActor.ExtensionData["privateKeyPem"];
                    _systemPrivateKey = privateKeyElement.GetString();
                }

                // If we have the actor but no private key, we have a problem
                if (_systemPrivateKey == null)
                {
                    _logger.LogWarning("System actor exists but has no private key - regenerating");
                    await CreateSystemActorAsync(cancellationToken);
                }
            }
            else
            {
                _logger.LogInformation("Creating new system actor");
                await CreateSystemActorAsync(cancellationToken);
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task CreateSystemActorAsync(CancellationToken cancellationToken)
    {
        // Generate RSA key pair using RSACryptoServiceProvider for compatibility with CryptographyService
        using var rsa = new RSACryptoServiceProvider(2048);
        
        var privateKeyPem = ExportPrivateKey(rsa);
        var publicKeyPem = ExportPublicKey(rsa);

        _systemPrivateKey = privateKeyPem;

        var baseUrl = (_options.BaseUrl ?? "http://localhost").TrimEnd('/');
        var routePrefix = _options.NormalizedRoutePrefix;
        var username = _options.SystemActorUsername ?? "sys";
        var actorId = $"{baseUrl}{routePrefix}/users/{username}";

        // Create the system actor
        var actor = new Application
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")),
                new ReferenceTermDefinition(new Uri("https://w3id.org/security/v1")),
                new ObjectTermDefinition
                {
                    ["broca"] = ActivityPubServerOptions.BrocaNamespace
                }
            },
            Id = actorId,
            Type = new[] { "Application" },
            PreferredUsername = username,
            Name = new[] { _options.ServerName },
            Summary = new[] { $"System actor for {_options.ServerName}" },
            Inbox = new Link { Href = new Uri($"{actorId}/inbox") },
            Outbox = new Link { Href = new Uri($"{actorId}/outbox") },
            Following = new Link { Href = new Uri($"{actorId}/following") },
            Followers = new Link { Href = new Uri($"{actorId}/followers") },
            Published = DateTime.UtcNow,
            ExtensionData = new Dictionary<string, JsonElement>
            {
                { "manuallyApprovesFollowers", JsonSerializer.SerializeToElement(false) },
                { "discoverable", JsonSerializer.SerializeToElement(true) },
                {
                    "publicKey",
                    JsonSerializer.SerializeToElement(new
                    {
                        id = $"{actorId}#main-key",
                        owner = actorId,
                        publicKeyPem = publicKeyPem
                    })
                },
                // Store private key (Note: In production, this should be in a secure key store)
                { "privateKeyPem", JsonSerializer.SerializeToElement(privateKeyPem) }
            }
        };

        // Add Broca namespace extension for admin operations if enabled
        if (_options.EnableAdminOperations && !string.IsNullOrWhiteSpace(_options.AdminApiToken))
        {
            actor.ExtensionData["broca:adminOperations"] = JsonSerializer.SerializeToElement(new
            {
                enabled = true,
                authenticationMethods = new[] { "bearer" },
                description = "This server supports administrative operations via ActivityPub protocol with bearer token authentication",
                endpoint = $"{actorId}/inbox"
            });
        }

        await _actorRepository.SaveActorAsync(username, actor, cancellationToken);
        _systemActor = actor;

        _logger.LogInformation("Created system actor: {ActorId}", actorId);
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
