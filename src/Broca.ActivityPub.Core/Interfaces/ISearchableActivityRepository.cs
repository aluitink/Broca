using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Core.Interfaces;

public interface ISearchableActivityRepository
{
    Task<IEnumerable<IObjectOrLink>> GetInboxActivitiesAsync(
        string username,
        CollectionSearchParameters search,
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default);

    Task<int> GetInboxCountAsync(
        string username,
        CollectionSearchParameters search,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<IObjectOrLink>> GetOutboxActivitiesAsync(
        string username,
        CollectionSearchParameters search,
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default);

    Task<int> GetOutboxCountAsync(
        string username,
        CollectionSearchParameters search,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<IObjectOrLink>> GetRepliesAsync(
        string objectId,
        CollectionSearchParameters search,
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default);

    Task<int> GetRepliesCountAsync(
        string objectId,
        CollectionSearchParameters search,
        CancellationToken cancellationToken = default);
}
