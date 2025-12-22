using System.Text.Json;
using Broca.ActivityPub.Core.Interfaces;
using KristofferStrube.ActivityStreams;
using KristofferStrube.ActivityStreams.JsonLD;
using Microsoft.Extensions.Logging;

namespace Broca.ActivityPub.Client.Services;

/// <summary>
/// Builder for creating ActivityPub activities anchored to an actor's identity
/// </summary>
public class ActivityBuilder : IActivityBuilder
{
    private readonly string _actorId;
    private readonly string _baseUrl;
    private readonly ILogger _logger;
    private int _activityCounter;

    public string ActorId => _actorId;

    public ActivityBuilder(string actorId, string baseUrl, ILogger logger)
    {
        _actorId = actorId ?? throw new ArgumentNullException(nameof(actorId));
        _baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activityCounter = 0;
    }

    /// <summary>
    /// Generates a unique activity ID
    /// </summary>
    private string GenerateActivityId(string type)
    {
        var counter = Interlocked.Increment(ref _activityCounter);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return $"{_baseUrl}/activities/{type.ToLower()}-{timestamp}-{counter}";
    }

    /// <summary>
    /// Generates a unique object ID
    /// </summary>
    private string GenerateObjectId(string type)
    {
        var counter = Interlocked.Increment(ref _activityCounter);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        // Extract username from actor ID (e.g., https://example.com/ap/users/admin -> admin)
        var username = ExtractUsernameFromActorId(_actorId);
        if (!string.IsNullOrEmpty(username))
        {
            return $"{_baseUrl}/users/{username}/objects/{type.ToLower()}-{timestamp}-{counter}";
        }
        
        // Fallback for non-standard actor IDs
        return $"{_baseUrl}/objects/{type.ToLower()}-{timestamp}-{counter}";
    }
    
    /// <summary>
    /// Extracts the username from an actor ID URL
    /// </summary>
    private static string? ExtractUsernameFromActorId(string actorId)
    {
        try
        {
            var uri = new Uri(actorId);
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            // Look for /users/{username} pattern
            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (segments[i].Equals("users", StringComparison.OrdinalIgnoreCase))
                {
                    return segments[i + 1];
                }
            }
        }
        catch
        {
            // Invalid URI format, return null
        }
        
