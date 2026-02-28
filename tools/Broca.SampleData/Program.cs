using System.Net.Http.Headers;
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
var userCount = int.Parse(GetArg("--count", "3"));
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

// Personas and their content
var personas = new (string shortName, string displayName, string summary)[]
{
    ("alice", "Alice Example",   "Decentralized social network enthusiast. Here to explore the fediverse."),
    ("bob",   "Bob Example",     "Software developer interested in open protocols and ActivityPub."),
    ("carol", "Carol Example",   "Digital rights advocate and self-hoster."),
    ("dave",  "Dave Example",    "Tinkerer exploring federated communication."),
    ("eve",   "Eve Example",     "Researcher studying decentralized communication systems."),
};

var noteBank = new (string shortName, string content)[]
{
    ("alice", "Just arrived on the fediverse! Looking forward to connecting with everyone. #ActivityPub #NewUser"),
    ("alice", "Decentralized social networks aren't just about avoiding big tech — they're about real ownership of your own data and identity."),
    ("alice", "The ActivityPub protocol is surprisingly elegant once you understand the actor/inbox/outbox model."),
    ("bob",   "Finally got my self-hosted instance working. The setup was easier than expected! #selfhosted #fediverse"),
    ("bob",   "HTTP signatures are the backbone of federated trust in ActivityPub. Worth taking the time to understand them properly."),
    ("bob",   "Hot take: the fediverse is what the social web was always supposed to be before it got monetized."),
    ("carol", "Digital rights start with data ownership. Federated platforms get this right where centralized ones consistently fail."),
    ("carol", "The fediverse is proof that community-driven open standards can outperform venture-backed social silos."),
    ("carol", "Joined this instance to experiment with ActivityPub. Genuinely impressed so far!"),
    ("dave",  "Got my first note posted from a fresh Broca instance. Everything is working great!"),
    ("dave",  "Best thing about federated networks: if you don't like your instance, you can move. No lock-in, ever."),
    ("dave",  "Shoutout to everyone building ActivityPub tooling. The ecosystem is growing fast and it's exciting to watch."),
    ("eve",   "Studying how information propagates across federated networks versus centralized ones. Fascinating differences."),
    ("eve",   "The fediverse has a fundamentally different information flow: more local community, less algorithmic amplification."),
    ("eve",   "Running some experiments on ActivityPub federation patterns. Early results are interesting — more to share soon."),
};

// Create sample users via admin back-channel
Console.WriteLine("[*] Creating sample users...");
var createdUsers = new List<(string username, string actorId)>();
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

for (int i = 0; i < Math.Min(userCount, personas.Length); i++)
{
    var (shortName, displayName, summary) = personas[i];
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

    var body = new JsonObject
    {
        ["@context"] = new JsonArray("https://www.w3.org/ns/activitystreams"),
        ["type"] = "Create",
        ["actor"] = $"{baseApUrl}/users/sys",
        ["object"] = new JsonObject
        {
            ["type"] = "Person",
            ["preferredUsername"] = username,
            ["name"] = displayName,
            ["summary"] = summary,
        }
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

    await Task.Delay(200);
}

if (createdUsers.Count == 0)
{
    Console.WriteLine("\n[!] No users available. Exiting.");
    return;
}

// Post notes as each sample user; track note IDs for later interactions
Console.WriteLine("\n[*] Posting notes...");
var noteIdsByUser = new Dictionary<string, List<string>>();

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
    var notes = noteBank.Where(n => n.shortName == shortName).Select(n => n.content).ToArray();
    var collected = new List<string>();

    foreach (var content in notes)
    {
        var createActivity = builder.CreateNote(content).ToPublic().ToFollowers().Build();

        try
        {
            var resp = await userClient.PostToOutboxAsync(createActivity);
            if (resp.IsSuccessStatusCode)
            {
                // Extract the Note ID from the Create activity's object
                var note = createActivity.Object?.OfType<Note>().FirstOrDefault()
                    ?? createActivity.Object?.OfType<KristofferStrube.ActivityStreams.Object>().FirstOrDefault();
                if (note?.Id != null) collected.Add(note.Id);

                Console.WriteLine($"    [{username}] {content[..Math.Min(60, content.Length)]}...");
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

        await Task.Delay(100);
    }

    noteIdsByUser[username] = collected;
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
            ? $"    [admin] followed {username}"
            : $"    [!] admin failed to follow {username}: {(int)resp.StatusCode}");
    }
    catch (Exception ex) { Console.WriteLine($"    [!] {ex.Message}"); }

    await Task.Delay(100);
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
            ? $"    [{username}] followed admin"
            : $"    [!] {username} failed to follow admin: {(int)resp.StatusCode}");
    }
    catch (Exception ex) { Console.WriteLine($"    [!] {username}: {ex.Message}"); }

    await Task.Delay(100);
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
                ? $"    [{username}] liked a post by {nextUsername}"
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
                    ? $"    [{username}] boosted a post by {nextUsername}"
                    : $"    [!] boost failed: {(int)resp.StatusCode}");
            }
            catch (Exception ex) { Console.WriteLine($"    [!] {ex.Message}"); }
        }

        await Task.Delay(100);
    }
}

Console.WriteLine("\n[✓] Done! Sample data seeded successfully.");
Console.WriteLine($"    {createdUsers.Count} user(s) ready with prefix '{userPrefix}'");
Console.WriteLine($"    Total posts: {noteIdsByUser.Values.Sum(v => v.Count)}");

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
