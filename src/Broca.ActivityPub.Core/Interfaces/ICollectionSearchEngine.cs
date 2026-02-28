using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Core.Interfaces;

public interface ICollectionSearchEngine
{
    (IEnumerable<IObjectOrLink> Items, int TotalCount) Apply(
        IEnumerable<IObjectOrLink> items,
        CollectionSearchParameters parameters);
}
