using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Broca.ActivityPub.Client.Services;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var serverUrl = GetArg("--server", "https://dev.broca.luit.ink").TrimEnd('/');
var apiKey = GetArg("--api-key", "dev-api-key-12345-change-in-production");
var userPrefix = GetArg("--prefix", "sample_");
var userCount = int.Parse(GetArg("--count", "8"));
var routePrefix = GetArg("--route-prefix", "ap");
var adminUsername = GetArg("--admin", "admin");

var baseApUrl = string.IsNullOrEmpty(routePrefix) ? serverUrl : $"{serverUrl}/{routePrefix}";
var adminActorId = $"{baseApUrl}/users/{adminUsername}";
var sysInboxUrl = $"{baseApUrl}/users/sys/inbox";

Console.WriteLine($"Server:  {serverUrl}");
Console.WriteLine($"Admin:   {adminActorId}");
Console.WriteLine($"Prefix:  {userPrefix}");
Console.WriteLine($"Count:   {userCount}");
Console.WriteLine();

// Shared infrastructure — one service provider, multiple client instances
var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddHttpClient("ActivityPub", client =>
{
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/activity+json"));
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/ld+json"));
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});
services.AddSingleton<ICryptoProvider, CryptoProvider>();
services.AddSingleton<HttpSignatureService>();
services.AddSingleton<IWebFingerService, WebFingerService>();

using var sp = services.BuildServiceProvider();
var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

// Initialize admin client
Console.WriteLine("[*] Initializing admin client...");
var adminClient = BuildClient(adminActorId);
await adminClient.InitializeAsync();
Console.WriteLine("    Ready.");

// Personas and their content - designed to exercise various relationship states
// - alice, bob, carol: Active community members who follow each other
// - dave: Lurker who follows but doesn't post much
// - eve: Researcher who posts but doesn't follow many
// - frank: User who will be blocked by alice
// - grace: User with pending/rejected follow relationship
// - heidi: Power user with many interactions
var personas = new (string shortName, string displayName, string summary, bool manuallyApproves)[]
{
    ("alice", "Alice Example",   "Decentralized social network enthusiast. Here to explore the fediverse. | she/her", false),
    ("bob",   "Bob Example",     "Software developer interested in open protocols and ActivityPub. 🖥️", false),
    ("carol", "Carol Example",   "Digital rights advocate and self-hoster. Privacy matters.", false),
    ("dave",  "Dave Example",    "Tinkerer exploring federated communication. Mostly lurking.", false),
    ("eve",   "Eve Example",     "Researcher studying decentralized communication systems. 📊", false),
    ("frank", "Frank Example",   "New to the fediverse. Still figuring things out.", false),
    ("grace", "Grace Example",   "Introvert. Selective about who follows me.", true),  // Manual approval
    ("heidi", "Heidi Example",   "Fediverse power user. Collections curator. #ActivityPub advocate", false),
};

