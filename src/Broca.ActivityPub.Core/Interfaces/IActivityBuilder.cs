using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Core.Interfaces;

/// <summary>
/// Builder for creating ActivityPub activities anchored to an actor's identity
/// </summary>
/// <remarks>
/// Provides a fluent API for constructing activities with automatic
/// actor attribution, ID generation, and proper audience targeting.
/// </remarks>
public interface IActivityBuilder
{
    /// <summary>
    /// The actor identity anchoring these activities
    /// </summary>
    string ActorId { get; }

    /// <summary>
    /// Creates a Note object
    /// </summary>
    /// <param name="content">The note content</param>
    /// <returns>Note builder for further configuration</returns>
    INoteBuilder CreateNote(string content);

    /// <summary>
    /// Creates a Follow activity
    /// </summary>
    /// <param name="targetActorId">The actor to follow</param>
    /// <returns>Follow activity</returns>
    Follow Follow(string targetActorId);

    /// <summary>
    /// Creates an Undo activity
    /// </summary>
    /// <param name="originalActivity">The activity to undo</param>
    /// <returns>Undo activity</returns>
    Undo Undo(Activity originalActivity);

    /// <summary>
    /// Creates a Like activity
    /// </summary>
    /// <param name="objectId">The object to like</param>
    /// <returns>Like activity</returns>
    Like Like(string objectId);

    /// <summary>
    /// Creates an Announce (boost/repost) activity
    /// </summary>
    /// <param name="objectId">The object to announce</param>
    /// <returns>Announce activity</returns>
    Announce Announce(string objectId);

    /// <summary>
    /// Creates an Accept activity (e.g., accepting a Follow)
    /// </summary>
    /// <param name="originalActivity">The activity to accept</param>
    /// <returns>Accept activity</returns>
    Accept Accept(Activity originalActivity);

    /// <summary>
    /// Creates a Reject activity (e.g., rejecting a Follow)
    /// </summary>
    /// <param name="originalActivity">The activity to reject</param>
    /// <returns>Reject activity</returns>
    Reject Reject(Activity originalActivity);

    /// <summary>
    /// Creates a TentativeAccept activity (e.g., pending approval)
    /// </summary>
    /// <param name="originalActivity">The activity to tentatively accept</param>
    /// <returns>TentativeAccept activity</returns>
    TentativeAccept TentativeAccept(Activity originalActivity);

    /// <summary>
    /// Creates a Delete activity
    /// </summary>
    /// <param name="objectId">The object to delete</param>
    /// <returns>Delete activity</returns>
    Delete Delete(string objectId);

    /// <summary>
    /// Creates a Block activity
    /// </summary>
    /// <param name="targetActorId">The actor to block</param>
    /// <returns>Block activity</returns>
    Block Block(string targetActorId);
}

/// <summary>
/// Builder for Note objects with fluent configuration
/// </summary>
public interface INoteBuilder
{
    /// <summary>
    /// Adds recipients to the "to" field (primary recipients)
    /// </summary>
    INoteBuilder To(params string[] recipients);

    /// <summary>
    /// Adds recipients to the "cc" field (carbon copy)
    /// </summary>
    INoteBuilder Cc(params string[] recipients);

    /// <summary>
    /// Adds recipients to the "bcc" field (blind carbon copy)
    /// </summary>
    INoteBuilder Bcc(params string[] recipients);

    /// <summary>
    /// Makes the note public
    /// </summary>
    INoteBuilder ToPublic();

    /// <summary>
    /// Addresses the note to the actor's followers
    /// </summary>
    INoteBuilder ToFollowers();

    /// <summary>
    /// Adds a tag or mention
    /// </summary>
    INoteBuilder WithMention(string actorId, string name);

    /// <summary>
    /// Sets in-reply-to for threading
    /// </summary>
    INoteBuilder InReplyTo(string objectId);

    /// <summary>
    /// Builds the Create activity containing this Note
    /// </summary>
    Activity Build();

    /// <summary>
    /// Builds just the Note object without wrapping in Create
    /// </summary>
    Note BuildNote();
}
