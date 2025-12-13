using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Server.Services;

/// <summary>
/// Simple identity provider that uses configuration to define a single user
/// </summary>
/// <remarks>
/// This is ideal for personal blogs, single-user instances, or simple use cases
/// where you want to ActivityPub-enable a website without a database.
/// </remarks>
public class SimpleIdentityProvider : IIdentityProvider
{
    private readonly IdentityProviderOptions _options;
    private readonly ILogger<SimpleIdentityProvider> _logger;
    private IdentityDetails? _identity;

    public SimpleIdentityProvider(
        IOptions<IdentityProviderOptions> options,
        ILogger<SimpleIdentityProvider> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IEnumerable<string>> GetUsernamesAsync(CancellationToken cancellationToken = default)
    {
        if (_options.SimpleIdentity?.Username == null)
        {
            _logger.LogWarning("SimpleIdentity.Username is not configured");
            return Task.FromResult(Enumerable.Empty<string>());
        }

        return Task.FromResult<IEnumerable<string>>(new[] { _options.SimpleIdentity.Username });
    }

    public Task<IdentityDetails?> GetIdentityDetailsAsync(string username, CancellationToken cancellationToken = default)
    {
        if (_options.SimpleIdentity?.Username == null || 
            !string.Equals(_options.SimpleIdentity.Username, username, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<IdentityDetails?>(null);
        }

        // Cache the identity details
        if (_identity == null)
        {
            _identity = CreateIdentityDetails();
        }

        return Task.FromResult<IdentityDetails?>(_identity);
    }

    public Task<bool> ExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        if (_options.SimpleIdentity?.Username == null)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(
            string.Equals(_options.SimpleIdentity.Username, username, StringComparison.OrdinalIgnoreCase));
    }

    private IdentityDetails CreateIdentityDetails()
    {
        var config = _options.SimpleIdentity!;

        var identity = new IdentityDetails
        {
            Username = config.Username!,
            DisplayName = config.DisplayName,
            Summary = config.Summary,
            AvatarUrl = config.AvatarUrl,
            HeaderUrl = config.HeaderUrl,
            IsBot = config.IsBot,
            IsLocked = config.IsLocked,
            IsDiscoverable = config.IsDiscoverable,
            Fields = config.Fields,
            ActorType = ParseActorType(config.ActorType)
        };

        // Load keys from files if provided
        if (!string.IsNullOrWhiteSpace(config.PrivateKeyPath) && 
            !string.IsNullOrWhiteSpace(config.PublicKeyPath))
        {
            try
            {
                var privateKey = File.ReadAllText(config.PrivateKeyPath);
                var publicKey = File.ReadAllText(config.PublicKeyPath);

                identity.Keys = new KeyPair
                {
                    PrivateKey = privateKey,
                    PublicKey = publicKey
                };

                _logger.LogInformation("Loaded keys from files for user {Username}", config.Username);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load keys from files for user {Username}, keys will be auto-generated", config.Username);
            }
        }

        return identity;
    }

    private ActorType ParseActorType(string actorType)
    {
        return actorType?.ToLowerInvariant() switch
        {
            "person" => ActorType.Person,
            "organization" => ActorType.Organization,
            "service" => ActorType.Service,
            "application" => ActorType.Application,
            "group" => ActorType.Group,
            _ => ActorType.Person
        };
    }
}