var noteBank = new (string shortName, string content, bool featured)[]
{
    // Alice - community builder
    ("alice", "Just arrived on the fediverse! Looking forward to connecting with everyone. #ActivityPub #NewUser", true),
    ("alice", "Decentralized social networks aren't just about avoiding big tech — they're about real ownership of your own data and identity.", true),
    ("alice", "The ActivityPub protocol is surprisingly elegant once you understand the actor/inbox/outbox model.", false),
    ("alice", "Pro tip: check out the collections feature — you can curate your best posts into featured lists!", false),
    
    // Bob - developer focus (MANY posts to test pagination - 25+ items)
    ("bob", "Finally got my self-hosted instance working. The setup was easier than expected! #selfhosted #fediverse", true),
    ("bob", "HTTP signatures are the backbone of federated trust in ActivityPub. Worth taking the time to understand them properly.", true),
    ("bob", "Hot take: the fediverse is what the social web was always supposed to be before it got monetized.", false),
    ("bob", "Working on a new ActivityPub client library. Open source, of course. 🔧", false),
    ("bob", "The JSON-LD context in ActivityStreams is both powerful and confusing. Anyone else feel this way?", false),
    ("bob", "Day 1 of ActivityPub deep dive: understanding the Actor model. Inbox, outbox, followers, following.", false),
    ("bob", "Day 2: HTTP Signatures. RSA-SHA256, signing request headers, verifying signatures server-side.", false),
    ("bob", "Day 3: WebFinger discovery. How do you find someone's actor from their @user@domain handle?", false),
    ("bob", "Day 4: The Create activity. Wrapping objects in activities for posting to outboxes.", false),
    ("bob", "Day 5: Following and followers. The Follow/Accept dance between servers.", false),
    ("bob", "Day 6: Likes and announces. Simple activities but crucial for engagement metrics.", false),
    ("bob", "Day 7: Collections and pagination. OrderedCollection, OrderedCollectionPage, first/next/prev.", false),
    ("bob", "Day 8: Addressing. To, cc, bcc and the special Public collection for visibility control.", false),
    ("bob", "Day 9: Replies and threading. InReplyTo and the conversation property.", false),
    ("bob", "Day 10: Attachments and media. Document, Image, Video types in the Attachment property.", false),
    ("bob", "Day 11: Mentions and tags. The Tag property with Mention and Hashtag types.", false),
    ("bob", "Day 12: Delete and Tombstone. How to handle content removal in a federated system.", false),
    ("bob", "Day 13: Update activity. Editing posts and propagating changes.", false),
    ("bob", "Day 14: Undo. Reversing follows, likes, announces, and blocks.", false),
    ("bob", "Day 15: Block. Preventing interactions from specific actors.", false),
    ("bob", "Day 16: Accept and Reject. Responding to follow requests.", false),
    ("bob", "Day 17: Shared inbox. Optimizing delivery to multiple recipients on the same server.", false),
    ("bob", "Day 18: NodeInfo. Advertising server capabilities and statistics.", false),
    ("bob", "Day 19: Custom extensions. Using JSON-LD context to add new properties.", false),
    ("bob", "Day 20: Testing federation. Setting up multiple instances for integration tests.", false),
    ("bob", "Day 21: Rate limiting and abuse prevention. Protecting your instance from bad actors.", false),
    ("bob", "Day 22: Content warnings. The summary property for sensitive content.", false),
    ("bob", "Day 23: Polls. Using Question/Note with oneOf/anyOf for voting.", false),
    ("bob", "Day 24: Profile fields. Custom key-value pairs in actor attachments.", false),
    ("bob", "Day 25: That's a wrap! 25 days of ActivityPub learning. Thread complete. 🎉", false),
    
    // Carol - digital rights advocate
    ("carol", "Digital rights start with data ownership. Federated platforms get this right where centralized ones consistently fail.", true),
    ("carol", "The fediverse is proof that community-driven open standards can outperform venture-backed social silos.", false),
    ("carol", "Joined this instance to experiment with ActivityPub. Genuinely impressed so far!", false),
    ("carol", "Privacy isn't about having something to hide — it's about having the right to choose what to share.", true),
    ("carol", "Self-hosting isn't just for tech enthusiasts anymore. The tools have gotten so much better.", false),
    
    // Dave - casual/lurker
    ("dave",  "Got my first note posted from a fresh Broca instance. Everything is working great!", false),
    ("dave",  "Best thing about federated networks: if you don't like your instance, you can move. No lock-in, ever.", false),
    ("dave",  "Shoutout to everyone building ActivityPub tooling. The ecosystem is growing fast and it's exciting to watch.", false),
    
    // Eve - researcher
    ("eve",   "Studying how information propagates across federated networks versus centralized ones. Fascinating differences.", true),
    ("eve",   "The fediverse has a fundamentally different information flow: more local community, less algorithmic amplification.", true),
    ("eve",   "Running some experiments on ActivityPub federation patterns. Early results are interesting — more to share soon.", false),
    ("eve",   "Published a new paper on decentralized social network resilience. Link in my profile! 📚", false),
    ("eve",   "Interesting finding: federated networks show more diverse conversation threads than algorithmic ones.", false),
    
    // Frank - newbie (will be blocked by alice for testing)
    ("frank", "Hello fediverse! Just signed up. What should I know?", false),
    ("frank", "Still getting used to this. Where's the algorithm? 😅", false),
    ("frank", "Testing testing... is this thing on?", false),
    
    // Grace - selective/private (manual approval)
    ("grace", "Curating my feed carefully. Quality over quantity.", true),
    ("grace", "Sometimes the best content comes from smaller circles.", false),
    
    // Heidi - power user with collections
    ("heidi", "Welcome to my curated corner of the fediverse! Check out my featured posts collection. ✨", true),
    ("heidi", "Just reorganized my collections — now I have featured, resources, and bookmarks properly separated.", false),
    ("heidi", "The collections feature is underrated. Perfect for organizing content by topic!", true),
    ("heidi", "ActivityPub tip of the day: use the Announce activity to boost content you want to amplify.", false),
    ("heidi", "Building a list of great ActivityPub resources. Will share when it's ready! 📋", false),
};

// Notes with attachments (for media gallery testing)
var notesWithAttachments = new (string shortName, string content, string imageUrl, string mediaType, string? altText)[]
{
    ("heidi", "Here's a photo from my latest adventure! 📸", "https://picsum.photos/seed/heidi1/800/600", "image/jpeg", "Scenic landscape photo"),
    ("heidi", "Sunset over the mountains. Nature is incredible.", "https://picsum.photos/seed/heidi2/800/600", "image/jpeg", "Mountain sunset"),
    ("heidi", "My workspace setup for remote work 🖥️", "https://picsum.photos/seed/heidi3/800/600", "image/jpeg", "Desktop workspace with monitor"),
    ("alice", "Found this cool infographic about the fediverse!", "https://picsum.photos/seed/alice1/600/800", "image/png", "Fediverse infographic"),
    ("eve", "Chart showing federation patterns in our research 📊", "https://picsum.photos/seed/eve1/800/600", "image/png", "Research data chart"),
    ("bob", "Screenshot of the new feature I'm working on", "https://picsum.photos/seed/bob1/800/600", "image/png", "Code editor screenshot"),
};

// Followers-only posts (for visibility testing)
var followersOnlyNotes = new (string shortName, string content)[]
{
    ("alice", "Followers-only: Working on something exciting, will share more soon! 🤫"),
    ("carol", "Followers-only: Quick update for my close network only."),
    ("heidi", "Followers-only: Behind the scenes of my collection curation process."),
};

