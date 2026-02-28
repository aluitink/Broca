using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;

namespace Broca.ActivityPub.IntegrationTests;

/// <summary>
/// Integration tests for content-type header handling (L2 compliance item)
/// Verifies that the inbox endpoints accept all valid ActivityPub content-types,
/// including Mastodon's use of profile parameter: application/ld+json; profile="..."
/// </summary>
public class ContentTypeHandlingTests : TwoServerFixture
{
    [Theory]
    [InlineData("application/activity+json")]
    [InlineData("application/ld+json")]
    [InlineData("application/ld+json; profile=\"https://www.w3.org/ns/activitystreams\"")]
    [InlineData("application/ld+json;profile=\"https://www.w3.org/ns/activitystreams\"")]
    [InlineData("application/ld+json; profile=https://www.w3.org/ns/activitystreams")]
    [InlineData("APPLICATION/ACTIVITY+JSON")]
    [InlineData("APPLICATION/LD+JSON")]
    public async Task SharedInbox_ValidActivityPubContentTypes_Accepted(string contentType)
    {
        // Arrange - Create a recipient user on Server A
        using (var scopeA = ServerA.Services.CreateScope())
        {
            var actorRepo = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "recipient", ServerA.BaseUrl);
        }

        var recipientId = $"{ServerA.BaseUrl}/users/recipient";
        var senderId = "https://remote.example/users/sender";

        // Create a valid ActivityPub activity JSON
        var activityJson = $$"""
        {
            "@context": "https://www.w3.org/ns/activitystreams",
            "type": "Create",
            "id": "https://remote.example/activities/{{Guid.NewGuid()}}",
            "actor": "{{senderId}}",
            "to": ["{{recipientId}}"],
            "object": {
                "type": "Note",
                "id": "https://remote.example/notes/{{Guid.NewGuid()}}",
                "content": "Test note",
                "attributedTo": "{{senderId}}"
            }
        }
        """;

        // Act - POST to shared inbox with specified content-type
        using var request = new HttpRequestMessage(HttpMethod.Post, "/inbox")
        {
            Content = new StringContent(activityJson, Encoding.UTF8, "text/plain")
        };
        
        // Manually set the Content-Type header to test exact value
        request.Content.Headers.Remove("Content-Type");
        request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        
        // Add Date header (required for signature validation, even though we're bypassing signatures)
        request.Headers.TryAddWithoutValidation("Date", DateTimeOffset.UtcNow.ToString("r"));

        var response = await ClientA.SendAsync(request);

