using System.Net;

namespace Broca.ActivityPub.IntegrationTests.Infrastructure;

/// <summary>
/// HTTP message handler that routes requests to the appropriate test server
/// based on the request URI's host
/// </summary>
public class TestServerRoutingHandler : DelegatingHandler
{
    private readonly Dictionary<string, HttpMessageHandler> _serverHandlers;

    public TestServerRoutingHandler(Dictionary<string, HttpMessageHandler> serverHandlers)
    {
        _serverHandlers = serverHandlers;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri == null)
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Request URI is null")
            };
        }

        var host = request.RequestUri.Host;

        // Find the handler for this host
        if (_serverHandlers.TryGetValue(host, out var handler))
        {
            // Create an invoker to call the target server's handler
            var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
            return await invoker.SendAsync(request, cancellationToken);
        }

        // If no handler found, return 404
        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"No test server found for host: {host}")
        };
    }
}
