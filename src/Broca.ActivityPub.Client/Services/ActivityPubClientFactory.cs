using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Client.Services;

public class ActivityPubClientFactory : IActivityPubClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebFingerService _webFingerService;
    private readonly HttpSignatureService _signatureService;
    private readonly ILogger<ActivityPubClient> _clientLogger;

    public ActivityPubClientFactory(
        IHttpClientFactory httpClientFactory,
        IWebFingerService webFingerService,
        HttpSignatureService signatureService,
        ILogger<ActivityPubClient> clientLogger)
    {
        _httpClientFactory = httpClientFactory;
        _webFingerService = webFingerService;
        _signatureService = signatureService;
        _clientLogger = clientLogger;
    }

    public IActivityPubClient CreateAnonymous()
        => new ActivityPubClient(
            _httpClientFactory,
            _webFingerService,
            _signatureService,
            Options.Create(new ActivityPubClientOptions()),
            _clientLogger);

    public IActivityPubClient CreateForActor(string actorId, string publicKeyId, string privateKeyPem)
        => new ActivityPubClient(
            _httpClientFactory,
            _webFingerService,
            _signatureService,
            Options.Create(new ActivityPubClientOptions
            {
                ActorId = actorId,
                PublicKeyId = publicKeyId,
                PrivateKeyPem = privateKeyPem
            }),
            _clientLogger);
}
