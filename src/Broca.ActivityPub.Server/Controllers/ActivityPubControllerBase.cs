using System.Globalization;
using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using KristofferStrube.ActivityStreams.JsonLD;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Broca.ActivityPub.Server.Controllers;

/// <summary>
/// Base controller for ActivityPub endpoints with common helper methods
/// </summary>
public abstract class ActivityPubControllerBase : ControllerBase
{
    /// <summary>
    /// Gets the base URL for the request, handling reverse proxy scenarios.
    /// Uses X-Forwarded-Proto header if present (for HTTPS termination at proxy).
    /// </summary>
    /// <param name="suffix">Optional suffix to append to the base URL</param>
    /// <returns>The base URL with the correct scheme</returns>
    protected string GetBaseUrl(string? suffix = null)
    {
        // Use X-Forwarded-Proto if present (for reverse proxy scenarios), otherwise use Request.Scheme
        var scheme = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;
        var baseUrl = $"{scheme}://{Request.Host}";
        
        if (!string.IsNullOrEmpty(suffix))
        {
            baseUrl += suffix.TrimEnd('/');
        }
        
        return baseUrl;
    }

    protected CollectionSearchParameters? GetSearchParameters()
    {
        var filter = Request.Query["$filter"].FirstOrDefault();
        var search = Request.Query["$search"].FirstOrDefault();
        var orderBy = Request.Query["$orderby"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(filter) &&
            string.IsNullOrWhiteSpace(search) &&
            string.IsNullOrWhiteSpace(orderBy))
        {
            return null;
        }

        return new CollectionSearchParameters
        {
            Filter = filter,
            Search = search,
            OrderBy = orderBy
        };
    }

    protected IActionResult BuildCollectionResponse(
        string collectionUrl,
        IEnumerable<IObjectOrLink> items,
        int totalCount,
        int page,
        int limit,
        CollectionSearchParameters? search = null,
        bool itemsAlreadyPaginated = false)
    {
        var searchEngine = HttpContext.RequestServices.GetService<ICollectionSearchEngine>();

        if (search?.HasSearchCriteria == true && searchEngine != null && !itemsAlreadyPaginated)
        {
            var (filtered, filteredCount) = searchEngine.Apply(items, search);
            totalCount = filteredCount;
            items = filtered.Skip(page * limit).Take(limit);
        }
        else if (!itemsAlreadyPaginated)
        {
            items = items.Skip(page * limit).Take(limit);
        }

        var searchQuery = search?.ToQueryString();
        var hasSearchQuery = !string.IsNullOrWhiteSpace(searchQuery);

        var hasPageParam = Request.Query.ContainsKey("page");
        var hasLimitParam = Request.Query.ContainsKey("limit");

        if (!hasPageParam && !hasLimitParam && !hasSearchQuery)
        {
            var collection = new OrderedCollection
            {
                JsonLDContext = ActivityStreamsLdContext(),
                Id = collectionUrl,
                TotalItems = (uint)totalCount,
                First = totalCount > 0
                    ? new Link { Href = new Uri($"{collectionUrl}?page=0&limit={limit}") }
                    : null
            };
            return Ok(collection);
        }

        var paginationBase = hasSearchQuery
            ? $"{collectionUrl}?{searchQuery}&"
            : $"{collectionUrl}?";

        var collectionPage = new OrderedCollectionPage
        {
            JsonLDContext = ActivityStreamsLdContext(),
            Id = $"{paginationBase}page={page}&limit={limit}",
            PartOf = new Link { Href = new Uri(collectionUrl) },
            TotalItems = (uint)totalCount,
            OrderedItems = items.ToList(),
            Next = (page * limit + limit < totalCount)
                ? new Link { Href = new Uri($"{paginationBase}page={page + 1}&limit={limit}") }
                : null,
            Prev = page > 0
                ? new Link { Href = new Uri($"{paginationBase}page={page - 1}&limit={limit}") }
                : null
        };
        return Ok(collectionPage);
    }

    private static List<ITermDefinition> ActivityStreamsLdContext() =>
        new() { new ReferenceTermDefinition(new Uri("https://www.w3.org/ns/activitystreams")) };

    protected bool RequiresInMemorySearch(CollectionSearchParameters? search)
    {
        if (search?.HasSearchCriteria != true)
            return false;

        var searchEngine = HttpContext.RequestServices.GetService<ICollectionSearchEngine>();
        if (searchEngine == null)
            return false;

        var repo = HttpContext.RequestServices.GetService<IActivityRepository>();
        return repo is not ISearchableActivityRepository;
    }

    protected static void ValidateRequestClockSkew(HttpRequest request)
    {
        var dateHeaderValue = request.Headers.TryGetValue("Date", out var dateValues)
            ? dateValues.ToString()
            : request.Headers.TryGetValue("Created", out var createdValues)
                ? createdValues.ToString()
                : null;

        if (string.IsNullOrEmpty(dateHeaderValue))
            throw new InvalidOperationException("Request is missing a Date or Created header");

        if (!DateTimeOffset.TryParse(dateHeaderValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var requestDate))
            throw new InvalidOperationException($"Request Date header is not a valid date/time: '{dateHeaderValue}'");

        var now = DateTimeOffset.UtcNow;

        if (now - requestDate > TimeSpan.FromHours(12))
            throw new InvalidOperationException("Request date is too old (clock skew exceeds 12 hours)");

        if (requestDate - now > TimeSpan.FromMinutes(5))
            throw new InvalidOperationException("Request date is too far in the future (clock skew exceeds 5 minutes)");
    }
}
