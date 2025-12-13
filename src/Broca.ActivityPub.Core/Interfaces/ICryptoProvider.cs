namespace Broca.ActivityPub.Core.Interfaces;

/// <summary>
/// Provides cryptographic operations for ActivityPub signatures
/// </summary>
public interface ICryptoProvider
{
    /// <summary>
    /// Signs data using RSA-SHA256 with the provided private key
    /// </summary>
    /// <param name="privateKeyPem">PEM-encoded RSA private key</param>
    /// <param name="bytesToSign">Data to sign</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Signature bytes</returns>
    Task<byte[]> RsaSignDataAsync(string privateKeyPem, byte[] bytesToSign, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies an RSA-SHA256 signature using the provided public key
    /// </summary>
    /// <param name="publicKeyPem">PEM-encoded RSA public key</param>
    /// <param name="signatureBytes">Signature to verify</param>
    /// <param name="signedDataBytes">Original data that was signed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if signature is valid, false otherwise</returns>
    Task<bool> RsaVerifyDataAsync(string publicKeyPem, byte[] signatureBytes, byte[] signedDataBytes, CancellationToken cancellationToken = default);
}
