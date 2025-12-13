using Broca.ActivityPub.Client.Extensions;
using Broca.ActivityPub.Client.Services;
using Broca.ActivityPub.Core.Interfaces;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Broca.ActivityPub.Client.Examples;

/// <summary>
/// Examples demonstrating the unified activity authoring layer
/// </summary>
public static class ActivityAuthoringExamples
{
    /// <summary>
    /// Example: Create and post a simple note to the public timeline
    /// </summary>
    public static async Task PostPublicNoteAsync(IActivityPubClient client)
    {
        // Create an activity builder anchored to the authenticated user's identity
        var builder = client.CreateActivityBuilder();

        // Build a public note
        var createActivity = builder
            .CreateNote("Hello, ActivityPub world! This is my first post.")
            .ToPublic()  // Post to public timeline
            .Build();

        // Post to the authenticated user's outbox
        // This will result in delivery to followers
        var response = await client.PostToOutboxAsync(createActivity);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Successfully posted! Activity ID: {createActivity.Id}");
        }
    }

    /// <summary>
    /// Example: Create a note with mentions
    /// </summary>
    public static async Task PostNoteWithMentionsAsync(IActivityPubClient client)
    {
        var builder = client.CreateActivityBuilder();

        var createActivity = builder
            .CreateNote("Hey @alice, check out this cool ActivityPub implementation!")
            .ToPublic()
            .WithMention("https://example.com/users/alice", "@alice")
            .Build();

        var response = await client.PostToOutboxAsync(createActivity);
    }

    /// <summary>
    /// Example: Create a reply to another post
    /// </summary>
    public static async Task ReplyToPostAsync(IActivityPubClient client, string originalPostId)
    {
        var builder = client.CreateActivityBuilder();

        var createActivity = builder
            .CreateNote("Great point! I totally agree with this.")
            .InReplyTo(originalPostId)  // Thread the conversation
            .ToPublic()
            .Build();

        var response = await client.PostToOutboxAsync(createActivity);
    }

    /// <summary>
    /// Example: Follow another user
    /// </summary>
    public static async Task FollowUserAsync(IActivityPubClient client, string targetActorId)
    {
        var builder = client.CreateActivityBuilder();

        var followActivity = builder.Follow(targetActorId);

        var response = await client.PostToOutboxAsync(followActivity);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Successfully sent follow request to {targetActorId}");
        }
    }

    /// <summary>
    /// Example: Unfollow a user
    /// </summary>
    public static async Task UnfollowUserAsync(
        IActivityPubClient client, 
        string targetActorId, 
        Activity originalFollowActivity)
    {
        var builder = client.CreateActivityBuilder();

        var undoActivity = builder.Undo(originalFollowActivity);

        var response = await client.PostToOutboxAsync(undoActivity);
    }

    /// <summary>
    /// Example: Like a post
    /// </summary>
    public static async Task LikePostAsync(IActivityPubClient client, string objectId)
    {
        var builder = client.CreateActivityBuilder();

        var likeActivity = builder.Like(objectId);

        var response = await client.PostToOutboxAsync(likeActivity);
    }

    /// <summary>
    /// Example: Boost/Announce a post
    /// </summary>
    public static async Task BoostPostAsync(IActivityPubClient client, string objectId)
    {
        var builder = client.CreateActivityBuilder();

        var announceActivity = builder.Announce(objectId);

        var response = await client.PostToOutboxAsync(announceActivity);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Successfully boosted post {objectId}");
        }
    }

    /// <summary>
    /// Example: Post to followers only
    /// </summary>
    public static async Task PostToFollowersOnlyAsync(IActivityPubClient client)
    {
        var builder = client.CreateActivityBuilder();

        var createActivity = builder
            .CreateNote("This is a followers-only post. Not public!")
            .ToFollowers()  // Only send to followers, not public
            .Build();

        var response = await client.PostToOutboxAsync(createActivity);
    }

    /// <summary>
    /// Example: Direct message to specific users
    /// </summary>
    public static async Task SendDirectMessageAsync(IActivityPubClient client, params string[] recipientActorIds)
    {
        var builder = client.CreateActivityBuilder();

        var noteBuilder = builder
            .CreateNote("This is a private direct message.");

        // Add each recipient
        foreach (var recipientId in recipientActorIds)
        {
            noteBuilder.To(recipientId);
        }

        var createActivity = noteBuilder.Build();

        var response = await client.PostToOutboxAsync(createActivity);
    }

    /// <summary>
    /// Complete example showing how to set up an authenticated client and post
    /// </summary>
    public static async Task CompleteAuthenticatedExample()
    {
        // 1. Set up dependency injection
        var services = new ServiceCollection();
        
        // 2. Configure authenticated ActivityPub client
        services.AddActivityPubClientAuthenticated(
            actorId: "https://example.com/users/john",
            privateKeyPem: "-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----",
            publicKeyId: "https://example.com/users/john#main-key"
        );

        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<IActivityPubClient>();

        // 3. Verify authentication
        var self = await client.GetSelfAsync();
        Console.WriteLine($"Authenticated as: {self?.PreferredUsername}");

        // 4. Create and post an activity
        var builder = client.CreateActivityBuilder();
        
        var createActivity = builder
            .CreateNote("My first authenticated post using the unified activity authoring layer!")
            .ToPublic()
            .Build();

        var response = await client.PostToOutboxAsync(createActivity);
        
        Console.WriteLine($"Post status: {response.StatusCode}");
    }

    /// <summary>
    /// Example: Server-to-server delivery using system actor
    /// </summary>
    /// <remarks>
    /// The server's system actor (sys@domain) can be used to perform
    /// federated requests with a verifiable identity.
    /// </remarks>
    public static async Task ServerFederatedRequestExample(
        ISystemIdentityService systemIdentity,
        IActivityPubClient client)
    {
        // Get the server's system actor
        var systemActor = await systemIdentity.GetSystemActorAsync();
        
        Console.WriteLine($"System actor: {systemActor.Id}");
        Console.WriteLine($"System alias: {systemIdentity.SystemActorAlias}");

        // The system actor can now be used to make authenticated requests
        // to other servers on behalf of the server itself
        
        // For example, following another server's relay actor
        var builder = client.CreateActivityBuilder();
        var followRelay = builder.Follow("https://relay.example.com/actor");
        
        // This would be signed with the system actor's credentials
        var response = await client.PostToOutboxAsync(followRelay);
    }
}
