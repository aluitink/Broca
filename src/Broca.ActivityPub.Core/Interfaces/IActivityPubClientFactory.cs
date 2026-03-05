namespace Broca.ActivityPub.Core.Interfaces;

public interface IActivityPubClientFactory
{
    IActivityPubClient CreateAnonymous();
    IActivityPubClient CreateForActor(string actorId, string publicKeyId, string privateKeyPem);
}
