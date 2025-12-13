using System.Security.Cryptography;

namespace Broca.ActivityPub.IntegrationTests.Infrastructure;

/// <summary>
/// Utility for generating RSA key pairs for testing
/// </summary>
public static class KeyGenerator
{
    /// <summary>
    /// Generates a new RSA key pair for testing
    /// </summary>
    /// <returns>A tuple containing the private and public key PEMs</returns>
    public static (string privateKeyPem, string publicKeyPem) GenerateKeyPair()
    {
        using var rsa = RSA.Create(2048);
        
        var privateKeyPem = $"-----BEGIN PRIVATE KEY-----\n{Convert.ToBase64String(rsa.ExportPkcs8PrivateKey(), Base64FormattingOptions.InsertLineBreaks)}\n-----END PRIVATE KEY-----";
        var publicKeyPem = $"-----BEGIN PUBLIC KEY-----\n{Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo(), Base64FormattingOptions.InsertLineBreaks)}\n-----END PUBLIC KEY-----";
        
        return (privateKeyPem, publicKeyPem);
    }

    /// <summary>
    /// Extracts the public key from a private key PEM
    /// </summary>
    public static string ExtractPublicKey(string privateKeyPem)
    {
        using var rsa = RSA.Create();
        
        // Remove PEM headers and decode
        var privateKeyBase64 = privateKeyPem
            .Replace("-----BEGIN PRIVATE KEY-----", "")
            .Replace("-----END PRIVATE KEY-----", "")
            .Replace("\n", "")
            .Replace("\r", "");
        
        var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);
        rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
        
        var publicKeyPem = $"-----BEGIN PUBLIC KEY-----\n{Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo(), Base64FormattingOptions.InsertLineBreaks)}\n-----END PUBLIC KEY-----";
        
        return publicKeyPem;
    }
}
