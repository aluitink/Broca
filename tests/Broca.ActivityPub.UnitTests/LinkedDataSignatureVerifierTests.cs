using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Broca.ActivityPub.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Broca.ActivityPub.UnitTests;

/// <summary>
/// Tests for LinkedDataSignatureVerifier using real-world Mastodon Delete activities captured
/// from the sys/inbox. Because the actors are deleted their public keys are no longer fetchable,
/// so these tests focus on correct parsing, graceful failure with a wrong key, and a round-trip
/// sign+verify test using a locally-generated key pair.
/// </summary>
public class LinkedDataSignatureVerifierTests
{
    private readonly LinkedDataSignatureVerifier _sut = new(NullLogger<LinkedDataSignatureVerifier>.Instance);

    // ---------------------------------------------------------------------------
    // Real-world Mastodon payloads (actor self-deletes from sys/inbox)
    // ---------------------------------------------------------------------------

    public static IEnumerable<object[]> RealWorldDeleteBodies() => new[]
    {
        new object[]
        {
            """
            {
              "actor": "https://mastodon.social/users/chipbond",
              "object": "https://mastodon.social/users/chipbond",
              "to": "https://www.w3.org/ns/activitystreams#Public",
              "@context": ["https://www.w3.org/ns/activitystreams","https://w3id.org/security/v1"],
              "id": "https://mastodon.social/users/chipbond#delete",
              "type": "Delete",
              "signature": {
                "type": "RsaSignature2017",
                "creator": "https://mastodon.social/users/chipbond#main-key",
                "created": "2026-03-06T08:03:37Z",
                "signatureValue": "rPmVVEaHEuzaBLBxEKqqSrPt4ht9F2lIYyFqIw5rC5x5XmC9gEUCBMmqxlxhYMhWncn78N9a5MGkrKGi6/dx0WMeI4qlVO2hI6LxNS9Wi/j4ZhITN+ETjfHUOUjqmafPhfD+WRGO6lzd5RLodZiXXeagefip+QVtinZWwkxHVfezrCHzRjm0i8mZkLtNCrgHTPfV7f8afSK3ZjTJI0tndsCh13Nm1aoQ/UC7lEbcBScEin7q9N6mB6unXx8VtERmjd2VXWeOwgccYiuwv0qxRWH9jFiEw45hrCXrgIQ7DUr+lNfCw0BG3xosAsk5Nz98crZQE+htZomCQ7ybV4KKMg=="
              }
            }
            """,
            "https://mastodon.social/users/chipbond#main-key"
        },
        new object[]
        {
            """
            {
              "actor": "https://mastodon.social/users/tylersinks",
              "object": "https://mastodon.social/users/tylersinks",
              "to": "https://www.w3.org/ns/activitystreams#Public",
              "@context": ["https://www.w3.org/ns/activitystreams","https://w3id.org/security/v1"],
              "id": "https://mastodon.social/users/tylersinks#delete",
              "type": "Delete",
              "signature": {
                "type": "RsaSignature2017",
                "creator": "https://mastodon.social/users/tylersinks#main-key",
                "created": "2026-03-06T07:39:32Z",
                "signatureValue": "sttlyfGPWjMW7hkUHuxsT/ROge0djmF8x+zQhcsM9w7WxzTFpnmpdj rurVKDAFEb3Z3UNgGtnF9LKewm5dtXxEvLu1bl9ovCphKd3yn1Oc3o1myVGRfwNCXZfLntAX7JuPIS4ua+K5Emsku/Im4O9dHi6YhXXQpkGDX95bY3+t5EkwcrlqjGTyDvxCp/3dOp98rVzKlPGzG0de2xkYU9COWngb3892V0fT0T3q3+ytlM7h6NQuOEK2iIU9Btab22vAcrVID3n4tQDndAPJWLowB/z8VFYDn+FalkAwNu0sTaRTlCeKL28yrRZOykmZNN0BBV1BYSrM+N/YWhVaSzHw=="
              }
            }
            """,
            "https://mastodon.social/users/tylersinks#main-key"
        },
        new object[]
        {
            """
            {
              "actor": "https://mastodon.social/users/Goliat",
              "object": "https://mastodon.social/users/Goliat",
              "to": "https://www.w3.org/ns/activitystreams#Public",
              "@context": ["https://www.w3.org/ns/activitystreams","https://w3id.org/security/v1"],
              "id": "https://mastodon.social/users/Goliat#delete",
              "type": "Delete",
              "signature": {
                "type": "RsaSignature2017",
                "creator": "https://mastodon.social/users/Goliat#main-key",
                "created": "2026-03-06T01:15:24Z",
                "signatureValue": "ixufjc/4hpXEkwpOUIhrzT05pDMBO2sKBjPayUGjM89/LxXC+GjjVdSt7jk3AJ7UK4Rq0aEaFNsQ/q3TZySJfvRQfhDYrGdiTs6tjtQ7AVsVHACcvguW9kp8ykVXSIhpIkpBi7Xd2LTlVwjkF4uKE6SmIQ6lP3v8it37mKJsjhOI6mt0G7a3uM9fwe/WnOflpkS0mWJC54BvW3i3ZAxqqP2plkbsMSjr7x3xD/snuX2kruo86WjIZEMnaVMCdpoMInPgMph+7XU6W0l7MTRPCGb9XoDg09KH1x0SYZET2VLVLixA9x+6ut/UIEpS8rGT9a55a8eXY5H3aGnJQUmJtA=="
              }
            }
            """,
            "https://mastodon.social/users/Goliat#main-key"
        },
        new object[]
        {
            """
            {
              "actor": "https://mastodon.social/ap/users/115855514074972447",
              "object": "https://mastodon.social/ap/users/115855514074972447",
              "to": "https://www.w3.org/ns/activitystreams#Public",
              "@context": ["https://www.w3.org/ns/activitystreams","https://w3id.org/security/v1"],
              "id": "https://mastodon.social/ap/users/115855514074972447#delete",
              "type": "Delete",
              "signature": {
                "type": "RsaSignature2017",
                "creator": "https://mastodon.social/ap/users/115855514074972447#main-key",
                "created": "2026-03-06T06:33:19Z",
                "signatureValue": "V1pwNf6IG/ay8tq8zYymG9k8m6aaxKKaJCV1XfRO++GU1+kc1P7yHQm9emWGmJ29Wy/Pjfdb6jlPKeOBnuJCYMpYb8qBo9pWwNQXhxk6Uy+1U9JBNsw0Hwrb2SQorkTGtLPZxmGSYA22c+3NslCP4EwYF6RmQHjA9si3d9tUp14iQQrgzpTxsNQDrjfLmd+Ty3cTm+pk+/ED++9Ay14H/Ql+hd8DtkmwRbwQZfEBtiqFGJZlWuQWjWmcLS7hxbohwR9jrQBhI/fqe4hFIFDudWUna8wVDjdomq5D/w3Xb011HCT0Tyln2INxndf+DDUVV2ALRGCH9WE8+RImUKhlaQ=="
              }
            }
            """,
            "https://mastodon.social/ap/users/115855514074972447#main-key"
        },
    };

