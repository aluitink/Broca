using System.Text.Json;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Server.Examples;

/// <summary>
/// Examples of creating and using custom collections
/// </summary>
public static class CustomCollectionExamples
{
    /// <summary>
    /// Example: Create a "Featured Posts" manual collection
    /// </summary>
    public static Activity CreateFeaturedPostsCollection(string actorId, string username)
    {
        var collectionDefinition = new CustomCollectionDefinition
        {
            Id = "featured",
            Name = "Featured Posts",
            Description = "My hand-picked best posts",
            Type = CollectionType.Manual,
            Visibility = CollectionVisibility.Public,
            SortOrder = CollectionSortOrder.Manual,
            MaxItems = 10 // Limit to 10 featured posts
        };

        return new Create
        {
            Type = new List<string> { "Create" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(actorId) } },
            Object = new List<IObjectOrLink>
            {
                new Collection
                {
                    Type = new List<string> { "Collection" },
                    Name = new List<string> { collectionDefinition.Name },
                    AttributedTo = new List<IObjectOrLink> { new Link { Href = new Uri(actorId) } },
                    ExtensionData = new Dictionary<string, JsonElement>
                    {
                        ["collectionDefinition"] = JsonSerializer.SerializeToElement(collectionDefinition)
                    }
                }
            }
        };
    }

    /// <summary>
    /// Example: Create a "Media Gallery" query collection
    /// Automatically includes all posts with images or videos
    /// </summary>
    public static Activity CreateMediaGalleryCollection(string actorId)
    {
        var collectionDefinition = new CustomCollectionDefinition
        {
            Id = "media",
            Name = "Media Gallery",
            Description = "All my posts with images and videos",
            Type = CollectionType.Query,
            Visibility = CollectionVisibility.Public,
            SortOrder = CollectionSortOrder.Chronological,
            QueryFilter = new CollectionQueryFilter
            {
                HasAttachment = true,
                ObjectTypes = new List<string> { "Image", "Video", "Note" }
            }
        };

        return new Create
        {
            Type = new List<string> { "Create" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(actorId) } },
            Object = new List<IObjectOrLink>
            {
                new Collection
                {
                    Type = new List<string> { "Collection" },
                    Name = new List<string> { collectionDefinition.Name },
                    ExtensionData = new Dictionary<string, JsonElement>
                    {
                        ["collectionDefinition"] = JsonSerializer.SerializeToElement(collectionDefinition)
                    }
                }
            }
        };
    }

    /// <summary>
    /// Example: Create a "Photography" collection filtered by tag
    /// </summary>
    public static Activity CreatePhotographyCollection(string actorId)
    {
        var collectionDefinition = new CustomCollectionDefinition
        {
            Id = "photography",
            Name = "Photography",
            Description = "All my photography posts",
            Type = CollectionType.Query,
            Visibility = CollectionVisibility.Public,
            SortOrder = CollectionSortOrder.Chronological,
            QueryFilter = new CollectionQueryFilter
            {
                Tags = new List<string> { "photography", "photo" },
                HasAttachment = true
            },
            MaxItems = 100
        };

        return new Create
        {
            Type = new List<string> { "Create" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(actorId) } },
            Object = new List<IObjectOrLink>
            {
                new Collection
                {
                    Type = new List<string> { "Collection" },
                    Name = new List<string> { collectionDefinition.Name },
                    ExtensionData = new Dictionary<string, JsonElement>
                    {
                        ["collectionDefinition"] = JsonSerializer.SerializeToElement(collectionDefinition)
                    }
                }
            }
        };
    }

    /// <summary>
    /// Example: Create a "Recent Articles" collection
    /// Only includes articles from the last 30 days
    /// </summary>
    public static Activity CreateRecentArticlesCollection(string actorId)
    {
        var thirtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-30);
        
        var collectionDefinition = new CustomCollectionDefinition
        {
            Id = "recent-articles",
            Name = "Recent Articles",
            Description = "Articles published in the last 30 days",
            Type = CollectionType.Query,
            Visibility = CollectionVisibility.Public,
            SortOrder = CollectionSortOrder.Chronological,
            QueryFilter = new CollectionQueryFilter
            {
                ObjectTypes = new List<string> { "Article" },
                AfterDate = thirtyDaysAgo
            }
        };

        return new Create
        {
            Type = new List<string> { "Create" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(actorId) } },
            Object = new List<IObjectOrLink>
            {
                new Collection
                {
                    Type = new List<string> { "Collection" },
                    Name = new List<string> { collectionDefinition.Name },
                    ExtensionData = new Dictionary<string, JsonElement>
                    {
                        ["collectionDefinition"] = JsonSerializer.SerializeToElement(collectionDefinition)
                    }
                }
            }
        };
    }

    /// <summary>
    /// Example: Create a private "Drafts" collection
    /// </summary>
    public static Activity CreateDraftsCollection(string actorId)
    {
        var collectionDefinition = new CustomCollectionDefinition
        {
            Id = "drafts",
            Name = "Drafts",
            Description = "My draft posts",
            Type = CollectionType.Manual,
            Visibility = CollectionVisibility.Private, // Private - not advertised
            SortOrder = CollectionSortOrder.Chronological
        };

        return new Create
        {
            Type = new List<string> { "Create" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(actorId) } },
            Object = new List<IObjectOrLink>
            {
                new Collection
                {
                    Type = new List<string> { "Collection" },
                    Name = new List<string> { collectionDefinition.Name },
                    ExtensionData = new Dictionary<string, JsonElement>
                    {
                        ["collectionDefinition"] = JsonSerializer.SerializeToElement(collectionDefinition)
                    }
                }
            }
        };
    }

    /// <summary>
    /// Example: Create a "Bookmarks" collection (unlisted)
    /// </summary>
    public static Activity CreateBookmarksCollection(string actorId)
    {
        var collectionDefinition = new CustomCollectionDefinition
        {
            Id = "bookmarks",
            Name = "Bookmarks",
            Description = "Posts I've bookmarked",
            Type = CollectionType.Manual,
            Visibility = CollectionVisibility.Unlisted, // Not advertised but accessible
            SortOrder = CollectionSortOrder.Chronological
        };

        return new Create
        {
            Type = new List<string> { "Create" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(actorId) } },
            Object = new List<IObjectOrLink>
            {
                new Collection
                {
                    Type = new List<string> { "Collection" },
                    Name = new List<string> { collectionDefinition.Name },
                    ExtensionData = new Dictionary<string, JsonElement>
                    {
                        ["collectionDefinition"] = JsonSerializer.SerializeToElement(collectionDefinition)
                    }
                }
            }
        };
    }

    /// <summary>
    /// Example: Advanced query collection with multiple filters
    /// "Popular Tech Posts" - tech posts with multiple criteria
    /// </summary>
    public static Activity CreatePopularTechPostsCollection(string actorId)
    {
        var collectionDefinition = new CustomCollectionDefinition
        {
            Id = "popular-tech",
            Name = "Popular Tech Posts",
            Description = "My most engaging technology posts",
            Type = CollectionType.Query,
            Visibility = CollectionVisibility.Public,
            SortOrder = CollectionSortOrder.Chronological,
            QueryFilter = new CollectionQueryFilter
            {
                Tags = new List<string> { "tech", "technology", "programming", "software" },
                ObjectTypes = new List<string> { "Article", "Note" },
                IsReply = false, // Only top-level posts
                AfterDate = DateTimeOffset.UtcNow.AddYears(-1) // Last year
            },
            MaxItems = 25
        };

        return new Create
        {
            Type = new List<string> { "Create" },
            Actor = new List<IObjectOrLink> { new Link { Href = new Uri(actorId) } },
            Object = new List<IObjectOrLink>
            {
                new Collection
                {
                    Type = new List<string> { "Collection" },
                    Name = new List<string> { collectionDefinition.Name },
                    ExtensionData = new Dictionary<string, JsonElement>
                    {
                        ["collectionDefinition"] = JsonSerializer.SerializeToElement(collectionDefinition)
                    }
                }
            }
        };
    }
}