        return null;
    }

    public INoteBuilder CreateNote(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        return new NoteBuilder(_actorId, _baseUrl, content, _logger, GenerateActivityId, GenerateObjectId);
    }

    public Follow Follow(string targetActorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetActorId);

        _logger.LogDebug("Creating Follow activity: {ActorId} -> {TargetActorId}", _actorId, targetActorId);

        return new Follow
        {
            JsonLDContext = new List<ITermDefinition> { new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) },
            Id = GenerateActivityId("follow"),
            Type = new[] { "Follow" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(_actorId) } },
            Object = new List<IObjectOrLink> { new Link { Href = new Uri(targetActorId) } },
            Published = DateTime.UtcNow
        };
    }

    public Undo Undo(Activity originalActivity)
    {
        ArgumentNullException.ThrowIfNull(originalActivity);

        _logger.LogDebug("Creating Undo activity for: {OriginalActivityId}", originalActivity.Id);

        return new Undo
        {
            JsonLDContext = new List<ITermDefinition> { new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) },
            Id = GenerateActivityId("undo"),
            Type = new[] { "Undo" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(_actorId) } },
            Object = new List<IObjectOrLink> { originalActivity },
            Published = DateTime.UtcNow
        };
    }

    public Like Like(string objectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        _logger.LogDebug("Creating Like activity: {ActorId} likes {ObjectId}", _actorId, objectId);

        return new Like
        {
            JsonLDContext = new List<ITermDefinition> { new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) },
            Id = GenerateActivityId("like"),
            Type = new[] { "Like" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(_actorId) } },
            Object = new List<IObjectOrLink> { new Link { Href = new Uri(objectId) } },
            Published = DateTime.UtcNow
        };
    }

    public Announce Announce(string objectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        _logger.LogDebug("Creating Announce activity: {ActorId} announces {ObjectId}", _actorId, objectId);

        return new Announce
        {
            JsonLDContext = new List<ITermDefinition> { new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) },
            Id = GenerateActivityId("announce"),
            Type = new[] { "Announce" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(_actorId) } },
            Object = new List<IObjectOrLink> { new Link { Href = new Uri(objectId) } },
            Published = DateTime.UtcNow,
            To = new List<IObjectOrLink> { new Link { Href = new Uri("https://www.w3.org/ns/activitystreams#Public") } }
        };
    }

    public Accept Accept(Activity originalActivity)
    {
        ArgumentNullException.ThrowIfNull(originalActivity);

        _logger.LogDebug("Creating Accept activity for: {OriginalActivityId}", originalActivity.Id);

        return new Accept
        {
            JsonLDContext = new List<ITermDefinition> { new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) },
            Id = GenerateActivityId("accept"),
            Type = new[] { "Accept" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(_actorId) } },
            Object = new List<IObjectOrLink> { originalActivity },
            Published = DateTime.UtcNow
        };
    }

    public Reject Reject(Activity originalActivity)
    {
        ArgumentNullException.ThrowIfNull(originalActivity);

        _logger.LogDebug("Creating Reject activity for: {OriginalActivityId}", originalActivity.Id);

        return new Reject
        {
            JsonLDContext = new List<ITermDefinition> { new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) },
            Id = GenerateActivityId("reject"),
            Type = new[] { "Reject" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(_actorId) } },
            Object = new List<IObjectOrLink> { originalActivity },
            Published = DateTime.UtcNow
        };
    }

    public TentativeAccept TentativeAccept(Activity originalActivity)
    {
        ArgumentNullException.ThrowIfNull(originalActivity);

        _logger.LogDebug("Creating TentativeAccept activity for: {OriginalActivityId}", originalActivity.Id);

        return new TentativeAccept
        {
            JsonLDContext = new List<ITermDefinition> { new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) },
            Id = GenerateActivityId("tentativeaccept"),
            Type = new[] { "TentativeAccept" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(_actorId) } },
            Object = new List<IObjectOrLink> { originalActivity },
            Published = DateTime.UtcNow
        };
    }

    public Delete Delete(string objectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);

        _logger.LogDebug("Creating Delete activity: {ActorId} deletes {ObjectId}", _actorId, objectId);

        return new Delete
        {
            JsonLDContext = new List<ITermDefinition> { new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) },
            Id = GenerateActivityId("delete"),
            Type = new[] { "Delete" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(_actorId) } },
            Object = new List<IObjectOrLink> { new Link { Href = new Uri(objectId) } },
            Published = DateTime.UtcNow
        };
    }

    public Block Block(string targetActorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetActorId);

        _logger.LogDebug("Creating Block activity: {ActorId} blocks {TargetActorId}", _actorId, targetActorId);

        return new Block
        {
            JsonLDContext = new List<ITermDefinition> { new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) },
            Id = GenerateActivityId("block"),
            Type = new[] { "Block" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(_actorId) } },
            Object = new List<IObjectOrLink> { new Link { Href = new Uri(targetActorId) } },
            Published = DateTime.UtcNow
        };
    }

    public Create Create(IObject obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        _logger.LogDebug("Creating Create activity: {ActorId} creates {ObjectType}", _actorId, obj.Type?.FirstOrDefault());

        return new Create
        {
            JsonLDContext = new List<ITermDefinition> { new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) },
            Id = GenerateActivityId("create"),
            Type = new[] { "Create" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(_actorId) } },
            Object = new List<IObjectOrLink> { obj },
            Published = DateTime.UtcNow
        };
    }

    public Add Add(IObject obj, string targetCollectionUrl)
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetCollectionUrl);

        _logger.LogDebug("Creating Add activity: {ActorId} adds object to {TargetCollection}", _actorId, targetCollectionUrl);

        return new Add
        {
            JsonLDContext = new List<ITermDefinition> { new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) },
            Id = GenerateActivityId("add"),
            Type = new[] { "Add" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(_actorId) } },
            Object = new List<IObjectOrLink> { obj },
            Target = new List<IObjectOrLink> { new Link { Href = new Uri(targetCollectionUrl) } },
            Published = DateTime.UtcNow
        };
    }

    public Add Add(string objectId, string targetCollectionUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetCollectionUrl);

        _logger.LogDebug("Creating Add activity: {ActorId} adds {ObjectId} to {TargetCollection}", _actorId, objectId, targetCollectionUrl);

        return new Add
        {
            JsonLDContext = new List<ITermDefinition> { new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) },
            Id = GenerateActivityId("add"),
            Type = new[] { "Add" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(_actorId) } },
            Object = new List<IObjectOrLink> { new Link { Href = new Uri(objectId) } },
            Target = new List<IObjectOrLink> { new Link { Href = new Uri(targetCollectionUrl) } },
            Published = DateTime.UtcNow
        };
    }

    public Remove Remove(string objectId, string targetCollectionUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetCollectionUrl);

        _logger.LogDebug("Creating Remove activity: {ActorId} removes {ObjectId} from {TargetCollection}", _actorId, objectId, targetCollectionUrl);

        return new Remove
        {
            JsonLDContext = new List<ITermDefinition> { new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) },
            Id = GenerateActivityId("remove"),
            Type = new[] { "Remove" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(_actorId) } },
            Object = new List<IObjectOrLink> { new Link { Href = new Uri(objectId) } },
            Target = new List<IObjectOrLink> { new Link { Href = new Uri(targetCollectionUrl) } },
            Published = DateTime.UtcNow
        };
    }

    public Update Update(IObject obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        _logger.LogDebug("Creating Update activity: {ActorId} updates {ObjectType}", _actorId, obj.Type?.FirstOrDefault());

        return new Update
        {
            JsonLDContext = new List<ITermDefinition> { new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) },
            Id = GenerateActivityId("update"),
            Type = new[] { "Update" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(_actorId) } },
            Object = new List<IObjectOrLink> { obj },
            Published = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Builder for Note objects with fluent configuration
