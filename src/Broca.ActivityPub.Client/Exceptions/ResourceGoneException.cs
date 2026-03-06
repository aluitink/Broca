namespace Broca.ActivityPub.Client.Exceptions;

public class ResourceGoneException(Uri uri) : Exception($"Resource is gone: {uri}")
{
    public Uri Uri { get; } = uri;
}