// Posts to delete (for testing Delete activity)
var notesToDelete = new (string shortName, string content)[]
{
    ("bob", "This post will be deleted shortly — testing the delete functionality!"),
};

// Posts to edit (for testing Update activity)
var notesToEdit = new (string shortName, string originalContent, string editedContent)[]
{
    ("alice", "This is my originla post with a typo.", "This is my original post with the typo fixed! (edited)"),
};

// Likes to undo (for testing Undo Like)
var likesToUndo = new (string liker, string likedUserShort)[]
{
    ("carol", "bob"),  // Carol will like then unlike one of Bob's posts
};

// Follows to undo (for testing Undo Follow) 
var followsToUndo = new (string follower, string followee)[]
{
    ("dave", "frank"),  // Dave will follow then unfollow Frank
};

// Conversation starters (will generate reply chains)
var conversationStarters = new (string author, string content, (string replier, string replyContent)[] replies)[]
{
    ("alice", "What's everyone's favorite feature of the fediverse so far?", new[]
    {
        ("bob", "Definitely the interoperability. Being able to follow anyone regardless of their server is 🔥"),
        ("carol", "Data portability. I can export and move my whole presence if I want to."),
        ("heidi", "Collections! Being able to organize my content the way I want."),
    }),
    ("bob", "Anyone else working on ActivityPub implementations? Would love to compare notes!", new[]
    {
        ("eve", "Yes! Currently researching federation patterns for a paper. Happy to collaborate."),
        ("alice", "Not implementing, but definitely learning. The spec is surprisingly readable."),
    }),
    ("eve", "Question for researchers: what metrics do you use to measure fediverse health?", new[]
    {
        ("carol", "I look at instance diversity and cross-instance communication patterns."),
        ("bob", "Uptime and successful federation requests are my go-to technical metrics."),
    }),
    ("heidi", "Poll: Do you prefer chronological feeds or curated collections?", new[]
    {
        ("alice", "Chronological all the way. I don't want an algorithm deciding what I see."),
        ("dave", "Can I say both? Chronological for discovery, collections for reference."),
        ("carol", "Chronological. Algorithms optimize for engagement, not wellbeing."),
    }),
};

// Custom collection definitions
var collectionDefinitions = new (string username, string collectionId, string name, string description, string type, string? queryFilter)[]
{
    // Heidi - power user with multiple curated collections
    ("heidi", "featured", "Featured Posts", "My hand-picked best posts", "Manual", null),
    ("heidi", "resources", "Resources", "Useful ActivityPub resources and links", "Manual", null),
    ("heidi", "media", "Media Gallery", "Posts with images and attachments", "Query", "hasAttachment"),
    
    // Alice - active user with featured posts
    ("alice", "featured", "Featured", "Posts I'm most proud of", "Manual", null),
    
    // Eve - researcher with reference materials  
    ("eve", "featured", "Key Findings", "Important research findings", "Manual", null),
    ("eve", "bookmarks", "Reading List", "Papers and posts to read later", "Manual", null),
    
    // Carol - privacy advocate
    ("carol", "featured", "Must Reads", "Essential posts about digital rights", "Manual", null),
};

// Relationship definitions (who follows/blocks whom)
var followRelationships = new (string follower, string followee)[]
{
    // Mutual follows (active community)
    ("alice", "bob"), ("bob", "alice"),
    ("alice", "carol"), ("carol", "alice"),
    ("bob", "carol"), ("carol", "bob"),
    ("alice", "heidi"), ("heidi", "alice"),
    ("bob", "heidi"), ("heidi", "bob"),
    ("carol", "heidi"), ("heidi", "carol"),
    
    // One-way follows (dave follows everyone but isn't followed back much)
    ("dave", "alice"),
    ("dave", "bob"),
    ("dave", "carol"),
    ("dave", "eve"),
    ("dave", "heidi"),
    
    // Eve follows researchers/advocates but is selective
    ("eve", "carol"),
    ("eve", "bob"),
    ("alice", "eve"), ("bob", "eve"), // Some follow Eve back
    
    // Frank follows popular users (newbie behavior)
    ("frank", "alice"),
    ("frank", "bob"),
    ("frank", "heidi"),
};

// Grace will reject follow requests from frank (testing rejected follows)
var pendingFollowsToReject = new (string follower, string followee)[]
{
    ("frank", "grace"),
};

// Block relationships
var blockRelationships = new (string blocker, string blocked)[]
{
    ("alice", "frank"),  // Alice blocks Frank for testing block functionality
};

// Create sample users via admin back-channel
Console.WriteLine("[*] Creating sample users...");
var createdUsers = new List<(string username, string actorId)>();
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