/// </summary>
internal class NoteBuilder : INoteBuilder
{
    private readonly string _actorId;
    private readonly string _baseUrl;
    private readonly string _content;
    private readonly ILogger _logger;
    private readonly Func<string, string> _generateActivityId;
    private readonly Func<string, string> _generateObjectId;

    private readonly List<IObjectOrLink> _to = new();
    private readonly List<IObjectOrLink> _cc = new();
    private readonly List<IObjectOrLink> _bcc = new();
    private readonly List<IObjectOrLink> _tags = new();
    private readonly List<IObjectOrLink> _attachments = new();
    private string? _inReplyTo;

    public NoteBuilder(
        string actorId, 
        string baseUrl, 
        string content, 
        ILogger logger,
        Func<string, string> generateActivityId,
        Func<string, string> generateObjectId)
    {
        _actorId = actorId;
        _baseUrl = baseUrl;
        _content = content;
        _logger = logger;
        _generateActivityId = generateActivityId;
        _generateObjectId = generateObjectId;
    }

    public INoteBuilder To(params string[] recipients)
    {
        foreach (var recipient in recipients)
        {
            _to.Add(new Link { Href = new Uri(recipient) });
        }
        return this;
    }

    public INoteBuilder Cc(params string[] recipients)
    {
        foreach (var recipient in recipients)
        {
            _cc.Add(new Link { Href = new Uri(recipient) });
        }
        return this;
    }

    public INoteBuilder Bcc(params string[] recipients)
    {
        foreach (var recipient in recipients)
        {
            _bcc.Add(new Link { Href = new Uri(recipient) });
        }
        return this;
    }

    public INoteBuilder ToPublic()
    {
        _to.Add(new Link { Href = new Uri("https://www.w3.org/ns/activitystreams#Public") });
        return this;
    }

    public INoteBuilder ToFollowers()
    {
        var followersUrl = $"{_actorId}/followers";
        _to.Add(new Link { Href = new Uri(followersUrl) });
        return this;
    }

    public INoteBuilder WithMention(string actorId, string name)
    {
        // Add to recipients
        _to.Add(new Link { Href = new Uri(actorId) });

        // Add as tag
        var mention = new Mention
        {
            Type = new[] { "Mention" },
            Href = new Uri(actorId),
            Name = new[] { name.StartsWith("@") ? name : $"@{name}" }
        };
        _tags.Add(mention);
        return this;
    }

    public INoteBuilder InReplyTo(string objectId)
    {
        _inReplyTo = objectId;
        return this;
    }

    public INoteBuilder WithAttachment(IObjectOrLink attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        _attachments.Add(attachment);
        return this;
    }

    public INoteBuilder WithDocument(string url, string mediaType, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);

        var document = new Document
        {
            Type = new[] { "Document" },
            MediaType = mediaType,
            Url = new List<Link> { new Link { Href = new Uri(url) } }
        };

        if (!string.IsNullOrWhiteSpace(name))
        {
            document.Name = new[] { name };
        }

        _attachments.Add(document);
        return this;
    }

    public INoteBuilder WithImage(string url, string? name = null, string mediaType = "image/jpeg")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);

        var image = new Image
        {
            Type = new[] { "Image" },
            MediaType = mediaType,
            Url = new List<Link> { new Link { Href = new Uri(url) } }
        };

        if (!string.IsNullOrWhiteSpace(name))
        {
            image.Name = new[] { name };
        }

        _attachments.Add(image);
        return this;
    }

    public Note BuildNote()
    {
        var noteId = _generateObjectId("note");
        
        _logger.LogDebug("Building Note: {NoteId} by {ActorId}", noteId, _actorId);

        var note = new Note
        {
            JsonLDContext = new List<ITermDefinition> { new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) },
            Id = noteId,
            Type = new[] { "Note" },
            AttributedTo = new List<IObjectOrLink> { new Link { Href = new Uri(_actorId) } },
            Content = new[] { _content },
            Published = DateTime.UtcNow,
            To = _to.Count > 0 ? _to : null,
            Cc = _cc.Count > 0 ? _cc : null,
            Bcc = _bcc.Count > 0 ? _bcc : null
        };

        if (_tags.Count > 0)
        {
            note.Tag = _tags;
        }

        if (_attachments.Count > 0)
        {
            note.Attachment = _attachments;
        }

        if (!string.IsNullOrWhiteSpace(_inReplyTo))
        {
            note.InReplyTo = new List<IObjectOrLink> { new Link { Href = new Uri(_inReplyTo) } };
        }

        return note;
    }

    public Activity Build()
    {
        var note = BuildNote();
        var createId = _generateActivityId("create");

        _logger.LogDebug("Building Create activity: {CreateId} for note {NoteId}", createId, note.Id);

        return new Activity
        {
            JsonLDContext = new List<ITermDefinition> { new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) },
            Id = createId,
            Type = new[] { "Create" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(_actorId) } },
            Object = new List<IObjectOrLink> { note },
            Published = note.Published,
            To = note.To,
            Cc = note.Cc,
            Bcc = note.Bcc
        };
    }
}
