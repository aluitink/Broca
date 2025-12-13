using Broca.ActivityPub.Client.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Broca.ActivityPub.IntegrationTests.Infrastructure;

/// <summary>
/// Test fixture base class for multi-server federation tests
/// Manages multiple server instances with proper initialization and cleanup
/// </summary>
public abstract class MultiServerTestFixture : IAsyncLifetime
{
    protected Dictionary<string, BrocaTestServer> Servers { get; } = new();
    protected Dictionary<string, HttpClient> HttpClients { get; } = new();
    private Dictionary<string, HttpMessageHandler> _serverHandlers = new();
    private TestServerRoutingHandler? _routingHandler;

    public async Task InitializeAsync()
    {
        // Create the routing handler first (empty initially)
        _routingHandler = new TestServerRoutingHandler(_serverHandlers);
        
        // Set up servers (which will use the routing handler)
        await SetupServersAsync();
        
        // Now populate the routing handler with actual server handlers
        foreach (var (instanceName, server) in Servers)
        {
            _serverHandlers[server.Domain] = server.Server.CreateHandler();
        }
    }

    public async Task DisposeAsync()
    {
        foreach (var client in HttpClients.Values)
        {
            client?.Dispose();
        }
        HttpClients.Clear();

        foreach (var server in Servers.Values)
        {
            await server.DisposeAsync();
        }
        Servers.Clear();
    }

    /// <summary>
    /// Override this method to set up your test servers
    /// </summary>
    protected abstract Task SetupServersAsync();

    /// <summary>
    /// Creates and registers a new test server
    /// </summary>
    protected async Task<BrocaTestServer> CreateServerAsync(string instanceName, string baseUrl, string domain)
    {
        var server = new BrocaTestServer(baseUrl, domain, instanceName, _routingHandler!);
        await server.InitializeAsync();
        
        Servers[instanceName] = server;
        HttpClients[instanceName] = server.CreateClient();
        
        return server;
    }

    /// <summary>
    /// Gets a server by instance name
    /// </summary>
    protected BrocaTestServer GetServer(string instanceName)
    {
        return Servers[instanceName];
    }

    /// <summary>
    /// Gets an HttpClient for a server by instance name
    /// </summary>
    protected HttpClient GetClient(string instanceName)
    {
        return HttpClients[instanceName];
    }

    /// <summary>
    /// Clears all data from all servers
    /// </summary>
    protected void ClearAllData()
    {
        foreach (var server in Servers.Values)
        {
            server.ClearData();
        }
    }
}

/// <summary>
/// Simple two-server test fixture for common federation scenarios
/// </summary>
public class TwoServerFixture : MultiServerTestFixture
{
    protected BrocaTestServer ServerA => GetServer("ServerA");
    protected BrocaTestServer ServerB => GetServer("ServerB");
    protected HttpClient ClientA => GetClient("ServerA");
    protected HttpClient ClientB => GetClient("ServerB");

    protected override async Task SetupServersAsync()
    {
        await CreateServerAsync("ServerA", "https://server-a.test", "server-a.test");
        await CreateServerAsync("ServerB", "https://server-b.test", "server-b.test");
    }
}
