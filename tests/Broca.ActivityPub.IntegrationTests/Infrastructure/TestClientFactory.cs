using Broca.ActivityPub.Client.Services;
using Broca.ActivityPub.Client.Extensions;
using Broca.ActivityPub.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Broca.ActivityPub.IntegrationTests.Infrastructure;

/// <summary>
/// Helper class for creating and managing ActivityPub test clients
/// </summary>
public static class TestClientFactory
{
    /// <summary>
    /// Creates a basic (unauthenticated) ActivityPub client for testing
    /// </summary>
    public static IActivityPubClient CreateClient(Func<HttpClient> httpClientFactory)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHttpClientFactory>(new TestHttpClientFactory(httpClientFactory));
        services.AddActivityPubClient();
        
        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IActivityPubClient>();
    }

    /// <summary>
    /// Creates an authenticated ActivityPub client for testing
    /// </summary>
    /// <param name="httpClientFactory">Factory to create HttpClient instances</param>
    /// <param name="actorId">The actor ID for authentication</param>
    /// <param name="privateKeyPem">The private key PEM for signing requests</param>
    /// <param name="publicKeyId">Optional public key ID (defaults to {actorId}#main-key)</param>
    public static IActivityPubClient CreateAuthenticatedClient(
        Func<HttpClient> httpClientFactory,
        string actorId,
        string privateKeyPem,
        string? publicKeyId = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHttpClientFactory>(new TestHttpClientFactory(httpClientFactory));
        services.AddActivityPubClientAuthenticated(actorId, privateKeyPem, publicKeyId);
        
        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IActivityPubClient>();
    }

    /// <summary>
    /// Simple HttpClientFactory implementation for testing
    /// </summary>
    private class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpClient> _factory;

        public TestHttpClientFactory(Func<HttpClient> factory)
        {
            _factory = factory;
        }

        public HttpClient CreateClient(string name)
        {
            return _factory();
        }
    }
}
