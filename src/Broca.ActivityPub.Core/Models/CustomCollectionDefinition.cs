using System.Text.Json.Serialization;

namespace Broca.ActivityPub.Core.Models;

/// <summary>
/// Defines a custom collection for an actor
/// </summary>
public class CustomCollectionDefinition
{
    /// <summary>
    /// Unique identifier for this collection (e.g., "featured", "pinned")
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for the collection
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the collection's purpose
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Type of collection
    /// </summary>
    [JsonPropertyName("type")]
    public CollectionType Type { get; set; }

    /// <summary>
    /// Visibility level of the collection
    /// </summary>
    [JsonPropertyName("visibility")]
    public CollectionVisibility Visibility { get; set; }

    /// <summary>
    /// Query filter for dynamic collections (only used when Type is Query)
    /// </summary>
    [JsonPropertyName("queryFilter")]
    public CollectionQueryFilter? QueryFilter { get; set; }

    /// <summary>
    /// Manually added items (only used when Type is Manual)
    /// </summary>
    [JsonPropertyName("items")]
    public List<string> Items { get; set; } = new();

    /// <summary>
    /// When this collection was created
    /// </summary>
    [JsonPropertyName("created")]
    public DateTimeOffset Created { get; set; }

    /// <summary>
    /// When this collection was last updated
    /// </summary>
    [JsonPropertyName("updated")]
    public DateTimeOffset Updated { get; set; }

    /// <summary>
    /// Maximum number of items to include in the collection (optional)
    /// </summary>
    [JsonPropertyName("maxItems")]
    public int? MaxItems { get; set; }

    /// <summary>
    /// Sort order for items in the collection
    /// </summary>
    [JsonPropertyName("sortOrder")]
    public CollectionSortOrder SortOrder { get; set; } = CollectionSortOrder.Chronological;
}

/// <summary>
/// Type of collection
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CollectionType
{
    /// <summary>
    /// Manual collection - items are explicitly added/removed by user
    /// </summary>
    Manual,

    /// <summary>
    /// Query collection - items are dynamically selected based on filters
    /// </summary>
    Query
}

/// <summary>
/// Visibility level of a collection
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CollectionVisibility
{
    /// <summary>
    /// Public - visible to everyone, advertised in actor profile
    /// </summary>
    Public,

    /// <summary>
    /// Private - only visible when authenticated with actor's key
    /// </summary>
    Private,

    /// <summary>
    /// Unlisted - not advertised but accessible if URL is known
    /// </summary>
    Unlisted
}

/// <summary>
/// Sort order for collection items
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CollectionSortOrder
{
    /// <summary>
    /// Newest items first
    /// </summary>
    Chronological,

    /// <summary>
    /// Oldest items first
    /// </summary>
    ReverseChronological,

    /// <summary>
    /// Manual ordering (for manual collections)
    /// </summary>
    Manual
}

/// <summary>
/// Filter for query-based collections
/// </summary>
public class CollectionQueryFilter
{
    /// <summary>
    /// Filter by activity type (e.g., "Note", "Article")
    /// </summary>
    [JsonPropertyName("activityTypes")]
    public List<string>? ActivityTypes { get; set; }

    /// <summary>
    /// Filter by object type
    /// </summary>
    [JsonPropertyName("objectTypes")]
    public List<string>? ObjectTypes { get; set; }

    /// <summary>
    /// Filter by tag/hashtag
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Include only items after this date
    /// </summary>
    [JsonPropertyName("afterDate")]
    public DateTimeOffset? AfterDate { get; set; }

    /// <summary>
    /// Include only items before this date
    /// </summary>
    [JsonPropertyName("beforeDate")]
    public DateTimeOffset? BeforeDate { get; set; }

    /// <summary>
    /// Filter by visibility (public, unlisted, private)
    /// </summary>
    [JsonPropertyName("visibility")]
    public List<string>? Visibility { get; set; }

    /// <summary>
    /// Search query for content matching
    /// </summary>
    [JsonPropertyName("searchQuery")]
    public string? SearchQuery { get; set; }

    /// <summary>
    /// Include only items with attachments
    /// </summary>
    [JsonPropertyName("hasAttachment")]
    public bool? HasAttachment { get; set; }

    /// <summary>
    /// Filter by in-reply-to status
    /// </summary>
    [JsonPropertyName("isReply")]
    public bool? IsReply { get; set; }

    /// <summary>
    /// Custom JSONPath expressions for advanced filtering
    /// </summary>
    [JsonPropertyName("customFilters")]
    public Dictionary<string, string>? CustomFilters { get; set; }
}
