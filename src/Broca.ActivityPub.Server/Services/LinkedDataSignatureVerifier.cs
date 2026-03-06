using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Broca.ActivityPub.Server.Services;

public class LinkedDataSignatureVerifier
{
    private const string IdentityContext = "https://w3id.org/identity/v1";

    private readonly ILogger<LinkedDataSignatureVerifier> _logger;

    public LinkedDataSignatureVerifier(ILogger<LinkedDataSignatureVerifier> logger)
    {
        _logger = logger;
    }

    public bool TryGetSignatureCreator(string body, out string? creatorUri)
    {
        creatorUri = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("signature", out var sig))
                return false;
            if (!sig.TryGetProperty("creator", out var creator))
                return false;
            creatorUri = creator.GetString();
            return !string.IsNullOrEmpty(creatorUri);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract LD signature creator from body");
            return false;
        }
    }

    /// <summary>
    /// Verifies an RsaSignature2017 Linked Data Signature against the supplied public key PEM.
    /// Uses approximate canonicalization (property-sorted JSON serialization) as a best-effort
    /// substitute for full URDNA2015, which is what Mastodon generates. Returns false when
    /// the signature is absent, malformed, or does not verify.
    /// </summary>
    public bool Verify(string body, string publicKeyPem)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("signature", out var sigElem))
            {
                _logger.LogDebug("No 'signature' property found in document");
                return false;
            }

            if (!sigElem.TryGetProperty("type", out var typeElem) || typeElem.GetString() != "RsaSignature2017")
            {
                _logger.LogDebug("LD signature type is not RsaSignature2017");
                return false;
            }

            if (!sigElem.TryGetProperty("signatureValue", out var sigValueElem))
            {
                _logger.LogDebug("LD signature missing signatureValue");
                return false;
            }

            var signatureValueB64 = sigValueElem.GetString();
            if (string.IsNullOrEmpty(signatureValueB64))
                return false;

            var signatureBytes = Convert.FromBase64String(signatureValueB64);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);

            // Attempt 1: approximate canonicalization — sort all object properties before hashing.
            // This does not match Mastodon's URDNA2015 RDF canonicalization exactly, but covers
            // implementations that sign sorted-property JSON rather than RDF N-Quads.
            var approximateOptionsHash = ComputeOptionsHash(sigElem, sortProperties: true);
            var approximateDocumentHash = ComputeDocumentHash(root, sortProperties: true);
            var approximateInput = Encoding.UTF8.GetBytes(approximateOptionsHash + approximateDocumentHash);

            if (rsa.VerifyData(approximateInput, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            {
                _logger.LogDebug("LD signature verified via approximate (sorted) canonicalization");
                return true;
            }

            // Attempt 2: preserve original property order — handles signers that do not sort.
            var preservedOptionsHash = ComputeOptionsHash(sigElem, sortProperties: false);
            var preservedDocumentHash = ComputeDocumentHash(root, sortProperties: false);
            var preservedInput = Encoding.UTF8.GetBytes(preservedOptionsHash + preservedDocumentHash);

            if (rsa.VerifyData(preservedInput, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            {
                _logger.LogDebug("LD signature verified via preserved-order serialization");
                return true;
            }

            _logger.LogDebug("LD signature did not verify with any canonicalization strategy");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "LD signature verification threw an exception");
            return false;
        }
    }

    // Serializes the signature block minus type/id/signatureValue, plus @context,
    // then returns its SHA-256 hex digest — matching Mastodon's options_hash step.
    private string ComputeOptionsHash(JsonElement sigElem, bool sortProperties)
    {
        var node = new JsonObject();
        node["@context"] = IdentityContext;

        foreach (var prop in sigElem.EnumerateObject())
        {
            if (prop.Name is "type" or "id" or "signatureValue")
                continue;
            node[prop.Name] = JsonNode.Parse(prop.Value.GetRawText());
        }

        return Sha256Hex(SerializeNode(node, sortProperties));
    }

    // Serializes the full document minus the "signature" property,
    // then returns its SHA-256 hex digest — matching Mastodon's document_hash step.
    private string ComputeDocumentHash(JsonElement root, bool sortProperties)
    {
        var node = new JsonObject();
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name == "signature")
                continue;
            node[prop.Name] = JsonNode.Parse(prop.Value.GetRawText());
        }

        return Sha256Hex(SerializeNode(node, sortProperties));
    }

    private static byte[] SerializeNode(JsonNode node, bool sortProperties)
    {
        if (!sortProperties)
            return Encoding.UTF8.GetBytes(node.ToJsonString());

        return Encoding.UTF8.GetBytes(SortedJsonString(node));
    }

    // Recursively re-serializes a JsonNode with all object keys in alphabetical order.
    private static string SortedJsonString(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var key in obj.Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal))
            {
                if (!first) sb.Append(',');
                sb.Append(JsonSerializer.Serialize(key));
                sb.Append(':');
                sb.Append(SortedJsonString(obj[key]));
                first = false;
            }
            sb.Append('}');
            return sb.ToString();
        }

        if (node is JsonArray arr)
        {
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (var item in arr)
            {
                if (!first) sb.Append(',');
                sb.Append(SortedJsonString(item));
                first = false;
            }
            sb.Append(']');
            return sb.ToString();
        }

        return node?.ToJsonString() ?? "null";
    }

    private static string Sha256Hex(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
