namespace Broca.ActivityPub.Core.Interfaces;

/// <summary>
/// Optional interface for activity statistics (e.g., NodeInfo support)
/// </summary>
public interface IActivityStatistics
{
    /// <summary>
    /// Counts Create activities posted since the given date (inclusive)
    /// </summary>
    Task<int> CountCreateActivitiesSinceAsync(DateTime since, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts distinct actors who posted Create activities since the given date (inclusive)
    /// </summary>
    Task<int> CountActiveActorsSinceAsync(DateTime since, CancellationToken cancellationToken = default);
}