    public static IEnumerable<object[]> RealWorldDeleteBodiesOnly()
        => RealWorldDeleteBodies().Select(row => new[] { row[0] });

    // ---------------------------------------------------------------------------
    // TryGetSignatureCreator
    // ---------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(RealWorldDeleteBodies))]
    public void TryGetSignatureCreator_RealWorldDelete_ExtractsCreatorUri(
        string body, string expectedCreator)
    {
        var result = _sut.TryGetSignatureCreator(body, out var creatorUri);

        Assert.True(result);
        Assert.Equal(expectedCreator, creatorUri);
    }

    [Fact]
    public void TryGetSignatureCreator_NoSignatureProperty_ReturnsFalse()
    {
        var body = """{"type":"Delete","actor":"https://example.com/users/alice","object":"https://example.com/users/alice"}""";

        var result = _sut.TryGetSignatureCreator(body, out var creatorUri);

        Assert.False(result);
        Assert.Null(creatorUri);
    }

    [Fact]
    public void TryGetSignatureCreator_InvalidJson_ReturnsFalse()
    {
        var result = _sut.TryGetSignatureCreator("not json {{{", out var creatorUri);

        Assert.False(result);
        Assert.Null(creatorUri);
    }

    // ---------------------------------------------------------------------------
    // Verify — real-world payloads with a wrong key must return false, not throw
    // ---------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(RealWorldDeleteBodiesOnly))]
    public void Verify_RealWorldDelete_WrongKey_ReturnsFalse(string body)
    {
        // We don't have the original private keys (accounts are deleted), so use an
        // unrelated key. The verifier must return false rather than throwing.
        var wrongKeyPem = GenerateRsaPublicKeyPem();

        var result = _sut.Verify(body, wrongKeyPem);

        Assert.False(result);
    }

    // ---------------------------------------------------------------------------
    // Verify — edge cases
    // ---------------------------------------------------------------------------

    [Fact]
    public void Verify_NoSignatureProperty_ReturnsFalse()
    {
        var body = """{"type":"Delete","actor":"https://example.com/users/alice"}""";
        var pem = GenerateRsaPublicKeyPem();

        Assert.False(_sut.Verify(body, pem));
    }

