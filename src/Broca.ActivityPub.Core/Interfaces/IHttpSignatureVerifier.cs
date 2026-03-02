namespace Broca.ActivityPub.Core.Interfaces;

public interface IHttpSignatureVerifier
{
    Task<bool> VerifyAsync(
        IDictionary<string, string> headers,
        string publicKeyPem,
        CancellationToken cancellationToken = default);

    bool VerifyDigest(byte[] bodyBytes, string digestHeader);

    string GetSignatureKeyId(string signatureHeader);
}
