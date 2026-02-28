namespace Broca.ActivityPub.Core.Interfaces;

/// <summary>
/// Optional interface for actor statistics (e.g., NodeInfo support)
/// </summary>
public interface IActorStatistics
{
    /// <summary>
    /// Counts total number of local actors
    /// </summary>
    Task<int> CountLocalActorsAsync(CancellationToken cancellationToken = default);
}