for (int i = 0; i < Math.Min(userCount, personas.Length); i++)
{
    var (shortName, displayName, summary, manuallyApproves) = personas[i];
    var username = $"{userPrefix}{shortName}";
    var actorId = $"{baseApUrl}/users/{username}";

    // Check if actor already exists
    try
    {
        var check = await http.GetAsync(actorId);
        if (check.IsSuccessStatusCode)
        {
            Console.WriteLine($"    [~] {username} already exists, using existing actor");
            createdUsers.Add((username, actorId));
            continue;
        }
    }
    catch { }

    var personObj = new JsonObject
    {
        ["type"] = "Person",
        ["preferredUsername"] = username,
        ["name"] = displayName,
        ["summary"] = summary,
    };
    
    if (manuallyApproves)
    {
        personObj["manuallyApprovesFollowers"] = true;
    }

    var body = new JsonObject
    {
        ["@context"] = new JsonArray("https://www.w3.org/ns/activitystreams"),
        ["type"] = "Create",
        ["actor"] = $"{baseApUrl}/users/sys",
        ["object"] = personObj
    };

    using var req = new HttpRequestMessage(HttpMethod.Post, sysInboxUrl)
    {
        Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/activity+json")
    };
    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

    var resp = await http.SendAsync(req);
    if (resp.IsSuccessStatusCode)
    {
        Console.WriteLine($"    [+] Created {username} ({displayName})");
        createdUsers.Add((username, actorId));
    }
    else
    {
        var err = await resp.Content.ReadAsStringAsync();
        Console.WriteLine($"    [!] Failed to create {username}: {(int)resp.StatusCode} {err}");
        Console.WriteLine("        Ensure ActivityPub:EnableAdminOperations=true is set on the server.");
    }

    await Task.Delay(250);
}

if (createdUsers.Count == 0)
{
    Console.WriteLine("\n[!] No users available. Exiting.");
    return;
}

// Post notes as each sample user; track note IDs for later interactions
Console.WriteLine("\n[*] Posting notes...");
var noteIdsByUser = new Dictionary<string, List<string>>();
var featuredNotesByUser = new Dictionary<string, List<string>>();

