using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;
using Microsoft.Extensions.Logging;

namespace Broca.ActivityPub.Server.Services.CollectionSearch;

public class DefaultCollectionSearchEngine : ICollectionSearchEngine
{
    private readonly ILogger<DefaultCollectionSearchEngine> _logger;

    public DefaultCollectionSearchEngine(ILogger<DefaultCollectionSearchEngine> logger)
    {
        _logger = logger;
    }

    public (IEnumerable<IObjectOrLink> Items, int TotalCount) Apply(
        IEnumerable<IObjectOrLink> items,
        CollectionSearchParameters parameters)
    {
        var result = items;

        if (!string.IsNullOrWhiteSpace(parameters.Filter))
        {
            try
            {
                var filterNode = ODataFilterParser.Parse(parameters.Filter);
                result = result.Where(item => EvaluateFilter(item, filterNode));
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(ex, "Invalid $filter expression: {Filter}", parameters.Filter);
                throw;
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var searchLower = parameters.Search.ToLowerInvariant();
            result = result.Where(item => MatchesSearch(item, searchLower));
        }

        var materialized = result.ToList();
        var totalCount = materialized.Count;

        if (!string.IsNullOrWhiteSpace(parameters.OrderBy))
        {
            materialized = ApplyOrderBy(materialized, parameters.OrderBy);
        }

        return (materialized, totalCount);
    }

    private static bool MatchesSearch(IObjectOrLink item, string searchLower)
    {
        if (item is not IObject obj)
            return false;

        var content = obj.Content?.FirstOrDefault()?.ToString() ?? "";
        var name = obj.Name?.FirstOrDefault() ?? "";
        var summary = obj.Summary?.FirstOrDefault()?.ToString() ?? "";

        if (content.Contains(searchLower, StringComparison.OrdinalIgnoreCase))
            return true;
        if (name.Contains(searchLower, StringComparison.OrdinalIgnoreCase))
            return true;
        if (summary.Contains(searchLower, StringComparison.OrdinalIgnoreCase))
            return true;

        // Also search within wrapped objects (e.g., Create activity wrapping a Note)
        if (item is Activity activity)
        {
            var innerObj = activity.Object?.FirstOrDefault() as IObject;
            if (innerObj != null)
            {
                var innerContent = innerObj.Content?.FirstOrDefault()?.ToString() ?? "";
                var innerName = innerObj.Name?.FirstOrDefault() ?? "";
                var innerSummary = innerObj.Summary?.FirstOrDefault()?.ToString() ?? "";

                return innerContent.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                       innerName.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                       innerSummary.Contains(searchLower, StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    private static List<IObjectOrLink> ApplyOrderBy(List<IObjectOrLink> items, string orderBy)
    {
        var parts = orderBy.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var property = parts[0].ToLowerInvariant();
        var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        Func<IObjectOrLink, object?> selector = property switch
        {
            "published" => item => (item as IObject)?.Published,
            "updated" => item => (item as IObject)?.Updated,
            "name" => item => (item as IObject)?.Name?.FirstOrDefault(),
            "type" => item => (item as IObject)?.Type?.FirstOrDefault(),
            _ => item => (item as IObject)?.Published
        };

        return descending
            ? items.OrderByDescending(selector).ToList()
            : items.OrderBy(selector).ToList();
    }

    internal static bool EvaluateFilter(IObjectOrLink item, FilterNode node)
    {
        return node switch
        {
            ComparisonNode comparison => EvaluateComparison(item, comparison),
            LogicalNode logical => EvaluateLogical(item, logical),
            NotNode not => !EvaluateFilter(item, not.Inner),
            FunctionNode function => EvaluateFunction(item, function),
            _ => true
        };
    }

    private static bool EvaluateLogical(IObjectOrLink item, LogicalNode node)
    {
        return node.Operator switch
        {
            LogicalOperator.And => EvaluateFilter(item, node.Left) && EvaluateFilter(item, node.Right),
            LogicalOperator.Or => EvaluateFilter(item, node.Left) || EvaluateFilter(item, node.Right),
            _ => true
        };
    }

    private static bool EvaluateComparison(IObjectOrLink item, ComparisonNode node)
    {
        var propertyValue = ResolveProperty(item, node.Property);
        var targetValue = node.Value;

        if (propertyValue == null && targetValue == null)
            return node.Operator is ComparisonOperator.Equal;

        if (propertyValue == null || targetValue == null)
            return node.Operator is ComparisonOperator.NotEqual;

        // String comparison
        if (propertyValue is string strProp && targetValue is string strTarget)
        {
            var cmp = string.Compare(strProp, strTarget, StringComparison.OrdinalIgnoreCase);
            return EvaluateComparisonResult(cmp, node.Operator);
        }

        // DateTimeOffset comparison
        if (propertyValue is DateTimeOffset dtoProp)
        {
            DateTimeOffset dtoTarget;
            if (targetValue is DateTimeOffset dt)
                dtoTarget = dt;
            else if (targetValue is string dateStr && DateTimeOffset.TryParse(dateStr, out var parsed))
                dtoTarget = parsed;
            else
                return false;

            var cmp = dtoProp.CompareTo(dtoTarget);
            return EvaluateComparisonResult(cmp, node.Operator);
        }

        // DateTime comparison
        if (propertyValue is DateTime dateTimeProp)
        {
            DateTime dateTimeTarget;
            if (targetValue is DateTime dtVal)
                dateTimeTarget = dtVal;
            else if (targetValue is DateTimeOffset dtoVal)
                dateTimeTarget = dtoVal.UtcDateTime;
            else if (targetValue is string dateStr2 && DateTime.TryParse(dateStr2, out var parsed2))
                dateTimeTarget = parsed2;
            else
                return false;

            var cmp = dateTimeProp.CompareTo(dateTimeTarget);
            return EvaluateComparisonResult(cmp, node.Operator);
        }

        // Boolean comparison
        if (propertyValue is bool boolProp && targetValue is bool boolTarget)
        {
            return node.Operator switch
            {
                ComparisonOperator.Equal => boolProp == boolTarget,
                ComparisonOperator.NotEqual => boolProp != boolTarget,
                _ => false
            };
        }

        // Numeric comparison
        if (targetValue is double numTarget)
        {
            double numProp;
            if (propertyValue is double d) numProp = d;
            else if (propertyValue is int i) numProp = i;
            else if (propertyValue is uint u) numProp = u;
            else return false;

            var cmp = numProp.CompareTo(numTarget);
            return EvaluateComparisonResult(cmp, node.Operator);
        }

        // Fallback: string comparison
        var strA = propertyValue.ToString() ?? "";
        var strB = targetValue.ToString() ?? "";
        return EvaluateComparisonResult(
            string.Compare(strA, strB, StringComparison.OrdinalIgnoreCase),
            node.Operator);
    }

    private static bool EvaluateComparisonResult(int cmp, ComparisonOperator op) =>
        op switch
        {
            ComparisonOperator.Equal => cmp == 0,
            ComparisonOperator.NotEqual => cmp != 0,
            ComparisonOperator.GreaterThan => cmp > 0,
            ComparisonOperator.GreaterThanOrEqual => cmp >= 0,
            ComparisonOperator.LessThan => cmp < 0,
            ComparisonOperator.LessThanOrEqual => cmp <= 0,
            _ => false
        };

    private static bool EvaluateFunction(IObjectOrLink item, FunctionNode node)
    {
        var propertyValue = ResolveProperty(item, node.Property)?.ToString();
        if (propertyValue == null)
            return false;

        return node.FunctionName switch
        {
            "contains" => propertyValue.Contains(node.Value, StringComparison.OrdinalIgnoreCase),
            "startswith" => propertyValue.StartsWith(node.Value, StringComparison.OrdinalIgnoreCase),
            "endswith" => propertyValue.EndsWith(node.Value, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    internal static object? ResolveProperty(IObjectOrLink item, string property)
    {
        var obj = item as IObject;

        // For activities wrapping objects, allow property resolution on the inner object too
        var innerObj = (item as Activity)?.Object?.FirstOrDefault() as IObject;

        return property.ToLowerInvariant() switch
        {
            "type" => obj?.Type?.FirstOrDefault(),
            "id" => obj?.Id,
            "published" => obj?.Published,
            "updated" => obj?.Updated,
            "name" => obj?.Name?.FirstOrDefault() ?? innerObj?.Name?.FirstOrDefault(),
            "content" => obj?.Content?.FirstOrDefault()?.ToString() ?? innerObj?.Content?.FirstOrDefault()?.ToString(),
            "summary" => obj?.Summary?.FirstOrDefault()?.ToString() ?? innerObj?.Summary?.FirstOrDefault()?.ToString(),
            "mediatype" or "mediaType" => obj?.MediaType,
            "attributedto" or "attributedTo" => ResolveFirstIri(obj?.AttributedTo) ?? ResolveFirstIri(innerObj?.AttributedTo),
            "inreplyto" or "inReplyTo" => ResolveFirstIri(obj?.InReplyTo) ?? ResolveFirstIri(innerObj?.InReplyTo),
            "hasattachment" or "hasAttachment" => (obj?.Attachment?.Any() == true) || (innerObj?.Attachment?.Any() == true),
            "isreply" or "isReply" => (obj?.InReplyTo != null) || (innerObj?.InReplyTo != null),
            "tag" => ResolveFirstTag(obj) ?? ResolveFirstTag(innerObj),
            _ => null
        };
    }

    private static string? ResolveFirstIri(IEnumerable<IObjectOrLink>? items)
    {
        var first = items?.FirstOrDefault();
        if (first is ILink link)
            return link.Href?.ToString();
        if (first is IObject obj)
            return obj.Id;
        return null;
    }

    private static string? ResolveFirstTag(IObject? obj)
    {
        if (obj?.Tag == null) return null;
        var firstTag = obj.Tag.FirstOrDefault();
        if (firstTag is IObject tagObj)
            return tagObj.Name?.FirstOrDefault();
        if (firstTag is ILink tagLink)
            return tagLink.Href?.ToString();
        return null;
    }
}
