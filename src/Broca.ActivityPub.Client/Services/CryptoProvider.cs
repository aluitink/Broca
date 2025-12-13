using System.Security.Cryptography;
using Broca.ActivityPub.Core.Interfaces;

namespace Broca.ActivityPub.Client.Services;

/// <summary>
/// Provides RSA cryptographic operations for ActivityPub HTTP signatures
/// </summary>
public class CryptoProvider : ICryptoProvider
{
    /// <inheritdoc/>
    public Task<byte[]> RsaSignDataAsync(string privateKeyPem, byte[] bytesToSign, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPem);
        ArgumentNullException.ThrowIfNull(bytesToSign);

        using var rsa = new RSACryptoServiceProvider();
        try
        {
            rsa.ImportFromPem(privateKeyPem);
            byte[] signature = rsa.SignData(bytesToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Task.FromResult(signature);
        }
        finally
        {
            rsa.PersistKeyInCsp = false;
        }
    }

    /// <inheritdoc/>
    public Task<bool> RsaVerifyDataAsync(string publicKeyPem, byte[] signatureBytes, byte[] signedDataBytes, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPem);
        ArgumentNullException.ThrowIfNull(signatureBytes);
        ArgumentNullException.ThrowIfNull(signedDataBytes);

        using var rsa = new RSACryptoServiceProvider();
        try
        {
            rsa.ImportFromPem(publicKeyPem);
            var hashAlgorithm = CryptoConfig.MapNameToOID("SHA256");
            if (hashAlgorithm == null)
            {
                throw new InvalidOperationException("Unable to map SHA256 to OID");
            }
            bool isValid = rsa.VerifyData(signedDataBytes, hashAlgorithm, signatureBytes);
            return Task.FromResult(isValid);
        }
        finally
        {
            rsa.PersistKeyInCsp = false;
        }
    }
}
