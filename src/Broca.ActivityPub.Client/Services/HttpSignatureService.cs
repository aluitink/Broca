using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Broca.ActivityPub.Core.Interfaces;

namespace Broca.ActivityPub.Client.Services;

/// <summary>
/// Service for creating and verifying HTTP signatures for ActivityPub requests
/// </summary>
/// <remarks>
/// Implements HTTP Signatures as required by Mastodon and other ActivityPub servers.
/// Mastodon requirements:
/// - Date header or (created) pseudo-header must be signed
/// - Digest header or (request-target) pseudo-header must be signed
/// - Host header must be signed for GET requests
/// - Digest header must be signed for POST requests
/// </remarks>
public class HttpSignatureService
{
    private readonly ICryptoProvider _cryptoProvider;

    public HttpSignatureService(ICryptoProvider cryptoProvider)
    {
        _cryptoProvider = cryptoProvider ?? throw new ArgumentNullException(nameof(cryptoProvider));
    }

    /// <summary>
    /// Applies HTTP signature headers to an outgoing request
    /// </summary>
    /// <param name="requestMethod">HTTP method (GET, POST, etc.)</param>
    /// <param name="requestUri">Target URI</param>
    /// <param name="addHeaderAction">Action to add headers to the request</param>
    /// <param name="senderPublicKeyId">URL of the sender's public key</param>
    /// <param name="senderPrivateKeyPem">PEM-encoded private key</param>
    /// <param name="accept">Accept header value</param>
    /// <param name="contentType">Content-Type header value</param>
    /// <param name="getContentFunc">Function to get request body bytes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task ApplyHttpSignatureAsync(
        string requestMethod,
        Uri requestUri,
        Action<string, string> addHeaderAction,
        string senderPublicKeyId,
        string senderPrivateKeyPem,
        string? accept = null,
        string? contentType = null,
        Func<CancellationToken, Task<byte[]>>? getContentFunc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(addHeaderAction);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestMethod);
        ArgumentNullException.ThrowIfNull(requestUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderPublicKeyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderPrivateKeyPem);

        var headers = new Dictionary<string, string>();

        // Add (request-target) pseudo-header
        string requestTarget = $"{requestMethod.ToLower()} {requestUri.AbsolutePath}";
        headers.Add("(request-target)", requestTarget);

        // Add host header (include port if non-default)
        var portString = requestUri.IsDefaultPort ? string.Empty : $":{requestUri.Port}";
        string requestHost = $"{requestUri.Host}{portString}";
        addHeaderAction("Host", requestHost);
        headers.Add("host", requestHost);

        // Add date header (RFC1123 format)
        string date = DateTimeOffset.UtcNow.ToString("r", CultureInfo.InvariantCulture);

        // Check if running in browser context (Blazor WebAssembly)
        // Fetch API cannot set Date header, so use (created) pseudo-header instead
        if (Environment.OSVersion.Platform == PlatformID.Other)
        {
            addHeaderAction("Created", date);
            headers.Add("(created)", date);
        }
        else
        {
            addHeaderAction("Date", date);
            headers.Add("date", date);
        }

        // Add Content-Type header if specified
        if (contentType != null)
        {
            addHeaderAction("Content-Type", contentType);
            headers.Add("content-type", contentType);
        }

        // Add Accept header if specified
        if (accept != null)
        {
            addHeaderAction("Accept", accept);
            headers.Add("accept", accept);
        }

        // Add Digest header for POST/PUT requests with body
        if (getContentFunc != null)
        {
            var serializedBody = await getContentFunc(cancellationToken);
            if (serializedBody != null)
            {
                var contentHash = $"SHA-256={ComputeContentDigestHash(serializedBody)}";
                headers.Add("digest", contentHash);
                addHeaderAction("Digest", contentHash);
            }
        }

        // Create signature string
        var headersList = string.Join(' ', headers.Keys);
        var stringToSign = string.Join('\n', headers.Select(kv => $"{kv.Key}: {kv.Value}"));

        // Sign the string
        var signature = await _cryptoProvider.RsaSignDataAsync(senderPrivateKeyPem, Encoding.UTF8.GetBytes(stringToSign), cancellationToken);
        var signatureBase64 = Convert.ToBase64String(signature);

        // Create Signature header
        string signatureHeader = $"keyId=\"{senderPublicKeyId}\",algorithm=\"rsa-sha256\",headers=\"{headersList}\",signature=\"{signatureBase64}\"";
        addHeaderAction("Signature", signatureHeader);
    }

    /// <summary>
    /// Verifies an HTTP signature on an incoming request
    /// </summary>
    /// <param name="httpHeaders">Request headers</param>
    /// <param name="purportedOwnerPublicKeyPem">Public key to verify against</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if signature is valid</returns>
    public async Task<bool> VerifyHttpSignatureAsync(
        IDictionary<string, string> httpHeaders,
        string purportedOwnerPublicKeyPem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpHeaders);
        ArgumentException.ThrowIfNullOrWhiteSpace(purportedOwnerPublicKeyPem);

        if (!httpHeaders.TryGetValue("signature", out var signatureHeader))
        {
            throw new InvalidOperationException("Signature header is missing");
        }

        var signatureParts = ParseSignatureParts(signatureHeader);

        if (!signatureParts.ContainsKey("headers"))
        {
            throw new InvalidOperationException("Unable to locate headers list in signature");
        }

        // Reconstruct the signed data
        var dataToSignBuilder = new StringBuilder();

        foreach (var header in signatureParts["headers"].Split(" "))
        {
            string? headerValue = null;

            if (header == "(request-target)")
            {
                headerValue = httpHeaders["(request-target)"];
            }
            else if (header == "(created)")
            {
                headerValue = httpHeaders["created"];
            }
            else if (header == "host")
            {
                // Handle Azure Functions routing
                if (httpHeaders.ContainsKey("x-ms-original-url"))
                {
                    headerValue = new Uri(httpHeaders["x-ms-original-url"]).Host;
                }
                else if (httpHeaders.ContainsKey("host"))
                {
                    headerValue = httpHeaders["host"];
                }
            }
            else
            {
                var firstHeader = httpHeaders.FirstOrDefault(h => h.Key.Equals(header, StringComparison.OrdinalIgnoreCase));
                headerValue = firstHeader.Value;
            }

            if (headerValue == null)
            {
                throw new InvalidOperationException($"Signature indicated header '{header}' as part of the signature but was not found on the request.");
            }

            dataToSignBuilder.Append($"{header}: {headerValue}\n");
        }

        var signatureHashBase64 = signatureParts["signature"];
        var algorithm = signatureParts["algorithm"].ToLowerInvariant();

        var dataToVerify = dataToSignBuilder.ToString().TrimEnd('\n');
        var signatureBytes = Convert.FromBase64String(signatureHashBase64);
        var bytesToVerify = Encoding.UTF8.GetBytes(dataToVerify);

        // Verify based on algorithm
        return algorithm switch
        {
            "hs2019" or "rsa-sha256" => await _cryptoProvider.RsaVerifyDataAsync(purportedOwnerPublicKeyPem, signatureBytes, bytesToVerify, cancellationToken),
            _ => throw new NotImplementedException($"Validation of signing algorithm '{algorithm}' has not been implemented.")
        };
    }

    /// <summary>
    /// Extracts the keyId from a Signature header
    /// </summary>
    /// <param name="signature">Signature header value</param>
    /// <returns>The keyId value</returns>
    public string GetSignatureKeyId(string signature)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signature);

        var signatureParts = ParseSignatureParts(signature);

        if (signatureParts == null || !signatureParts.ContainsKey("keyId"))
        {
            throw new InvalidOperationException("Could not find signature field 'keyId'");
        }

        return signatureParts["keyId"];
    }

    /// <summary>
    /// Extracts the keyId from HTTP request headers
    /// </summary>
    /// <param name="headers">Request headers dictionary</param>
    /// <returns>The keyId value</returns>
    public string GetSignatureKeyIdFromHeaders(IDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        if (!headers.TryGetValue("Signature", out var signatureHeader) && !headers.TryGetValue("signature", out signatureHeader))
        {
            throw new InvalidOperationException("No Signature header found");
        }

        var signatureParts = ParseSignatureParts(signatureHeader);

        if (signatureParts == null || !signatureParts.ContainsKey("keyId"))
        {
            throw new InvalidOperationException("Could not find signature field 'keyId'");
        }

        return signatureParts["keyId"];
    }

    /// <summary>
    /// Computes SHA-256 digest hash of content
    /// </summary>
    /// <param name="bytes">Content bytes</param>
    /// <returns>Base64-encoded hash</returns>
    public string ComputeContentDigestHash(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        using var sha256 = SHA256.Create();
        byte[] hashedBytes = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hashedBytes);
    }

    /// <summary>
    /// Verifies an object signature (for signed ActivityPub objects)
    /// </summary>
    /// <param name="activityObject">The activity object with a signature property</param>
    /// <param name="fetchPublicKeyPemAsync">Function to fetch the public key PEM by key ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if signature is valid</returns>
    public async Task<bool> VerifyObjectSignatureAsync(
        IDictionary<string, object> activityObject,
        Func<string, Task<string>> fetchPublicKeyPemAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activityObject);
        ArgumentNullException.ThrowIfNull(fetchPublicKeyPemAsync);

        if (!activityObject.TryGetValue("signature", out var sigObj) || sigObj == null)
        {
            throw new InvalidOperationException("No signature property found in object");
        }

        IDictionary<string, object>? signatureDict = sigObj as IDictionary<string, object>;

        // Handle JsonElement case
        if (signatureDict == null && sigObj is JsonElement jeSig && jeSig.ValueKind == JsonValueKind.Object)
        {
            signatureDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jeSig.GetRawText());
        }

        if (signatureDict == null)
        {
            throw new InvalidOperationException("Signature property is not a dictionary");
        }

        if (!signatureDict.TryGetValue("creator", out var creatorObj) || creatorObj == null)
        {
            throw new InvalidOperationException("No creator in signature");
        }

        if (!signatureDict.TryGetValue("signatureValue", out var signatureValueObj) || signatureValueObj == null)
        {
            throw new InvalidOperationException("No signatureValue in signature");
        }

        var creator = GetStringValue(creatorObj);
        var signatureValue = GetStringValue(signatureValueObj);

        // Canonicalize the object (excluding the signature property)
        var canonicalized = CanonicalizeObjectForSignature(activityObject);
        var data = Encoding.UTF8.GetBytes(canonicalized);

        // Fetch the public key PEM
        var publicKeyPem = await fetchPublicKeyPemAsync(creator);

        if (string.IsNullOrWhiteSpace(publicKeyPem))
        {
            throw new InvalidOperationException($"Public key PEM not found for creator: {creator}");
        }

        if (string.IsNullOrWhiteSpace(signatureValue))
        {
            throw new InvalidOperationException("Signature value is null or empty");
        }

        // Verify signature
        var signatureBytes = Convert.FromBase64String(signatureValue);
        return await _cryptoProvider.RsaVerifyDataAsync(publicKeyPem, signatureBytes, data, cancellationToken);
    }

    /// <summary>
    /// Parses a Signature header into its component parts
    /// </summary>
    public Dictionary<string, string> ParseSignatureParts(string signatureHeader)
    {
        var signatureParts = signatureHeader.Split(',');
        var dictionary = new Dictionary<string, string>();

        foreach (var sigPart in signatureParts)
        {
            var parts = sigPart.Split('=', 2);

            if (parts.Length < 2)
            {
                throw new InvalidOperationException($"Invalid Signature: {signatureHeader}");
            }

            var sigPartKey = parts[0].Trim();
            var sigPartValue = parts[1].Trim('"');
            dictionary.Add(sigPartKey, sigPartValue);
        }

        return dictionary;
    }

    /// <summary>
    /// Canonicalizes an object for signature verification
    /// </summary>
    private static string CanonicalizeObjectForSignature(object obj)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Remove 'signature' property if present
        if (obj is IDictionary<string, object> dict)
        {
            var filteredDict = dict.Where(kv => kv.Key != "signature").ToDictionary(kv => kv.Key, kv => kv.Value);
            return JsonSerializer.Serialize(filteredDict, options);
        }

        return JsonSerializer.Serialize(obj, options);
    }

    /// <summary>
    /// Extracts string value from various object types (handles JsonElement)
    /// </summary>
    private static string GetStringValue(object value)
    {
        if (value is JsonElement je && je.ValueKind == JsonValueKind.String)
        {
            return je.GetString() ?? string.Empty;
        }

        return value?.ToString() ?? string.Empty;
    }
}