        // Assert - Should not be rejected due to content-type
        // Note: Might return 401 (missing signature) but should NOT return 415 (Unsupported Media Type)
        Assert.NotEqual(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        
        // If signature validation is disabled or bypassed, should succeed
        if (response.IsSuccessStatusCode)
        {
            Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted);
        }
    }

    [Theory]
    [InlineData("application/activity+json")]
    [InlineData("application/ld+json")]
    [InlineData("application/ld+json; profile=\"https://www.w3.org/ns/activitystreams\"")]
    [InlineData("application/ld+json;profile=\"https://www.w3.org/ns/activitystreams\"")]
    [InlineData("application/ld+json; profile=https://www.w3.org/ns/activitystreams")]
    public async Task UserInbox_ValidActivityPubContentTypes_Accepted(string contentType)
    {
        // Arrange - Create a recipient user on Server A
        using (var scopeA = ServerA.Services.CreateScope())
        {
            var actorRepo = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "user_inbox_test", ServerA.BaseUrl);
        }

        var recipientId = $"{ServerA.BaseUrl}/users/user_inbox_test";
        var senderId = "https://remote.example/users/sender";

        // Create a valid ActivityPub activity JSON
        var activityJson = $$"""
        {
            "@context": "https://www.w3.org/ns/activitystreams",
            "type": "Create",
            "id": "https://remote.example/activities/{{Guid.NewGuid()}}",
            "actor": "{{senderId}}",
            "to": ["{{recipientId}}"],
            "object": {
                "type": "Note",
                "id": "https://remote.example/notes/{{Guid.NewGuid()}}",
                "content": "Test note for user inbox",
                "attributedTo": "{{senderId}}"
            }
        }
        """;

        // Act - POST to user's individual inbox with specified content-type
        using var request = new HttpRequestMessage(HttpMethod.Post, "/users/user_inbox_test/inbox")
        {
            Content = new StringContent(activityJson, Encoding.UTF8, "text/plain")
        };
        
        // Manually set the Content-Type header to test exact value
        request.Content.Headers.Remove("Content-Type");
        request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        
        // Add Date header
        request.Headers.TryAddWithoutValidation("Date", DateTimeOffset.UtcNow.ToString("r"));

        var response = await ClientA.SendAsync(request);

        // Assert - Should not be rejected due to content-type (415)
        Assert.NotEqual(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        
        // If signature validation is bypassed, should succeed
        if (response.IsSuccessStatusCode)
        {
            Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted);
        }
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/plain")]
    [InlineData("text/html")]
    [InlineData("application/xml")]
    public async Task SharedInbox_InvalidContentTypes_Rejected(string contentType)
    {
        // Arrange - Create a recipient user on Server A
        using (var scopeA = ServerA.Services.CreateScope())
        {
            var actorRepo = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "reject_test", ServerA.BaseUrl);
        }

        var recipientId = $"{ServerA.BaseUrl}/users/reject_test";
        var senderId = "https://remote.example/users/sender";

        // Create activity JSON (valid content, but wrong content-type)
        var activityJson = $$"""
        {
            "@context": "https://www.w3.org/ns/activitystreams",
            "type": "Create",
            "id": "https://remote.example/activities/{{Guid.NewGuid()}}",
            "actor": "{{senderId}}",
            "to": ["{{recipientId}}"],
            "object": {
                "type": "Note",
                "id": "https://remote.example/notes/{{Guid.NewGuid()}}",
                "content": "Test note",
                "attributedTo": "{{senderId}}"
            }
        }
        """;

        // Act - POST to shared inbox with invalid content-type
        using var request = new HttpRequestMessage(HttpMethod.Post, "/inbox")
        {
            Content = new StringContent(activityJson, Encoding.UTF8, "text/plain")
        };
        
        // Manually set the Content-Type header
        request.Content.Headers.Remove("Content-Type");
        request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        
        // Add Date header
        request.Headers.TryAddWithoutValidation("Date", DateTimeOffset.UtcNow.ToString("r"));

        var response = await ClientA.SendAsync(request);

        // Assert - Should be rejected with 415 Unsupported Media Type
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/plain")]
    [InlineData("text/html")]
    public async Task UserInbox_InvalidContentTypes_Rejected(string contentType)
    {
        // Arrange - Create a recipient user on Server A
        using (var scopeA = ServerA.Services.CreateScope())
        {
            var actorRepo = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "user_reject_test", ServerA.BaseUrl);
        }

        var recipientId = $"{ServerA.BaseUrl}/users/user_reject_test";
        var senderId = "https://remote.example/users/sender";

        // Create activity JSON
        var activityJson = $$"""
        {
            "@context": "https://www.w3.org/ns/activitystreams",
            "type": "Create",
            "id": "https://remote.example/activities/{{Guid.NewGuid()}}",
            "actor": "{{senderId}}",
            "to": ["{{recipientId}}"],
            "object": {
                "type": "Note",
                "id": "https://remote.example/notes/{{Guid.NewGuid()}}",
                "content": "Test note",
                "attributedTo": "{{senderId}}"
            }
        }
        """;

        // Act - POST to user inbox with invalid content-type
        using var request = new HttpRequestMessage(HttpMethod.Post, "/users/user_reject_test/inbox")
        {
            Content = new StringContent(activityJson, Encoding.UTF8, "text/plain")
        };
        
        // Manually set the Content-Type header
        request.Content.Headers.Remove("Content-Type");
        request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        
        // Add Date header
        request.Headers.TryAddWithoutValidation("Date", DateTimeOffset.UtcNow.ToString("r"));

        var response = await ClientA.SendAsync(request);

        // Assert - Should be rejected with 415 Unsupported Media Type
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task SharedInbox_MastodonStyleContentType_AcceptedAndProcessed()
    {
        // This is a comprehensive end-to-end test simulating a real Mastodon delivery
        // with the exact content-type header that Mastodon uses
        
        // Arrange - Create a recipient user on Server A
        using (var scopeA = ServerA.Services.CreateScope())
        {
            var actorRepo = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "mastodon_test", ServerA.BaseUrl);
        }

        var recipientId = $"{ServerA.BaseUrl}/users/mastodon_test";
        var senderId = "https://mastodon.example/users/sender";

        // Create activity with Mastodon-style formatting
        var activityJson = $$"""
        {
            "@context": [
                "https://www.w3.org/ns/activitystreams",
                {
                    "ostatus": "http://ostatus.org#",
                    "atomUri": "ostatus:atomUri",
                    "inReplyToAtomUri": "ostatus:inReplyToAtomUri"
                }
            ],
            "type": "Create",
            "id": "https://mastodon.example/activities/{{Guid.NewGuid()}}",
            "actor": "{{senderId}}",
            "published": "{{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}}",
            "to": ["{{recipientId}}"],
            "cc": ["https://www.w3.org/ns/activitystreams#Public"],
            "object": {
                "type": "Note",
                "id": "https://mastodon.example/statuses/{{Guid.NewGuid()}}",
                "published": "{{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}}",
                "attributedTo": "{{senderId}}",
                "to": ["{{recipientId}}"],
                "cc": ["https://www.w3.org/ns/activitystreams#Public"],
                "content": "<p>Test note from Mastodon</p>",
                "contentMap": {
                    "en": "<p>Test note from Mastodon</p>"
                }
            }
        }
        """;

        // Act - POST with Mastodon's exact content-type format
        using var request = new HttpRequestMessage(HttpMethod.Post, "/inbox")
        {
            Content = new StringContent(activityJson, Encoding.UTF8, "text/plain")
        };
        
        // Mastodon sends this exact header
        request.Content.Headers.Remove("Content-Type");
        request.Content.Headers.TryAddWithoutValidation(
            "Content-Type", 
            "application/ld+json; profile=\"https://www.w3.org/ns/activitystreams\"");
        
        // Add Date header
        request.Headers.TryAddWithoutValidation("Date", DateTimeOffset.UtcNow.ToString("r"));

        var response = await ClientA.SendAsync(request);

        // Assert - Should not return 415 Unsupported Media Type
        Assert.NotEqual(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        
        // Content-type should be accepted (even if other validation might fail)
        Assert.True(
            response.StatusCode == HttpStatusCode.OK || 
            response.StatusCode == HttpStatusCode.Accepted ||
            response.StatusCode == HttpStatusCode.Unauthorized, // Signature validation
            $"Expected 200/202/401, got {response.StatusCode}");
    }

    [Fact]
    public async Task UserInbox_MastodonStyleContentType_AcceptedAndProcessed()
    {
        // Test user inbox with Mastodon-style content-type
        
        // Arrange
        using (var scopeA = ServerA.Services.CreateScope())
        {
            var actorRepo = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "mastodon_user_test", ServerA.BaseUrl);
        }

        var recipientId = $"{ServerA.BaseUrl}/users/mastodon_user_test";
        var senderId = "https://mastodon.example/users/remote";

        var activityJson = $$"""
        {
            "@context": "https://www.w3.org/ns/activitystreams",
            "type": "Follow",
            "id": "https://mastodon.example/follows/{{Guid.NewGuid()}}",
            "actor": "{{senderId}}",
            "object": "{{recipientId}}"
        }
        """;

        // Act - POST with Mastodon content-type to user inbox
        using var request = new HttpRequestMessage(HttpMethod.Post, "/users/mastodon_user_test/inbox")
        {
            Content = new StringContent(activityJson, Encoding.UTF8, "text/plain")
        };
        
        request.Content.Headers.Remove("Content-Type");
        request.Content.Headers.TryAddWithoutValidation(
            "Content-Type", 
            "application/ld+json; profile=\"https://www.w3.org/ns/activitystreams\"");
        
        request.Headers.TryAddWithoutValidation("Date", DateTimeOffset.UtcNow.ToString("r"));

        var response = await ClientA.SendAsync(request);

        // Assert - Content-type should be accepted
        Assert.NotEqual(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Theory]
    [InlineData("application/ld+json; profile=\"https://www.w3.org/ns/activitystreams\"; charset=utf-8")]
    [InlineData("application/activity+json; charset=utf-8")]
    [InlineData("application/ld+json;profile=\"https://www.w3.org/ns/activitystreams\";charset=utf-8")]
    public async Task SharedInbox_ContentTypeWithCharset_Accepted(string contentType)
    {
        // Test that charset parameter doesn't break content-type matching
        
        // Arrange
        using (var scopeA = ServerA.Services.CreateScope())
        {
            var actorRepo = scopeA.ServiceProvider.GetRequiredService<IActorRepository>();
            await TestDataSeeder.SeedActorAsync(actorRepo, "charset_test", ServerA.BaseUrl);
        }

        var recipientId = $"{ServerA.BaseUrl}/users/charset_test";
        var senderId = "https://remote.example/users/sender";

        var activityJson = $$"""
        {
            "@context": "https://www.w3.org/ns/activitystreams",
            "type": "Create",
            "id": "https://remote.example/activities/{{Guid.NewGuid()}}",
            "actor": "{{senderId}}",
            "to": ["{{recipientId}}"],
            "object": {
                "type": "Note",
                "id": "https://remote.example/notes/{{Guid.NewGuid()}}",
                "content": "Test note with charset",
                "attributedTo": "{{senderId}}"
            }
        }
        """;

        // Act
        using var request = new HttpRequestMessage(HttpMethod.Post, "/inbox")
        {
            Content = new StringContent(activityJson, Encoding.UTF8, "text/plain")
        };
        
        request.Content.Headers.Remove("Content-Type");
        request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        request.Headers.TryAddWithoutValidation("Date", DateTimeOffset.UtcNow.ToString("r"));

        var response = await ClientA.SendAsync(request);

        // Assert
        Assert.NotEqual(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }
}
