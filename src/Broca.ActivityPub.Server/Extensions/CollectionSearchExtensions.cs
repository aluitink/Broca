using Broca.ActivityPub.Core.Interfaces;
using Broca.ActivityPub.Server.Services.CollectionSearch;

namespace Broca.ActivityPub.Server.Extensions;

public static class CollectionSearchExtensions
{
    public static IServiceCollection AddCollectionSearch(this IServiceCollection services)
    {
        services.AddScoped<ICollectionSearchEngine, DefaultCollectionSearchEngine>();
        return services;
    }
}
