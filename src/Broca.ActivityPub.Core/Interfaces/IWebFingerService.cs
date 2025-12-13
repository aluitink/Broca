using Broca.ActivityPub.Core.Models;

namespace Broca.ActivityPub.Core.Interfaces;

/// <summary>
/// Service for WebFinger protocol operations
/// </summary>
public interface IWebFingerService
{
    /// <summary>
    /// Resolves a user alias (e.g., @user@domain.tld) to a WebFinger resource
    /// </summary>
    /// <param name="client">HTTP client to use for the request</param>
    /// <param name="userAlias">User alias in the format @user@domain.tld or user@domain.tld</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>WebFinger resource containing user information</returns>
    Task<WebFingerResource> WebFingerUserByAliasAsync(HttpClient client, string userAlias, CancellationToken cancellationToken = default);
}
