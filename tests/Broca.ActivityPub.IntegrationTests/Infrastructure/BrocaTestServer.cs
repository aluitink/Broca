using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Persistence.InMemory;

namespace Broca.ActivityPub.IntegrationTests.Infrastructure;

/// <summary>
/// Server factory for creating isolated ActivityPub server instances for testing
/// Each instance has its own in-memory repositories and base URL
/// </summary>
public class BrocaTestServer : WebApplicationFactory<Broca.API.Program>, IAsyncDisposable
{
    private readonly string _baseUrl;
    private readonly string _domain;
    private readonly string _instanceName;
    private readonly TestServerRoutingHandler? _routingHandler;
    private readonly Dictionary<string, string?>? _additionalConfiguration;
    private bool _initialized;

    public string BaseUrl => _baseUrl;
    public string Domain => _domain;
    public string InstanceName => _instanceName;

    /// <summary>
    /// Creates a new test server instance
    /// </summary>
    /// <param name="baseUrl">Base URL for this server (e.g., "https://server-a.test")</param>
    /// <param name="domain">Domain name (e.g., "server-a.test")</param>
    /// <param name="instanceName">Friendly instance name for debugging</param>
    /// <param name="routingHandler">Optional routing handler for cross-server communication</param>
    /// <param name="additionalConfiguration">Additional configuration values</param>
    public BrocaTestServer(
        string baseUrl, 
        string domain, 
        string instanceName, 
        TestServerRoutingHandler? routingHandler = null,
        Dictionary<string, string?>? additionalConfiguration = null)
    {
        _baseUrl = baseUrl;
        _domain = domain;
        _instanceName = instanceName;
        _routingHandler = routingHandler;
        _additionalConfiguration = additionalConfiguration;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var configValues = new Dictionary<string, string?>
            {
                ["ActivityPub:BaseUrl"] = _baseUrl,
                ["ActivityPub:PrimaryDomain"] = _domain,
                ["ActivityPub:EnableAdminOperations"] = "true"
            };

            // Merge additional configuration if provided
            if (_additionalConfiguration != null)
            {
                foreach (var kvp in _additionalConfiguration)
                {
                    configValues[kvp.Key] = kvp.Value;
                }
            }

            config.AddInMemoryCollection(configValues);
        });

        builder.ConfigureServices(services =>
        {
            // If routing is configured, replace the HttpClientFactory
            if (_routingHandler != null)
            {
                // Remove the existing HttpClientFactory registration
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IHttpClientFactory));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Register our custom factory that uses the routing handler
                services.AddSingleton<IHttpClientFactory>(sp =>
                {
                    return new RoutingHttpClientFactory(_routingHandler);
                });
            }
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureWebHost(webHostBuilder =>
        {
            webHostBuilder.UseUrls(_baseUrl);
        });

        return base.CreateHost(builder);
    }

    /// <summary>
    /// Initializes the server (creates system actor, etc.)
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        using var scope = Services.CreateScope();
        var systemIdentity = scope.ServiceProvider.GetRequiredService<ISystemIdentityService>();
        await systemIdentity.EnsureSystemActorAsync();

        _initialized = true;
    }

    /// <summary>
    /// Clears all data from this server's repositories
    /// </summary>
    public void ClearData()
    {
        using var scope = Services.CreateScope();
        
        var activityRepo = scope.ServiceProvider.GetRequiredService<IActivityRepository>();
        if (activityRepo is InMemoryActivityRepository inMemoryActivityRepo)
        {
            inMemoryActivityRepo.Clear();
        }

        var actorRepo = scope.ServiceProvider.GetRequiredService<IActorRepository>();
        if (actorRepo is InMemoryActorRepository inMemoryActorRepo)
        {
            inMemoryActorRepo.Clear();
        }
    }

    /// <summary>
    /// Gets a repository from this server's service provider
    /// </summary>
    public T GetRepository<T>() where T : class
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
    }
}
