namespace Broca.ActivityPub.IntegrationTests.Infrastructure;

/// <summary>
/// HttpClientFactory that creates clients using the test server routing handler
/// </summary>
public class RoutingHttpClientFactory : IHttpClientFactory
{
    private readonly TestServerRoutingHandler _routingHandler;

    public RoutingHttpClientFactory(TestServerRoutingHandler routingHandler)
    {
        _routingHandler = routingHandler;
    }

    public HttpClient CreateClient(string name)
    {
        return new HttpClient(_routingHandler, disposeHandler: false);
    }
}