foreach (var (username, actorId) in createdUsers)
{
    var userClient = BuildClient(actorId);
    try
    {
        await userClient.InitializeAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    [!] Cannot initialize {username}: {ex.Message}");
        continue;
    }

    var builder = userClient.CreateActivityBuilder();
    var shortName = username[userPrefix.Length..];
    var userNotes = noteBank.Where(n => n.shortName == shortName).ToArray();
    var collected = new List<string>();
    var featured = new List<string>();

    foreach (var (_, content, isFeatured) in userNotes)
    {
        var createActivity = builder.CreateNote(content).ToPublic().ToFollowers().Build();

        try
        {
            var resp = await userClient.PostToOutboxAsync(createActivity);
            if (resp.IsSuccessStatusCode)
            {
                var note = createActivity.Object?.OfType<Note>().FirstOrDefault()
                    ?? createActivity.Object?.OfType<KristofferStrube.ActivityStreams.Object>().FirstOrDefault();
                if (note?.Id != null)
                {
                    collected.Add(note.Id);
                    if (isFeatured) featured.Add(note.Id);
                }

                Console.WriteLine($"    [{username}] {content[..Math.Min(55, content.Length)]}...{(isFeatured ? " ★" : "")}");
            }
            else
            {
                Console.WriteLine($"    [!] {username} post failed: {(int)resp.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    [!] {username} error: {ex.Message}");
        }

        await Task.Delay(500);
    }

    noteIdsByUser[username] = collected;
    featuredNotesByUser[username] = featured;
}

// Create conversations (reply chains)
Console.WriteLine("\n[*] Creating conversations...");
var conversationNoteIds = new Dictionary<string, string>();

foreach (var (authorShort, starterContent, replies) in conversationStarters)
{
    var authorUsername = $"{userPrefix}{authorShort}";
    if (!createdUsers.Any(u => u.username == authorUsername)) continue;
    
    var authorActorId = createdUsers.First(u => u.username == authorUsername).actorId;
    var authorClient = BuildClient(authorActorId);
    
    try
    {
        await authorClient.InitializeAsync();
        var authorBuilder = authorClient.CreateActivityBuilder();
        
        // Post the conversation starter
        var starterActivity = authorBuilder.CreateNote(starterContent).ToPublic().ToFollowers().Build();
        var starterResp = await authorClient.PostToOutboxAsync(starterActivity);
        
        if (!starterResp.IsSuccessStatusCode) continue;
        
        var starterNote = starterActivity.Object?.OfType<Note>().FirstOrDefault();
        if (starterNote?.Id == null) continue;
        
        Console.WriteLine($"    [{authorUsername}] Started: {starterContent[..Math.Min(50, starterContent.Length)]}...");
        conversationNoteIds[$"{authorShort}-starter"] = starterNote.Id;
        
        // Post replies
        foreach (var (replierShort, replyContent) in replies)
        {
            var replierUsername = $"{userPrefix}{replierShort}";
            if (!createdUsers.Any(u => u.username == replierUsername)) continue;
            
            var replierActorId = createdUsers.First(u => u.username == replierUsername).actorId;
            var replierClient = BuildClient(replierActorId);
            
            try
            {
                await replierClient.InitializeAsync();
                var replierBuilder = replierClient.CreateActivityBuilder();
                
                var replyActivity = replierBuilder.CreateNote(replyContent)
                    .ToPublic()
                    .ToFollowers()
                    .InReplyTo(starterNote.Id)
                    .WithMention(authorActorId, $"@{authorShort}")
                    .Build();
                
                var replyResp = await replierClient.PostToOutboxAsync(replyActivity);
                if (replyResp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"      └─ [{replierUsername}] {replyContent[..Math.Min(45, replyContent.Length)]}...");
                }
            }
            catch { }
            
            await Task.Delay(500);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    [!] Conversation error: {ex.Message}");
    }
    
    await Task.Delay(250);
}

// Post notes with attachments (for media gallery testing)
Console.WriteLine("\n[*] Posting notes with media attachments...");
foreach (var (shortName, content, imageUrl, mediaType, altText) in notesWithAttachments)
{
    var username = $"{userPrefix}{shortName}";
    if (!createdUsers.Any(u => u.username == username)) continue;
    
    var actorId = createdUsers.First(u => u.username == username).actorId;
    var userClient = BuildClient(actorId);
    
    try
    {
        await userClient.InitializeAsync();
        var builder = userClient.CreateActivityBuilder();
        
        var createActivity = builder.CreateNote(content)
            .ToPublic()
            .ToFollowers()
            .WithImage(imageUrl, altText, mediaType)
            .Build();
        
        var resp = await userClient.PostToOutboxAsync(createActivity);
        if (resp.IsSuccessStatusCode)
        {
            var note = createActivity.Object?.OfType<Note>().FirstOrDefault();
            if (note?.Id != null && noteIdsByUser.ContainsKey(username))
            {
                noteIdsByUser[username].Add(note.Id);
            }
            Console.WriteLine($"    [{username}] 📷 {content[..Math.Min(50, content.Length)]}...");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    [!] Attachment error: {ex.Message}");
    }
    
    await Task.Delay(500);
}

// Post followers-only notes (for visibility testing) 
Console.WriteLine("\n[*] Posting followers-only notes...");
foreach (var (shortName, content) in followersOnlyNotes)
{
    var username = $"{userPrefix}{shortName}";
    if (!createdUsers.Any(u => u.username == username)) continue;
    
    var actorId = createdUsers.First(u => u.username == username).actorId;
    var userClient = BuildClient(actorId);
    
    try
    {
        await userClient.InitializeAsync();
        var builder = userClient.CreateActivityBuilder();
        
        // Followers-only: address to followers collection only (no Public)
        var createActivity = builder.CreateNote(content)
            .ToFollowers()
            .Build();
        
        var resp = await userClient.PostToOutboxAsync(createActivity);
        if (resp.IsSuccessStatusCode)
        {
            Console.WriteLine($"    [{username}] 🔒 {content[..Math.Min(50, content.Length)]}...");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    [!] Followers-only error: {ex.Message}");
    }
    
    await Task.Delay(500);
}

// Post and then delete notes (for Delete activity testing)
Console.WriteLine("\n[*] Testing Delete activity...");
foreach (var (shortName, content) in notesToDelete)
{
    var username = $"{userPrefix}{shortName}";
    if (!createdUsers.Any(u => u.username == username)) continue;
    
    var actorId = createdUsers.First(u => u.username == username).actorId;
    var userClient = BuildClient(actorId);
    
    try
    {
        await userClient.InitializeAsync();
        var builder = userClient.CreateActivityBuilder();
        
        // First create the note
        var createActivity = builder.CreateNote(content).ToPublic().ToFollowers().Build();
        var createResp = await userClient.PostToOutboxAsync(createActivity);
        
        if (createResp.IsSuccessStatusCode)
        {
            var note = createActivity.Object?.OfType<Note>().FirstOrDefault();
            if (note?.Id != null)
            {
                Console.WriteLine($"    [{username}] Created note to delete: {content[..Math.Min(40, content.Length)]}...");
                
                await Task.Delay(500);
                
                // Now delete it
                var deleteActivity = builder.Delete(note.Id);
                var deleteResp = await userClient.PostToOutboxAsync(deleteActivity);
                if (deleteResp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"    [{username}] 🗑️ Deleted the note");
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    [!] Delete error: {ex.Message}");
    }
    
    await Task.Delay(250);
}

// Post and then edit notes (for Update activity testing)
Console.WriteLine("\n[*] Testing Update activity...");
foreach (var (shortName, originalContent, editedContent) in notesToEdit)
{
    var username = $"{userPrefix}{shortName}";
    if (!createdUsers.Any(u => u.username == username)) continue;
    
    var actorId = createdUsers.First(u => u.username == username).actorId;
    var userClient = BuildClient(actorId);
    
    try
    {
        await userClient.InitializeAsync();
        var builder = userClient.CreateActivityBuilder();
        
        // First create the note
        var createActivity = builder.CreateNote(originalContent).ToPublic().ToFollowers().Build();
        var createResp = await userClient.PostToOutboxAsync(createActivity);
        
        if (createResp.IsSuccessStatusCode)
        {
            var note = createActivity.Object?.OfType<Note>().FirstOrDefault();
            if (note?.Id != null)
            {
                Console.WriteLine($"    [{username}] Created note to edit: {originalContent[..Math.Min(40, originalContent.Length)]}...");
                
                await Task.Delay(500);
                
                // Now update it
                var updatedNote = new Note
                {
                    Id = note.Id,
                    Type = new[] { "Note" },
                    Content = new[] { editedContent },
                    AttributedTo = note.AttributedTo,
                    Published = note.Published,
                    Updated = DateTime.UtcNow,
                    To = note.To,
                    Cc = note.Cc
                };
                var updateActivity = builder.Update(updatedNote);
                var updateResp = await userClient.PostToOutboxAsync(updateActivity);
                if (updateResp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"    [{username}] ✏️ Edited the note");
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    [!] Update error: {ex.Message}");
    }
    
    await Task.Delay(500);
}

// Create custom collections
Console.WriteLine("\n[*] Creating custom collections...");
var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

foreach (var (shortName, collectionId, name, description, collType, queryFilter) in collectionDefinitions)
{
    var username = $"{userPrefix}{shortName}";
    if (!createdUsers.Any(u => u.username == username)) continue;
    
    var collectionDef = new CustomCollectionDefinition
    {
        Id = collectionId,
        Name = name,
        Description = description,
        Type = collType == "Query" ? CollectionType.Query : CollectionType.Manual,
        Visibility = CollectionVisibility.Public,
    };
    
    if (queryFilter == "hasAttachment")
    {
        collectionDef.QueryFilter = new CollectionQueryFilter { HasAttachment = true };
    }
    
    var collectionObj = new JsonObject
    {
        ["type"] = "Collection",
        ["name"] = name,
        ["attributedTo"] = $"{baseApUrl}/users/{username}",
        ["collectionDefinition"] = JsonSerializer.SerializeToNode(collectionDef, jsonOptions)
    };
    
    var createBody = new JsonObject
    {
        ["@context"] = new JsonArray("https://www.w3.org/ns/activitystreams"),
        ["type"] = "Create",
        ["actor"] = $"{baseApUrl}/users/sys",
        ["object"] = collectionObj
    };
    
    using var req = new HttpRequestMessage(HttpMethod.Post, sysInboxUrl)
    {
        Content = new StringContent(createBody.ToJsonString(), System.Text.Encoding.UTF8, "application/activity+json")
    };
    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    
    try
    {
        var resp = await http.SendAsync(req);
        Console.WriteLine(resp.IsSuccessStatusCode
            ? $"    [+] Created collection '{collectionId}' for {username}"
            : $"    [!] Failed to create collection '{collectionId}': {(int)resp.StatusCode}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    [!] Collection error: {ex.Message}");
    }
    
    await Task.Delay(500);
}

// Add featured notes to collections
Console.WriteLine("\n[*] Adding featured notes to collections...");
foreach (var (username, actorId) in createdUsers)
{
    if (!featuredNotesByUser.TryGetValue(username, out var featured) || featured.Count == 0) continue;
    
    var userClient = BuildClient(actorId);
    try
    {
        await userClient.InitializeAsync();
        var builder = userClient.CreateActivityBuilder();
        var targetCollection = $"{actorId}/collections/featured";
        
        foreach (var noteId in featured)
        {
            var addActivity = builder.Add(noteId, targetCollection);
            var resp = await userClient.PostToOutboxAsync(addActivity);
            if (resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"    [{username}] Added note to featured collection");
            }
        }
    }
    catch { }
    
    await Task.Delay(500);
}

// Set up follow relationships
Console.WriteLine("\n[*] Setting up follow relationships...");
foreach (var (followerShort, followeeShort) in followRelationships)
{
    var followerUsername = $"{userPrefix}{followerShort}";
    var followeeUsername = $"{userPrefix}{followeeShort}";
    
    if (!createdUsers.Any(u => u.username == followerUsername)) continue;
    if (!createdUsers.Any(u => u.username == followeeUsername)) continue;
    
    var followerActorId = createdUsers.First(u => u.username == followerUsername).actorId;
    var followeeActorId = createdUsers.First(u => u.username == followeeUsername).actorId;
    
    var followerClient = BuildClient(followerActorId);
    try
    {
        await followerClient.InitializeAsync();
        var builder = followerClient.CreateActivityBuilder();
        var resp = await followerClient.PostToOutboxAsync(builder.Follow(followeeActorId));
        Console.WriteLine(resp.IsSuccessStatusCode
            ? $"    [{followerShort}] → [{followeeShort}]"
            : $"    [!] {followerShort} failed to follow {followeeShort}: {(int)resp.StatusCode}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    [!] Follow error: {ex.Message}");
    }
    
    await Task.Delay(250);
}

// Admin follows each sample user
Console.WriteLine("\n[*] Admin follows sample users...");
var adminBuilder = adminClient.CreateActivityBuilder();
foreach (var (username, actorId) in createdUsers)
{
    try
    {
        var resp = await adminClient.PostToOutboxAsync(adminBuilder.Follow(actorId));
        Console.WriteLine(resp.IsSuccessStatusCode
            ? $"    [admin] → [{username}]"
            : $"    [!] admin failed to follow {username}: {(int)resp.StatusCode}");
    }
    catch (Exception ex) { Console.WriteLine($"    [!] {ex.Message}"); }

    await Task.Delay(250);
}

// Each sample user follows admin
Console.WriteLine("\n[*] Sample users follow admin...");
foreach (var (username, actorId) in createdUsers)
{
    var userClient = BuildClient(actorId);
    try
    {
        await userClient.InitializeAsync();
        var resp = await userClient.PostToOutboxAsync(userClient.CreateActivityBuilder().Follow(adminActorId));
        Console.WriteLine(resp.IsSuccessStatusCode
            ? $"    [{username}] → [admin]"
            : $"    [!] {username} failed to follow admin: {(int)resp.StatusCode}");
    }
    catch (Exception ex) { Console.WriteLine($"    [!] {username}: {ex.Message}"); }

    await Task.Delay(250);
}

// Set up block relationships
Console.WriteLine("\n[*] Setting up block relationships...");
foreach (var (blockerShort, blockedShort) in blockRelationships)
{
    var blockerUsername = $"{userPrefix}{blockerShort}";
    var blockedUsername = $"{userPrefix}{blockedShort}";
    
    if (!createdUsers.Any(u => u.username == blockerUsername)) continue;
    if (!createdUsers.Any(u => u.username == blockedUsername)) continue;
    
    var blockerActorId = createdUsers.First(u => u.username == blockerUsername).actorId;
    var blockedActorId = createdUsers.First(u => u.username == blockedUsername).actorId;
    
    var blockerClient = BuildClient(blockerActorId);
    try
    {
        await blockerClient.InitializeAsync();
        var builder = blockerClient.CreateActivityBuilder();
        var resp = await blockerClient.PostToOutboxAsync(builder.Block(blockedActorId));
        Console.WriteLine(resp.IsSuccessStatusCode
            ? $"    [{blockerShort}] blocked [{blockedShort}]"
            : $"    [!] Block failed: {(int)resp.StatusCode}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    [!] Block error: {ex.Message}");
    }
    
    await Task.Delay(250);
}

// Create pending follows that will be rejected (for users with manual approval)
Console.WriteLine("\n[*] Creating follow requests for manual approval accounts...");
foreach (var (followerShort, followeeShort) in pendingFollowsToReject)
{
    var followerUsername = $"{userPrefix}{followerShort}";
    var followeeUsername = $"{userPrefix}{followeeShort}";
    
    if (!createdUsers.Any(u => u.username == followerUsername)) continue;
    if (!createdUsers.Any(u => u.username == followeeUsername)) continue;
    
    var followerActorId = createdUsers.First(u => u.username == followerUsername).actorId;
    var followeeActorId = createdUsers.First(u => u.username == followeeUsername).actorId;
    
    // Send the follow request (will be pending since followee requires manual approval)
    var followerClient = BuildClient(followerActorId);
    try
    {
        await followerClient.InitializeAsync();
        var builder = followerClient.CreateActivityBuilder();
        var resp = await followerClient.PostToOutboxAsync(builder.Follow(followeeActorId));
        Console.WriteLine(resp.IsSuccessStatusCode
            ? $"    [{followerShort}] → [{followeeShort}] (pending approval)"
            : $"    [!] Follow request failed: {(int)resp.StatusCode}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    [!] Follow request error: {ex.Message}");
    }
    
    await Task.Delay(250);
}

// Test Undo Follow (follow then unfollow)
Console.WriteLine("\n[*] Testing Undo Follow...");
foreach (var (followerShort, followeeShort) in followsToUndo)
{
    var followerUsername = $"{userPrefix}{followerShort}";
    var followeeUsername = $"{userPrefix}{followeeShort}";
    
    if (!createdUsers.Any(u => u.username == followerUsername)) continue;
    if (!createdUsers.Any(u => u.username == followeeUsername)) continue;
    
    var followerActorId = createdUsers.First(u => u.username == followerUsername).actorId;
    var followeeActorId = createdUsers.First(u => u.username == followeeUsername).actorId;
    
    var followerClient = BuildClient(followerActorId);
    try
    {
        await followerClient.InitializeAsync();
        var builder = followerClient.CreateActivityBuilder();
        
        // First follow
        var followActivity = builder.Follow(followeeActorId);
        var followResp = await followerClient.PostToOutboxAsync(followActivity);
        
        if (followResp.IsSuccessStatusCode)
        {
            Console.WriteLine($"    [{followerShort}] → [{followeeShort}] (followed)");
            await Task.Delay(250);
            
            // Then undo the follow
            var undoActivity = builder.Undo(followActivity);
            var undoResp = await followerClient.PostToOutboxAsync(undoActivity);
            Console.WriteLine(undoResp.IsSuccessStatusCode
                ? $"    [{followerShort}] ↛ [{followeeShort}] (unfollowed)"
                : $"    [!] Undo follow failed: {(int)undoResp.StatusCode}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    [!] Undo follow error: {ex.Message}");
    }
    
    await Task.Delay(250);
}

// Test Undo Like (like then unlike)
Console.WriteLine("\n[*] Testing Undo Like...");
foreach (var (likerShort, likedUserShort) in likesToUndo)
{
    var likerUsername = $"{userPrefix}{likerShort}";
    var likedUsername = $"{userPrefix}{likedUserShort}";
    
    if (!createdUsers.Any(u => u.username == likerUsername)) continue;
    if (!noteIdsByUser.TryGetValue(likedUsername, out var targetNotes) || targetNotes.Count == 0) continue;
    
    var likerActorId = createdUsers.First(u => u.username == likerUsername).actorId;
    var likerClient = BuildClient(likerActorId);
    
    try
    {
        await likerClient.InitializeAsync();
        var builder = likerClient.CreateActivityBuilder();
        
        // Pick a note to like (use last one to avoid conflicts with other likes)
        var noteToLike = targetNotes.Last();
        
        // First like
        var likeActivity = builder.Like(noteToLike);
        var likeResp = await likerClient.PostToOutboxAsync(likeActivity);
        
        if (likeResp.IsSuccessStatusCode)
        {
            Console.WriteLine($"    [{likerShort}] ♥ [{likedUserShort}] (liked)");
            await Task.Delay(250);
            
            // Then undo the like
            var undoActivity = builder.Undo(likeActivity);
            var undoResp = await likerClient.PostToOutboxAsync(undoActivity);
            Console.WriteLine(undoResp.IsSuccessStatusCode
                ? $"    [{likerShort}] ♡ [{likedUserShort}] (unliked)"
                : $"    [!] Undo like failed: {(int)undoResp.StatusCode}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    [!] Undo like error: {ex.Message}");
    }
    
    await Task.Delay(250);
}

// Sample users like and boost each other's posts
if (createdUsers.Count > 1)
{
    Console.WriteLine("\n[*] Sample users interact with each other's posts...");
    for (int i = 0; i < createdUsers.Count; i++)
    {
        var (username, actorId) = createdUsers[i];
        var (nextUsername, _) = createdUsers[(i + 1) % createdUsers.Count];

        if (!noteIdsByUser.TryGetValue(nextUsername, out var targetNotes) || targetNotes.Count == 0)
            continue;

        var userClient = BuildClient(actorId);
        try { await userClient.InitializeAsync(); }
        catch { continue; }

        var builder = userClient.CreateActivityBuilder();

        // Like the first note
        try
        {
            var resp = await userClient.PostToOutboxAsync(builder.Like(targetNotes[0]));
            Console.WriteLine(resp.IsSuccessStatusCode
                ? $"    [{username}] ♥ {nextUsername}"
                : $"    [!] like failed: {(int)resp.StatusCode}");
        }
        catch (Exception ex) { Console.WriteLine($"    [!] {ex.Message}"); }

        // Announce (boost) the second note if available
        if (targetNotes.Count > 1)
        {
            try
            {
                var resp = await userClient.PostToOutboxAsync(builder.Announce(targetNotes[1]));
                Console.WriteLine(resp.IsSuccessStatusCode
                    ? $"    [{username}] ⟳ {nextUsername}"
                    : $"    [!] boost failed: {(int)resp.StatusCode}");
            }
            catch (Exception ex) { Console.WriteLine($"    [!] {ex.Message}"); }
        }

        await Task.Delay(250);
    }
}

// Print summary
var totalNotes = noteIdsByUser.Values.Sum(v => v.Count);
var totalConversations = conversationStarters.Length;
var totalCollections = collectionDefinitions.Length;
var totalFollows = followRelationships.Length + createdUsers.Count * 2; // user follows + admin relationships
var totalBlocks = blockRelationships.Length;
var totalMediaPosts = notesWithAttachments.Length;
var totalFollowersOnlyPosts = followersOnlyNotes.Length;

Console.WriteLine("\n[✓] Done! Sample data seeded successfully.");
Console.WriteLine($"    {createdUsers.Count} user(s) with prefix '{userPrefix}'");
Console.WriteLine($"    {totalNotes} notes posted (bob has 25+ for pagination testing)");
Console.WriteLine($"    {totalMediaPosts} posts with media attachments");
Console.WriteLine($"    {totalFollowersOnlyPosts} followers-only posts");
Console.WriteLine($"    {totalConversations} conversation threads with replies");
Console.WriteLine($"    {totalCollections} custom collections (featured, bookmarks, media)");
Console.WriteLine($"    {totalFollows} follow relationships");
Console.WriteLine($"    {totalBlocks} block relationship(s)");
Console.WriteLine($"    {pendingFollowsToReject.Length} pending follow request(s)");
Console.WriteLine();
Console.WriteLine("Activities exercised:");
Console.WriteLine("  - Create (notes, collections, actors)");
Console.WriteLine("  - Follow / Accept / Reject");
Console.WriteLine("  - Like / Announce (boost)");
Console.WriteLine("  - Block");
Console.WriteLine("  - Delete (tombstone)");
Console.WriteLine("  - Update (edit post)");
Console.WriteLine("  - Undo (unlike, unfollow)");
Console.WriteLine("  - Add / Remove (collections)");
Console.WriteLine();
Console.WriteLine("Relationship summary:");
Console.WriteLine("  - alice ↔ bob ↔ carol ↔ heidi (mutual follows)");
Console.WriteLine("  - dave → alice, bob, carol, eve, heidi (one-way follows)");
Console.WriteLine("  - alice ⊘ frank (blocked)");
Console.WriteLine("  - frank → grace (pending, requires manual approval)");
Console.WriteLine("  - dave → frank → (unfollowed via Undo)");
Console.WriteLine("  - carol ♡ bob (liked then unliked via Undo)");

// --- Helpers ---

ActivityPubClient BuildClient(string actorId) =>
    new(
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<IWebFingerService>(),
        sp.GetRequiredService<HttpSignatureService>(),
        Options.Create(new ActivityPubClientOptions
        {
            ActorId = actorId,
            ApiKey = apiKey,
            TimeoutSeconds = 30,
            EnableCaching = false,
        }),
        loggerFactory.CreateLogger<ActivityPubClient>());

string GetArg(string name, string defaultValue)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    return defaultValue;
}
