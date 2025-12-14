using Microsoft.AspNetCore.Components;

namespace Broca.ActivityPub.Components.Services;

/// <summary>
/// Interface for components that render ActivityStreams objects.
/// </summary>
public interface IObjectRenderer
{
    /// <summary>
    /// Gets the type of object this renderer supports.
    /// </summary>
    Type SupportedType { get; }

    /// <summary>
    /// Gets the render fragment for the object.
    /// </summary>
    RenderFragment GetRenderFragment(object obj);
}

/// <summary>
/// Base class for object renderers with strongly-typed support.
/// </summary>
/// <typeparam name="T">The type of object this renderer handles.</typeparam>
public abstract class ObjectRendererBase<T> : IObjectRenderer where T : class
{
    /// <inheritdoc />
    public Type SupportedType => typeof(T);

    /// <inheritdoc />
    public RenderFragment GetRenderFragment(object obj)
    {
        if (obj is T typedObj)
        {
            return Render(typedObj);
        }

        return builder => { };
    }

    /// <summary>
    /// Renders the strongly-typed object.
    /// </summary>
    /// <param name="obj">The object to render.</param>
    /// <returns>A render fragment for the object.</returns>
    protected abstract RenderFragment Render(T obj);
}
