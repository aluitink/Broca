using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Server.Services.CollectionSearch;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Logging.Abstractions;

namespace Broca.ActivityPub.UnitTests.CollectionSearch;

public class DefaultCollectionSearchEngineTests
{
    private readonly DefaultCollectionSearchEngine _engine = new(NullLogger<DefaultCollectionSearchEngine>.Instance);

    private static List<IObjectOrLink> CreateTestItems()
    {
        return new List<IObjectOrLink>
        {
            new Note
            {
                Id = "https://example.com/notes/1",
                Type = new[] { "Note" },
                Content = new[] { "Hello world, this is a test note" },
                Name = new[] { "First Note" },
                Published = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Note
            {
                Id = "https://example.com/notes/2",
                Type = new[] { "Note" },
                Content = new[] { "Another note about cats" },
                Name = new[] { "Cat Note" },
                Published = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Article
            {
                Id = "https://example.com/articles/1",
                Type = new[] { "Article" },
                Content = new[] { "A long article about programming" },
                Name = new[] { "Programming Guide" },
                Summary = new[] { "Learn to code" },
                Published = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Note
            {
                Id = "https://example.com/notes/3",
                Type = new[] { "Note" },
                Content = new[] { "Reply to someone" },
                Name = new[] { "Reply Note" },
                InReplyTo = new IObjectOrLink[] { new Link { Href = new Uri("https://other.com/notes/99") } },
                Published = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        };
    }

    [Fact]
    public void Apply_FilterByType_ReturnsMatchingItems()
    {
        var items = CreateTestItems();
        var search = new CollectionSearchParameters { Filter = "type eq 'Note'" };

        var (result, count) = _engine.Apply(items, search);

        Assert.Equal(3, count);
        Assert.All(result, item => Assert.Contains("Note", (item as IObject)!.Type!));
    }

    [Fact]
    public void Apply_FilterByTypeArticle_ReturnsOnlyArticles()
    {
        var items = CreateTestItems();
        var search = new CollectionSearchParameters { Filter = "type eq 'Article'" };

        var (result, count) = _engine.Apply(items, search);

        Assert.Equal(1, count);
        Assert.Contains("Article", (result.Single() as IObject)!.Type!);
    }

    [Fact]
    public void Apply_SearchByContent_ReturnsMatching()
    {
        var items = CreateTestItems();
        var search = new CollectionSearchParameters { Search = "cats" };

        var (result, count) = _engine.Apply(items, search);

        Assert.Equal(1, count);
        Assert.Equal("https://example.com/notes/2", (result.Single() as IObject)!.Id);
    }

    [Fact]
    public void Apply_SearchByName_ReturnsMatching()
    {
        var items = CreateTestItems();
        var search = new CollectionSearchParameters { Search = "programming" };

        var (result, count) = _engine.Apply(items, search);

        Assert.Equal(1, count);
        Assert.Equal("https://example.com/articles/1", (result.Single() as IObject)!.Id);
    }

    [Fact]
    public void Apply_SearchBySummary_ReturnsMatching()
    {
        var items = CreateTestItems();
        var search = new CollectionSearchParameters { Search = "learn to code" };

        var (result, count) = _engine.Apply(items, search);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Apply_ContainsFilter_ReturnsMatching()
    {
        var items = CreateTestItems();
        var search = new CollectionSearchParameters { Filter = "contains(content, 'test')" };

        var (result, count) = _engine.Apply(items, search);

        Assert.Equal(1, count);
        Assert.Equal("https://example.com/notes/1", (result.Single() as IObject)!.Id);
    }

    [Fact]
    public void Apply_StartsWithFilter_ReturnsMatching()
    {
        var items = CreateTestItems();
        var search = new CollectionSearchParameters { Filter = "startswith(name, 'Cat')" };

        var (result, count) = _engine.Apply(items, search);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Apply_IsReplyFilter_ReturnsReplies()
    {
        var items = CreateTestItems();
        var search = new CollectionSearchParameters { Filter = "isReply eq true" };

        var (result, count) = _engine.Apply(items, search);

        Assert.Equal(1, count);
        Assert.Equal("https://example.com/notes/3", (result.Single() as IObject)!.Id);
    }

    [Fact]
    public void Apply_NotReply_ExcludesReplies()
    {
        var items = CreateTestItems();
        var search = new CollectionSearchParameters { Filter = "isReply eq false" };

        var (result, count) = _engine.Apply(items, search);

        Assert.Equal(3, count);
    }

    [Fact]
    public void Apply_DateFilter_ReturnsItemsAfterDate()
    {
        var items = CreateTestItems();
        var search = new CollectionSearchParameters { Filter = "published ge '2025-08-01T00:00:00+00:00'" };

        var (result, count) = _engine.Apply(items, search);

        Assert.Equal(2, count);
    }

    [Fact]
    public void Apply_CombinedFilterAndSearch_BothApplied()
    {
        var items = CreateTestItems();
        var search = new CollectionSearchParameters
        {
            Filter = "type eq 'Note'",
            Search = "hello"
        };

        var (result, count) = _engine.Apply(items, search);

        Assert.Equal(1, count);
        Assert.Equal("https://example.com/notes/1", (result.Single() as IObject)!.Id);
    }

    [Fact]
    public void Apply_OrderByPublishedAsc_SortedCorrectly()
    {
        var items = CreateTestItems();
        var search = new CollectionSearchParameters { OrderBy = "published asc" };

        var (result, count) = _engine.Apply(items, search);

        var ids = result.Select(i => (i as IObject)!.Id).ToList();
        Assert.Equal("https://example.com/notes/1", ids[0]);
        Assert.Equal("https://example.com/notes/2", ids[1]);
        Assert.Equal("https://example.com/articles/1", ids[2]);
        Assert.Equal("https://example.com/notes/3", ids[3]);
    }

    [Fact]
    public void Apply_OrderByPublishedDesc_SortedCorrectly()
    {
        var items = CreateTestItems();
        var search = new CollectionSearchParameters { OrderBy = "published desc" };

        var (result, count) = _engine.Apply(items, search);

        var ids = result.Select(i => (i as IObject)!.Id).ToList();
        Assert.Equal("https://example.com/notes/3", ids[0]);
        Assert.Equal("https://example.com/articles/1", ids[1]);
    }

    [Fact]
    public void Apply_OrFilter_ReturnsEitherMatch()
    {
        var items = CreateTestItems();
        var search = new CollectionSearchParameters
        {
            Filter = "type eq 'Article' or contains(content, 'cats')"
        };

        var (result, count) = _engine.Apply(items, search);

        Assert.Equal(2, count);
    }

    [Fact]
    public void Apply_AndFilter_RequiresBothConditions()
    {
        var items = CreateTestItems();
        var search = new CollectionSearchParameters
        {
            Filter = "type eq 'Note' and contains(content, 'cats')"
        };

        var (result, count) = _engine.Apply(items, search);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Apply_InvalidFilter_ThrowsFormatException()
    {
        var items = CreateTestItems();
        var search = new CollectionSearchParameters { Filter = "bad filter !@#" };

        Assert.Throws<FormatException>(() => _engine.Apply(items, search));
    }

    [Fact]
    public void Apply_NoSearchCriteria_ReturnsAllItems()
    {
        var items = CreateTestItems();
        var search = new CollectionSearchParameters();

        var (result, count) = _engine.Apply(items, search);

        Assert.Equal(4, count);
    }

    [Fact]
    public void Apply_WrappedActivity_SearchFindsInnerContent()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/wrapped",
            Type = new[] { "Note" },
            Content = new[] { "Wrapped content about kittens" }
        };
        var create = new Create
        {
            Id = "https://example.com/activities/1",
            Type = new[] { "Create" },
            Object = new IObjectOrLink[] { note }
        };

        var items = new List<IObjectOrLink> { create };
        var search = new CollectionSearchParameters { Search = "kittens" };

        var (result, count) = _engine.Apply(items, search);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Apply_WrappedActivity_FilterFindsInnerContent()
    {
        var note = new Note
        {
            Id = "https://example.com/notes/wrapped",
            Type = new[] { "Note" },
            Content = new[] { "Wrapped content about kittens" }
        };
        var create = new Create
        {
            Id = "https://example.com/activities/1",
            Type = new[] { "Create" },
            Object = new IObjectOrLink[] { note }
        };

        var items = new List<IObjectOrLink> { create };
        var search = new CollectionSearchParameters { Filter = "contains(content, 'kittens')" };

        var (result, count) = _engine.Apply(items, search);

        Assert.Equal(1, count);
    }
}
