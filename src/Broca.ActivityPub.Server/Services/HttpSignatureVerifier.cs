using Broca.ActivityPub.Client.Services;
using Broca.ActivityPub.Core.Interfaces;

namespace Broca.ActivityPub.Server.Services;

public class HttpSignatureVerifier : IHttpSignatureVerifier
{
    private readonly HttpSignatureService _signatureService;

    public HttpSignatureVerifier(HttpSignatureService signatureService)
    {
        _signatureService = signatureService;
    }

    public Task<bool> VerifyAsync(
        IDictionary<string, string> headers,
        string publicKeyPem,
        CancellationToken cancellationToken = default)
        => _signatureService.VerifyHttpSignatureAsync(headers, publicKeyPem, cancellationToken);

    public bool VerifyDigest(byte[] bodyBytes, string digestHeader)
    {
        if (!digestHeader.StartsWith("SHA-256=", StringComparison.OrdinalIgnoreCase))
            return true; // unknown algorithm — skip rather than reject

        var expected = _signatureService.ComputeContentDigestHash(bodyBytes);
        var provided = digestHeader.Substring(8);
        return provided == expected;
    }

    public string GetSignatureKeyId(string signatureHeader)
        => _signatureService.GetSignatureKeyId(signatureHeader);
}
