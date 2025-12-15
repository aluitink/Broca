using Broca.ActivityPub.Core.Models;
using KristofferStrube.ActivityStreams;

namespace Broca.ActivityPub.Core.Interfaces;

/// <summary>
/// Service interface for managing custom collections
/// </summary>
public interface ICollectionService
{
    /// <summary>
    /// Creates a new custom collection
    /// </summary>
    Task<CustomCollectionDefinition> CreateCollectionAsync(
        string username, 
        CustomCollectionDefinition definition, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing collection definition
    /// </summary>
    Task<CustomCollectionDefinition> UpdateCollectionAsync(
        string username, 
        string collectionId, 
        CustomCollectionDefinition definition, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a collection
    /// </summary>
    Task DeleteCollectionAsync(
        string username, 
        string collectionId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a collection definition
    /// </summary>
    Task<CustomCollectionDefinition?> GetCollectionDefinitionAsync(
        string username, 
        string collectionId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all collection definitions for a user
    /// </summary>
    Task<IEnumerable<CustomCollectionDefinition>> GetCollectionDefinitionsAsync(
        string username, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the items in a collection (resolves query filters for query collections)
    /// </summary>
    Task<IEnumerable<IObjectOrLink>> GetCollectionItemsAsync(
        string username, 
        string collectionId, 
        int limit = 20, 
        int offset = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of items in a collection
    /// </summary>
    Task<int> GetCollectionItemCountAsync(
        string username, 
        string collectionId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an item to a manual collection
    /// </summary>
    Task AddItemToCollectionAsync(
        string username, 
        string collectionId, 
        string itemId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an item from a manual collection
    /// </summary>
    Task RemoveItemFromCollectionAsync(
        string username, 
        string collectionId, 
        string itemId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a collection definition
    /// </summary>
    Task<(bool IsValid, string? ErrorMessage)> ValidateCollectionDefinitionAsync(
        CustomCollectionDefinition definition, 
        CancellationToken cancellationToken = default);
}
