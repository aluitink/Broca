using Broca.ActivityPub.Client.Services;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Broca.ActivityPub.UnitTests;

public class ResilientActivityPubClientTests
{
    private readonly Mock<IActivityPubClient> _innerClient = new();
    private readonly Mock<ProxyService> _proxyService;
    private readonly ResilientActivityPubClient _sut;

    public ResilientActivityPubClientTests()
    {
        _proxyService = new Mock<ProxyService>(
            new HttpClient(), NullLogger<ProxyService>.Instance);

        _sut = new ResilientActivityPubClient(
            _innerClient.Object,
            _proxyService.Object,
            NullLogger<ResilientActivityPubClient>.Instance);
    }

    [Fact]
    public async Task GetActorAsync_WhenDirectSucceeds_ReturnsDirectResult()
    {
        var uri = new Uri("https://mastodon.social/users/gargron");
        var actor = new Person { Id = uri.ToString() };
        _innerClient.Setup(c => c.GetActorAsync(uri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);

        var result = await _sut.GetActorAsync(uri);

        Assert.Same(actor, result);
        _proxyService.Verify(
            p => p.GetViaProxyAsync<Actor>(It.IsAny<Uri>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetActorAsync_WhenDirectThrows_FallsBackToProxy()
    {
        var uri = new Uri("https://www.threads.net/ap/users/mosseri/");
        var actor = new Person { Id = uri.ToString() };

        _innerClient.Setup(c => c.GetActorAsync(uri, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Failed to fetch actor"));
        _proxyService.Setup(p => p.GetViaProxyAsync<Actor>(uri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);

        var result = await _sut.GetActorAsync(uri);

        Assert.Same(actor, result);
        _proxyService.Verify(
            p => p.GetViaProxyAsync<Actor>(uri, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetActorAsync_WhenDirectAndProxyBothFail_Throws()
    {
        var uri = new Uri("https://example.com/users/nonexistent");

        _innerClient.Setup(c => c.GetActorAsync(uri, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Failed to fetch actor"));
        _proxyService.Setup(p => p.GetViaProxyAsync<Actor>(uri, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Actor?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.GetActorAsync(uri));
    }

    [Fact]
    public async Task GetAsync_WhenDirectReturnsNull_FallsBackToProxy()
    {
        var uri = new Uri("https://www.threads.net/ap/users/mosseri/");
        var actor = new Person { Id = uri.ToString() };

        _innerClient.Setup(c => c.GetAsync<Actor>(uri, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Actor?)null);
        _proxyService.Setup(p => p.GetViaProxyAsync<Actor>(uri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);

        var result = await _sut.GetAsync<Actor>(uri);

        Assert.Same(actor, result);
    }

    [Fact]
    public async Task GetAsync_WhenDirectSucceeds_DoesNotProxy()
    {
        var uri = new Uri("https://mastodon.social/users/gargron");
        var actor = new Person { Id = uri.ToString() };

        _innerClient.Setup(c => c.GetAsync<Actor>(uri, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);

        var result = await _sut.GetAsync<Actor>(uri);

        Assert.Same(actor, result);
        _proxyService.Verify(
            p => p.GetViaProxyAsync<Actor>(It.IsAny<Uri>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAsync_WhenDirectThrowsHttpException_FallsBackToProxy()
    {
        var uri = new Uri("https://example.com/some/resource");
        var note = new Note { Id = uri.ToString() };

        _innerClient.Setup(c => c.GetAsync<Note>(uri, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        _proxyService.Setup(p => p.GetViaProxyAsync<Note>(uri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

        var result = await _sut.GetAsync<Note>(uri);

        Assert.Same(note, result);
    }

    [Fact]
    public async Task GetActorByAliasAsync_WhenDirectFails_FallsBackToProxy()
    {
        var alias = "mosseri@threads.net";
        var actorUri = new Uri("https://www.threads.net/ap/users/mosseri/");
        var actor = new Person { Id = actorUri.ToString() };

        _innerClient.Setup(c => c.GetActorByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Failed to fetch actor"));

        // The proxy-based fallback does WebFinger via proxy, then GetActorAsync.
        // We mock GetViaProxyAsync for the WebFinger call and GetActorAsync for the actor fetch.
        var webFingerResource = new WebFingerResource("acct:mosseri@threads.net")
        {
            Links = new List<ResourceLink>
            {
                new ResourceLink
                {
                    Rel = "self",
                    Href = actorUri.ToString(),
                    Type = "application/activity+json"
                }
            }
        };

        _proxyService.Setup(p => p.GetViaProxyAsync<WebFingerResource>(
                It.Is<Uri>(u => u.ToString().Contains("webfinger")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(webFingerResource);

        // The resolved actor URI will go through GetActorAsync which will try inner then proxy
        _innerClient.Setup(c => c.GetActorAsync(actorUri, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Failed to fetch actor"));
        _proxyService.Setup(p => p.GetViaProxyAsync<Actor>(actorUri, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);

        var result = await _sut.GetActorByAliasAsync(alias);

        Assert.Equal(actorUri.ToString(), result.Id);
    }
}
