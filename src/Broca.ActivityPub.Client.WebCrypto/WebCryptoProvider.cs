using Glihm.JSInterop.Browser.WebCryptoAPI.Cryptography;
using Glihm.JSInterop.Browser.WebCryptoAPI.Cryptography.RSA;
using Glihm.JSInterop.Browser.WebCryptoAPI.Interfaces;
using Glihm.JSInterop.Browser.WebCryptoAPI.Interfaces.CryptoKeys;
using Glihm.JSInterop.Browser.WebCryptoAPI.Interfaces.Subtle.RSA;
using Glihm.JSInterop.Browser.WebCryptoAPI.Interfaces.Subtle.SHA;
using Glihm.JSInterop.Browser.WebCryptoAPI.JSHelpers;
using Broca.ActivityPub.Core.Interfaces;

namespace Broca.ActivityPub.Client.WebCrypto;

/// <summary>
/// Web Crypto API-based implementation of ICryptoProvider for Blazor WebAssembly
/// </summary>
/// <remarks>
/// This provider uses the browser's native Web Crypto API for RSA signing and verification,
/// which is necessary in WASM environments where .NET's RSACryptoServiceProvider is not supported.
/// </remarks>
public class WebCryptoProvider : ICryptoProvider
{
    private readonly Crypto _crypto;

    public WebCryptoProvider(Crypto crypto)
    {
        _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
    }

    /// <inheritdoc/>
    public async Task<byte[]> RsaSignDataAsync(string privateKeyPem, byte[] bytesToSign, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPem);
        ArgumentNullException.ThrowIfNull(bytesToSign);

        byte[]? key = PemHelper.KeyExtract(privateKeyPem);
        if (key is null)
        {
            throw new InvalidOperationException("Failed to extract key from PEM");
        }

        if (!await _crypto.IsWebCryptoAPISupported())
        {
            throw new NotSupportedException("Web Crypto API is not supported in this browser");
        }

        JSResultValue<CryptoKeyDescriptor> res = await _crypto.Subtle.ImportKey(
            CryptoKeyFormat.PKCS8,
            key,
            new RsaHashedImportParams(RsaAlgorithm.SSA_PKCS1_v1_5, ShaAlgorithm.SHA256),
            false,
            CryptoKeyUsage.Sign);

        if (!res)
        {
            res.GetValueOrThrow();
        }

        var keyDescriptor = res.GetValueOrThrow();

        var result = await _crypto.Subtle.Sign(new RsaSsaPkcs1Params(), keyDescriptor, bytesToSign);
        return result.GetValueOrThrow();
    }

    /// <inheritdoc/>
    public async Task<bool> RsaVerifyDataAsync(string publicKeyPem, byte[] signatureBytes, byte[] signedDataBytes, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPem);
        ArgumentNullException.ThrowIfNull(signatureBytes);
        ArgumentNullException.ThrowIfNull(signedDataBytes);

        byte[]? key = PemHelper.KeyExtract(publicKeyPem);
        if (key is null)
        {
            return false;
        }

        if (!await _crypto.IsWebCryptoAPISupported())
        {
            return false;
        }

        var res = await _crypto.Subtle.ImportKey(
            CryptoKeyFormat.PKCS8,
            key,
            new RsaHashedImportParams(RsaAlgorithm.SSA_PKCS1_v1_5, ShaAlgorithm.SHA256),
            true,
            CryptoKeyUsage.Verify);

        if (!res)
        {
            return false;
        }

        var keyDescriptor = res.GetValueOrThrow();

        var result = await _crypto.Subtle.Verify(new RsaSsaPkcs1Params(), keyDescriptor, signatureBytes, signedDataBytes);
        return result.GetValueOrThrow();
    }
}
