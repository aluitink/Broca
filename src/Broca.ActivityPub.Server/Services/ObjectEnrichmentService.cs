using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using KristofferStrube.ActivityStreams.JsonLD;
using Microsoft.Extensions.Options;

namespace Broca.ActivityPub.Server.Services;

/// <summary>
/// Service for enriching ActivityPub objects with collection metadata (replies, likes, shares)
/// This allows clients to see counts without dereferencing each collection
/// </summary>
public class ObjectEnrichmentService
{
    private readonly IActivityRepository _activityRepository;
    private readonly ActivityPubServerOptions _options;
    private readonly ILogger<ObjectEnrichmentService> _logger;

    public ObjectEnrichmentService(
        IActivityRepository activityRepository,
        IOptions<ActivityPubServerOptions> options,
        ILogger<ObjectEnrichmentService> logger)
    {
        _activityRepository = activityRepository;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Enriches an object with collection information (replies, likes, shares)
    /// Populates the Replies, Likes, and Shares properties with collection references and counts
    /// </summary>
    /// <param name="obj">The object to enrich</param>
    /// <param name="baseUrl">The base URL for constructing collection URLs</param>
    public async Task EnrichObjectAsync(KristofferStrube.ActivityStreams.Object obj, string baseUrl)
    {
        if (obj == null || string.IsNullOrEmpty(obj.Id))
        {
            return;
        }

        try
        {
            // Extract username from object ID if it's a local object
            // Format: {baseUrl}/users/{username}/objects/{objectId}
            if (!obj.Id.StartsWith(baseUrl))
            {
                // Skip enrichment for remote objects
                return;
            }

            // Parse the object ID to extract username and objectId
            var idParts = obj.Id.Replace(baseUrl, "").Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (idParts.Length < 4 || idParts[0] != "users" || idParts[2] != "objects")
            {
                _logger.LogDebug("Object ID {ObjectId} doesn't match expected format", obj.Id);
                return;
            }

            var username = idParts[1];
            var objectId = idParts[3];

            // Get counts for the collections
            var repliesCount = await _activityRepository.GetRepliesCountAsync(obj.Id);
            var likesCount = await _activityRepository.GetLikesCountAsync(obj.Id);
            var sharesCount = await _activityRepository.GetSharesCountAsync(obj.Id);

            // Build collection URLs
            var repliesUrl = $"{baseUrl}/users/{username}/objects/{objectId}/replies";
            var likesUrl = $"{baseUrl}/users/{username}/objects/{objectId}/likes";
            var sharesUrl = $"{baseUrl}/users/{username}/objects/{objectId}/shares";

            // Populate Replies collection
            if (repliesCount > 0 || obj.Replies == null)
            {
                obj.Replies = new Collection
                {
                    JsonLDContext = new List<ITermDefinition>
                    {
                        new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
                    },
                    Id = repliesUrl,
                    Type = new[] { "Collection" },
                    TotalItems = (uint)repliesCount
                };
            }

            // Populate Likes collection
            if (likesCount > 0 || obj.Likes == null)
            {
                obj.Likes = new Collection
                {
                    JsonLDContext = new List<ITermDefinition>
                    {
                        new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
                    },
                    Id = likesUrl,
                    Type = new[] { "Collection" },
                    TotalItems = (uint)likesCount
                };
            }

            // Populate Shares collection
            if (sharesCount > 0 || obj.Shares == null)
            {
                obj.Shares = new Collection
                {
                    JsonLDContext = new List<ITermDefinition>
                    {
                        new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams"))
                    },
                    Id = sharesUrl,
                    Type = new[] { "Collection" },
                    TotalItems = (uint)sharesCount
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enrich object {ObjectId} with collection metadata", obj.Id);
            // Don't throw - enrichment is optional and shouldn't break the request
        }
    }

    /// <summary>
    /// Enriches an activity by enriching its embedded object
    /// </summary>
    /// <param name="activity">The activity to enrich</param>
    /// <param name="baseUrl">The base URL for constructing collection URLs</param>
    public async Task EnrichActivityAsync(IObjectOrLink activity, string baseUrl)
    {
        if (activity == null)
        {
            return;
        }

        // If the activity is an object itself, enrich it
        if (activity is KristofferStrube.ActivityStreams.Object activityObj && !string.IsNullOrEmpty(activityObj.Id))
        {
            await EnrichObjectAsync(activityObj, baseUrl);
        }

        // Also check if the activity is an Activity with an Object property (like Create, Update activities)
        if (activity is Activity activityWithObject && activityWithObject.Object?.Any() == true)
        {
            foreach (var obj in activityWithObject.Object)
            {
                if (obj is KristofferStrube.ActivityStreams.Object embeddedObj)
                {
                    await EnrichObjectAsync(embeddedObj, baseUrl);
                }
            }
        }
    }

    /// <summary>
    /// Enriches multiple activities in bulk
    /// </summary>
    /// <param name="activities">The activities to enrich</param>
    /// <param name="baseUrl">The base URL for constructing collection URLs</param>
    public async Task EnrichActivitiesAsync(IEnumerable<IObjectOrLink> activities, string baseUrl)
    {
        if (activities == null)
        {
            return;
        }

        foreach (var activity in activities)
        {
            await EnrichActivityAsync(activity, baseUrl);
        }
    }
}
