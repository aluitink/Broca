using Broca.ActivityPub.Client.Services;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;

namespace Broca.ActivityPub.Client.Extensions;

/// <summary>
/// Extension methods for configuring Broca ActivityPub Client
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds ActivityPub client services in anonymous mode (no authentication)
    /// </summary>
    /// <remarks>
    /// Anonymous mode allows browsing public ActivityPub content without signing requests.
    /// Use this when you don't need to authenticate as a specific actor.
    /// </remarks>
    public static IServiceCollection AddActivityPubClient(this IServiceCollection services)
    {
        return services.AddActivityPubClient(options => { });
    }

    /// <summary>
    /// Adds ActivityPub client services with custom configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Action to configure client options</param>
    public static IServiceCollection AddActivityPubClient(
        this IServiceCollection services,
        Action<ActivityPubClientOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        // Register options
        services.Configure(configureOptions);

        // Register core services
        services.TryAddSingleton<ICryptoProvider, CryptoProvider>();
        services.TryAddSingleton<HttpSignatureService>();
        services.TryAddSingleton<IWebFingerService, WebFingerService>();
        
        // Register HTTP client factory
        services.AddHttpClient("ActivityPub", client =>
        {
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/activity+json"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/ld+json"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        // Register proxy service for CORS fallback
        services.TryAddScoped<ProxyService>();

        // Register the base ActivityPub client
        services.TryAddScoped<ActivityPubClient>();
        
        // Register the resilient client wrapper as the primary IActivityPubClient
        services.TryAddScoped<IActivityPubClient>(sp =>
        {
            var baseClient = sp.GetRequiredService<ActivityPubClient>();
            var proxyService = sp.GetRequiredService<ProxyService>();
            var logger = sp.GetRequiredService<ILogger<ResilientActivityPubClient>>();
            return new ResilientActivityPubClient(baseClient, proxyService, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds ActivityPub client services in authenticated mode
    /// </summary>
    /// <remarks>
    /// Authenticated mode signs all requests with the actor's private key.
    /// This allows accessing protected resources and posting activities.
    /// </remarks>
    /// <param name="services">Service collection</param>
    /// <param name="actorId">The ActivityPub actor ID (e.g., https://example.com/users/alice)</param>
    /// <param name="privateKeyPem">PEM-encoded RSA private key</param>
    /// <param name="publicKeyId">Public key ID (e.g., https://example.com/users/alice#main-key)</param>
    public static IServiceCollection AddActivityPubClientAuthenticated(
        this IServiceCollection services,
        string actorId,
        string privateKeyPem,
        string? publicKeyId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPem);

        // Default publicKeyId to actorId#main-key if not provided
        publicKeyId ??= $"{actorId.TrimEnd('#')}#main-key";

        return services.AddActivityPubClient(options =>
        {
            options.ActorId = actorId;
            options.PrivateKeyPem = privateKeyPem;
            options.PublicKeyId = publicKeyId;
        });
    }

    /// <summary>
    /// Adds ActivityPub client services in authenticated mode with additional configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="actorId">The ActivityPub actor ID</param>
    /// <param name="privateKeyPem">PEM-encoded RSA private key</param>
    /// <param name="publicKeyId">Public key ID</param>
    /// <param name="configureOptions">Action to configure additional options</param>
    public static IServiceCollection AddActivityPubClientAuthenticated(
        this IServiceCollection services,
        string actorId,
        string privateKeyPem,
        string? publicKeyId,
        Action<ActivityPubClientOptions> configureOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPem);
        ArgumentNullException.ThrowIfNull(configureOptions);

        // Default publicKeyId to actorId#main-key if not provided
        publicKeyId ??= $"{actorId.TrimEnd('#')}#main-key";

        return services.AddActivityPubClient(options =>
        {
            options.ActorId = actorId;
            options.PrivateKeyPem = privateKeyPem;
            options.PublicKeyId = publicKeyId;
            configureOptions(options);
        });
    }

    /// <summary>
    /// Adds ActivityPub client services with API key authentication
    /// </summary>
    /// <remarks>
    /// The client will be configured with an API key and will fetch the actor's
    /// private key from the server when InitializeAsync() is called.
    /// The API key should match the server's AdminApiToken configuration.
    /// </remarks>
    /// <param name="services">Service collection</param>
    /// <param name="actorId">The ActivityPub actor ID (e.g., https://example.com/users/alice)</param>
    /// <param name="apiKey">API key for authenticating with the server</param>
    public static IServiceCollection AddActivityPubClientWithApiKey(
        this IServiceCollection services,
        string actorId,
        string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        return services.AddActivityPubClient(options =>
        {
            options.ActorId = actorId;
            options.ApiKey = apiKey;
        });
    }

    /// <summary>
    /// Adds ActivityPub client services with API key authentication and additional configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="actorId">The ActivityPub actor ID</param>
    /// <param name="apiKey">API key for authenticating with the server</param>
    /// <param name="configureOptions">Action to configure additional options</param>
    public static IServiceCollection AddActivityPubClientWithApiKey(
        this IServiceCollection services,
        string actorId,
        string apiKey,
        Action<ActivityPubClientOptions> configureOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentNullException.ThrowIfNull(configureOptions);

        return services.AddActivityPubClient(options =>
        {
            options.ActorId = actorId;
            options.ApiKey = apiKey;
            configureOptions(options);
        });
    }
}


