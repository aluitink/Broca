using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.IntegrationTests.Infrastructure;

/// <summary>
/// Extension methods for working with ActivityStreams objects in tests
/// Provides convenient helpers for extracting IDs and values from IObjectOrLink collections
/// </summary>
public static class ActivityStreamExtensions
{
    /// <summary>
    /// Extracts the ID from an IObjectOrLink (handles both inline objects and link references)
    /// </summary>
    public static string? GetId(this IObjectOrLink? objectOrLink)
    {
        return objectOrLink switch
        {
            ILink link => link.Href?.ToString(),
            IObject obj => obj.Id,
            _ => null
        };
    }

    /// <summary>
    /// Extracts the ID from the first item in an IObjectOrLink collection
    /// </summary>
    public static string? GetFirstId(this IEnumerable<IObjectOrLink>? collection)
    {
        return collection?.FirstOrDefault()?.GetId();
    }

    /// <summary>
    /// Extracts all IDs from an IObjectOrLink collection
    /// </summary>
    public static IEnumerable<string> GetIds(this IEnumerable<IObjectOrLink>? collection)
    {
        if (collection == null) yield break;

        foreach (var item in collection)
        {
            var id = item.GetId();
            if (id != null) yield return id;
        }
    }

    /// <summary>
    /// Filters activities by type using type casting (preferred over Type string comparison)
    /// </summary>
    public static IEnumerable<T> OfActivityType<T>(this IEnumerable<IObjectOrLink> activities) where T : class
    {
        return activities.OfType<T>();
    }

    /// <summary>
    /// Finds the first activity of a specific type
    /// </summary>
    public static T? FirstOfType<T>(this IEnumerable<IObjectOrLink> activities) where T : class
    {
        return activities.OfType<T>().FirstOrDefault();
    }

    /// <summary>
    /// Checks if an activity is of a specific type (using type casting)
    /// </summary>
    public static bool IsActivityType<T>(this IObjectOrLink activity) where T : class
    {
        return activity is T;
    }
}