    [Fact]
    public void Verify_WrongSignatureType_ReturnsFalse()
    {
        var body = """
        {
          "type": "Delete",
          "signature": {
            "type": "Ed25519Signature2020",
            "creator": "https://example.com/users/alice#main-key",
            "signatureValue": "abc123"
          }
        }
        """;
        var pem = GenerateRsaPublicKeyPem();

        Assert.False(_sut.Verify(body, pem));
    }

    [Fact]
    public void Verify_InvalidBase64SignatureValue_ReturnsFalse()
    {
        var body = """
        {
          "type": "Delete",
          "actor": "https://example.com/users/alice",
          "signature": {
            "type": "RsaSignature2017",
            "creator": "https://example.com/users/alice#main-key",
            "created": "2026-01-01T00:00:00Z",
            "signatureValue": "!!!not-valid-base64!!!"
          }
        }
        """;
        var pem = GenerateRsaPublicKeyPem();

        Assert.False(_sut.Verify(body, pem));
    }

    [Fact]
    public void Verify_TamperedDocument_ReturnsFalse()
    {
        using var rsa = RSA.Create(2048);
        var publicKeyPem = rsa.ExportRSAPublicKeyPem();

        var (body, _) = BuildSignedDocument(rsa, "https://example.com/users/alice",
            "https://example.com/users/alice#delete");

        // Tamper with the body after signing
        var tampered = body.Replace("alice", "mallory");

        Assert.False(_sut.Verify(tampered, publicKeyPem));
    }

    // ---------------------------------------------------------------------------
    // Round-trip sign + verify with a locally generated key
    // ---------------------------------------------------------------------------

    [Fact]
    public void Verify_SortedPropertySignature_VerifiesCorrectly()
    {
        using var rsa = RSA.Create(2048);
        var publicKeyPem = rsa.ExportRSAPublicKeyPem();

        var (body, _) = BuildSignedDocument(rsa,
            "https://example.com/users/testactor",
            "https://example.com/users/testactor#delete");

        Assert.True(_sut.Verify(body, publicKeyPem));
    }

    [Fact]
    public void Verify_RoundTrip_CorrectPublicKey_ReturnsTrue()
    {
        using var rsa = RSA.Create(2048);
        var publicKeyPem = rsa.ExportRSAPublicKeyPem();

        var actorId = "https://example.com/users/bob";
        var (body, _) = BuildSignedDocument(rsa, actorId, $"{actorId}#delete");

        Assert.True(_sut.Verify(body, publicKeyPem));
    }

    [Fact]
    public void Verify_RoundTrip_DifferentKey_ReturnsFalse()
    {
        using var signingKey = RSA.Create(2048);
        using var differentKey = RSA.Create(2048);

        var (body, _) = BuildSignedDocument(signingKey,
            "https://example.com/users/carol",
            "https://example.com/users/carol#delete");

        Assert.False(_sut.Verify(body, differentKey.ExportRSAPublicKeyPem()));
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static string GenerateRsaPublicKeyPem()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportRSAPublicKeyPem();
    }

    private const string IdentityContext = "https://w3id.org/identity/v1";

    /// <summary>
    /// Produces a signed Delete body using the same sorted-property canonicalization
    /// that LinkedDataSignatureVerifier uses for its first verification attempt.
    /// </summary>
    private static (string body, string signatureValue) BuildSignedDocument(
        RSA rsa, string actorId, string activityId)
    {
        var created = "2026-01-01T00:00:00Z";
        var creatorUri = actorId + "#main-key";

        var document = new JsonObject
        {
            ["@context"] = new JsonArray
            {
                "https://www.w3.org/ns/activitystreams",
                "https://w3id.org/security/v1"
            },
            ["id"] = activityId,
            ["type"] = "Delete",
            ["actor"] = actorId,
            ["object"] = actorId,
            ["to"] = "https://www.w3.org/ns/activitystreams#Public"
        };

        var sigOptions = new JsonObject
        {
            ["@context"] = IdentityContext,
            ["creator"] = creatorUri,
            ["created"] = created
        };

        var optionsHash = Sha256Hex(Encoding.UTF8.GetBytes(SortedJsonString(sigOptions)));
        var documentHash = Sha256Hex(Encoding.UTF8.GetBytes(SortedJsonString(document)));
        var toSign = Encoding.UTF8.GetBytes(optionsHash + documentHash);

        var sigBytes = rsa.SignData(toSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signatureValue = Convert.ToBase64String(sigBytes);

        document["signature"] = new JsonObject
        {
            ["type"] = "RsaSignature2017",
            ["creator"] = creatorUri,
            ["created"] = created,
            ["signatureValue"] = signatureValue
        };

        return (document.ToJsonString(), signatureValue);
    }

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
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
