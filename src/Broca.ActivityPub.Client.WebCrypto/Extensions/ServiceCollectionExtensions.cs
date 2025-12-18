using Broca.ActivityPub.Client.Extensions;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Glihm.JSInterop.Browser.WebCryptoAPI.Cryptography.RSA;
using Microsoft.Extensions.DependencyInjection;

namespace Broca.ActivityPub.Client.WebCrypto.Extensions;

/// <summary>
/// Extension methods for configuring ActivityPub Client with WebCrypto support
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds ActivityPub client services with WebCrypto provider for Blazor WebAssembly
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configureOptions">Optional action to configure client options</param>
    /// <returns>Service collection for chaining</returns>
    /// <remarks>
    /// This extension registers the ActivityPub client with the WebCryptoProvider,
    /// which uses the browser's native Web Crypto API for RSA operations.
    /// This is required for Blazor WebAssembly since .NET's RSACryptoServiceProvider
    /// is not supported in WASM environments.
    /// </remarks>
    public static IServiceCollection AddActivityPubClientWithWebCrypto(
        this IServiceCollection services,
        Action<ActivityPubClientOptions>? configureOptions = null)
    {
        // Add WebCrypto RSA services
        services.AddWebCryptoRsa();
        services.AddScoped<WebCryptoProvider>();

        // Add ActivityPub client with standard registration
        if (configureOptions != null)
        {
            services.AddActivityPubClient(configureOptions);
        }
        else
        {
            services.AddActivityPubClient();
        }

        // Override the default CryptoProvider with WebCryptoProvider
        services.AddScoped<ICryptoProvider>(sp => sp.GetRequiredService<WebCryptoProvider>());

        return services;
    }

    /// <summary>
    /// Adds ActivityPub client services in authenticated mode with WebCrypto provider
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="actorId">The ActivityPub actor ID for authenticated requests</param>
    /// <param name="privateKeyPem">PEM-encoded private key for signing HTTP requests</param>
    /// <param name="publicKeyId">The public key ID (typically actorId#main-key)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddActivityPubClientAuthenticatedWithWebCrypto(
        this IServiceCollection services,
        string actorId,
        string privateKeyPem,
        string? publicKeyId = null)
    {
        return services.AddActivityPubClientWithWebCrypto(options =>
        {
            options.ActorId = actorId;
            options.PrivateKeyPem = privateKeyPem;
            options.PublicKeyId = publicKeyId ?? $"{actorId.TrimEnd('#')}#main-key";
        });
    }

    /// <summary>
    /// Adds ActivityPub client services with API key authentication and WebCrypto provider
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="actorId">The ActivityPub actor ID</param>
    /// <param name="apiKey">API key for authenticating with the server</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddActivityPubClientWithApiKeyAndWebCrypto(
        this IServiceCollection services,
        string actorId,
        string apiKey)
    {
        return services.AddActivityPubClientWithWebCrypto(options =>
        {
            options.ActorId = actorId;
            options.ApiKey = apiKey;
        });
    }
}
