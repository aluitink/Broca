using Broca.ActivityPub.Core.Models;

namespace Broca.ActivityPub.UnitTests.CollectionSearch;

public class CollectionSearchParametersTests
{
    [Fact]
    public void HasSearchCriteria_NoParams_ReturnsFalse()
    {
        var p = new CollectionSearchParameters();
        Assert.False(p.HasSearchCriteria);
    }

    [Fact]
    public void HasSearchCriteria_WithFilter_ReturnsTrue()
    {
        var p = new CollectionSearchParameters { Filter = "type eq 'Note'" };
        Assert.True(p.HasSearchCriteria);
    }

    [Fact]
    public void HasSearchCriteria_WithSearch_ReturnsTrue()
    {
        var p = new CollectionSearchParameters { Search = "hello" };
        Assert.True(p.HasSearchCriteria);
    }

    [Fact]
    public void HasSearchCriteria_WithOrderBy_ReturnsTrue()
    {
        var p = new CollectionSearchParameters { OrderBy = "published desc" };
        Assert.True(p.HasSearchCriteria);
    }

    [Fact]
    public void ToQueryString_AllParams_CorrectFormat()
    {
        var p = new CollectionSearchParameters
        {
            Filter = "type eq 'Note'",
            Search = "hello world",
            OrderBy = "published desc"
        };

        var qs = p.ToQueryString();

        Assert.Contains("$filter=", qs);
        Assert.Contains("$search=", qs);
        Assert.Contains("$orderby=", qs);
    }

    [Fact]
    public void ToQueryString_OnlyFilter_NoSearchOrOrderby()
    {
        var p = new CollectionSearchParameters { Filter = "type eq 'Note'" };

        var qs = p.ToQueryString();

        Assert.Contains("$filter=", qs);
        Assert.DoesNotContain("$search=", qs);
        Assert.DoesNotContain("$orderby=", qs);
    }

    [Fact]
    public void FromQueryString_ParsesCorrectly()
    {
        var qs = "$filter=type%20eq%20%27Note%27&$search=hello&$orderby=published%20desc";

        var result = CollectionSearchParameters.FromQueryString(qs);

        Assert.NotNull(result);
        Assert.Equal("type eq 'Note'", result!.Filter);
        Assert.Equal("hello", result.Search);
        Assert.Equal("published desc", result.OrderBy);
    }

    [Fact]
    public void FromQueryString_NullInput_ReturnsNull()
    {
        var result = CollectionSearchParameters.FromQueryString(null);
        Assert.Null(result);
    }

    [Fact]
    public void FromQueryString_EmptyInput_ReturnsNull()
    {
        var result = CollectionSearchParameters.FromQueryString("");
        Assert.Null(result);
    }

    [Fact]
    public void FromQueryString_NoSearchParams_ReturnsNull()
    {
        var result = CollectionSearchParameters.FromQueryString("page=0&limit=20");
        Assert.Null(result);
    }

    [Fact]
    public void ToQueryString_Roundtrips()
    {
        var original = new CollectionSearchParameters
        {
            Filter = "type eq 'Note'",
            Search = "hello"
        };

        var qs = original.ToQueryString();
        var parsed = CollectionSearchParameters.FromQueryString(qs);

        Assert.NotNull(parsed);
        Assert.Equal(original.Filter, parsed!.Filter);
        Assert.Equal(original.Search, parsed.Search);
    }
}
