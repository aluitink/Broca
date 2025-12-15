using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Client.Services;
using KristofferStrube.ActivityStreams;
using KristofferStrube.ActivityStreams.JsonLD;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Broca.ActivityPub.IntegrationTests.Infrastructure;

/// <summary>
/// Helper for seeding test data into in-memory repositories
/// </summary>
public static class TestDataSeeder
{
    private static readonly ILogger _logger = NullLogger.Instance;

    /// <summary>
    /// Creates an ActivityBuilder for a given actor
    /// </summary>
    private static IActivityBuilder CreateBuilderForActor(string actorId)
    {
        // Extract base URL from actor ID
        var actorUri = new Uri(actorId);
        var baseUrl = $"{actorUri.Scheme}://{actorUri.Authority}";

        return new ActivityBuilder(actorId, baseUrl, _logger);
    }

    /// <summary>
    /// Seeds a test actor into the repository with generated keys
    /// </summary>
    /// <param name="actorRepository">The actor repository to seed into</param>
    /// <param name="username">The username for the actor</param>
    /// <param name="baseUrl">The base URL of the server</param>
    /// <param name="manuallyApprovesFollowers">Whether the actor manually approves followers</param>
    /// <returns>Tuple containing the actor and the generated private key PEM</returns>
    public static async Task<(Actor actor, string privateKeyPem)> SeedActorAsync(
        IActorRepository actorRepository, 
        string username, 
        string baseUrl,
        bool manuallyApprovesFollowers = false)
    {
        var (privateKeyPem, publicKeyPem) = KeyGenerator.GenerateKeyPair();
        
        // Build actor ID - assumes default /users/{username} pattern
        // In tests, RoutePrefix is empty by default
        var actorId = $"{baseUrl}/users/{username}";
        var publicKeyId = $"{actorId}#main-key";

        var actor = new Actor
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")),
                new ReferenceTermDefinition(new Uri("https://w3id.org/security/v1"))
            },
            Id = actorId,
            Type = new[] { "Person" },
            PreferredUsername = username,
            Name = new[] { username },
            Summary = new[] { $"Test user {username}" },
            // Construct inbox/outbox/etc by appending to actor ID
            // This matches how the server constructs them
            Inbox = new Link { Href = new Uri($"{actorId}/inbox") },
            Outbox = new Link { Href = new Uri($"{actorId}/outbox") },
            Followers = new Link { Href = new Uri($"{actorId}/followers") },
            Following = new Link { Href = new Uri($"{actorId}/following") },
            ExtensionData = new Dictionary<string, JsonElement>
            {
                {
                    "publicKey",
                    JsonSerializer.SerializeToElement(new
                    {
                        id = publicKeyId,
                        owner = actorId,
                        publicKeyPem = publicKeyPem
                    })
                },
                {
                    "privateKeyPem",
                    JsonSerializer.SerializeToElement(privateKeyPem)
                },
                {
                    "manuallyApprovesFollowers",
                    JsonSerializer.SerializeToElement(manuallyApprovesFollowers)
                }
            }
        };

        await actorRepository.SaveActorAsync(username, actor);

        return (actor, privateKeyPem);
    }

    /// <summary>
    /// Creates a Note activity
    /// </summary>
    public static Note CreateNote(string actorId, string content, string? noteId = null)
    {
        var builder = CreateBuilderForActor(actorId);
        return builder.CreateNote(content).BuildNote();
    }

    /// <summary>
    /// Creates a Note activity with attachments
    /// </summary>
    public static Activity CreateNoteWithAttachments(
        string actorId, 
        string content, 
        IEnumerable<Document> attachments,
        string? noteId = null)
    {
        noteId ??= $"{actorId}/notes/{Guid.NewGuid()}";

        return new Activity
        {
            JsonLDContext = new List<ITermDefinition>
            {
                new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
            },
            Id = noteId,
            Type = new[] { "Note" },
            AttributedTo = new IObjectOrLink[] { new Actor { Id = actorId } },
            Content = new[] { content },
            Attachment = attachments.Cast<IObjectOrLink>().ToList(),
            Published = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a Document attachment (for images, videos, etc.)
    /// </summary>
    public static Document CreateDocument(string url, string mediaType, string? name = null)
    {
        var document = new Document
        {
            Type = new[] { "Document" },
            MediaType = mediaType,
            Url = new List<Link> { new Link { Href = new Uri(url) } }
        };

        if (!string.IsNullOrEmpty(name))
        {
            document.Name = new[] { name };
        }

        return document;
    }

    /// <summary>
    /// Creates an Image attachment
    /// </summary>
    public static Image CreateImage(string url, string? name = null, string mediaType = "image/jpeg")
    {
        var image = new Image
        {
            Type = new[] { "Image" },
            MediaType = mediaType,
            Url = new List<Link> { new Link { Href = new Uri(url) } }
        };

        if (!string.IsNullOrEmpty(name))
        {
            image.Name = new[] { name };
        }

        return image;
    }

    /// <summary>
    /// Creates a Create activity wrapping a Note
    /// </summary>
    public static Activity CreateCreateActivity(
        string actorId, 
        string content, 
        string? activityId = null, 
        string? noteId = null,
        string[]? to = null,
        string[]? cc = null,
        string[]? bcc = null)
    {
        var builder = CreateBuilderForActor(actorId);
        var noteBuilder = builder.CreateNote(content);

        // Add addressing if provided
        if (to != null && to.Length > 0)
        {
            noteBuilder.To(to);
        }

        if (cc != null && cc.Length > 0)
        {
            noteBuilder.Cc(cc);
        }

        if (bcc != null && bcc.Length > 0)
        {
            noteBuilder.Bcc(bcc);
        }

        return noteBuilder.Build();
    }

    /// <summary>
    /// Creates a Like activity
    /// </summary>
    public static Like CreateLike(string actorId, string objectId, string? activityId = null)
    {
        var builder = CreateBuilderForActor(actorId);
        return builder.Like(objectId);
    }

    /// <summary>
    /// Creates a Follow activity
    /// </summary>
    public static Follow CreateFollow(string actorId, string targetActorId, string? activityId = null)
    {
        var builder = CreateBuilderForActor(actorId);
        return builder.Follow(targetActorId);
    }

    /// <summary>
    /// Creates an Undo activity wrapping another activity
    /// </summary>
    public static Undo CreateUndo(string actorId, Activity activityToUndo, string? activityId = null)
    {
        var builder = CreateBuilderForActor(actorId);
        return builder.Undo(activityToUndo);
    }

    /// <summary>
    /// Creates a Create activity with an attachment (image, video, etc.)
    /// </summary>
    public static Activity CreateCreateActivityWithAttachment(
        string actorId,
        string content,
        string attachmentUrl,
        string attachmentMediaType,
        string? attachmentName = null)
    {
        var builder = CreateBuilderForActor(actorId);
        
        return builder.CreateNote(content)
            .WithDocument(attachmentUrl, attachmentMediaType, attachmentName)
            .Build();
    }

    /// <summary>
    /// Creates a Create activity with an attachment addressed to a specific recipient
    /// </summary>
    public static Activity CreateCreateActivityWithAttachmentToRecipient(
        string actorId,
        string recipientId,
        string content,
        string attachmentUrl,
        string attachmentMediaType,
        string? attachmentName = null)
    {
        var builder = CreateBuilderForActor(actorId);
        
        return builder.CreateNote(content)
            .To(recipientId)
            .WithDocument(attachmentUrl, attachmentMediaType, attachmentName)
            .Build();
    }
}
