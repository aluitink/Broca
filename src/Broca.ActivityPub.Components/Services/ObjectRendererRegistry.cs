using Microsoft.AspNetCore.Components;

namespace Broca.ActivityPub.Components.Services;

/// <summary>
/// Registry for managing object renderers.
/// </summary>
public interface IObjectRendererRegistry
{
    /// <summary>
    /// Registers a renderer for a specific object type.
    /// </summary>
    /// <param name="objectType">The type of object the renderer handles.</param>
    /// <param name="renderer">The renderer instance.</param>
    void RegisterRenderer(Type objectType, IObjectRenderer renderer);

    /// <summary>
    /// Gets a renderer for the specified object type.
    /// </summary>
    /// <param name="objectType">The type of object to get a renderer for.</param>
    /// <returns>The renderer if found, otherwise null.</returns>
    IObjectRenderer? GetRenderer(Type objectType);

    /// <summary>
    /// Gets a render fragment for the specified object.
    /// </summary>
    /// <param name="obj">The object to render.</param>
    /// <returns>A render fragment for the object, or a default fragment if no renderer is found.</returns>
    RenderFragment GetRenderFragment(object obj);
}

/// <summary>
/// Default implementation of the object renderer registry.
/// </summary>
public class ObjectRendererRegistry : IObjectRendererRegistry
{
    private readonly Dictionary<Type, IObjectRenderer> _renderers = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public void RegisterRenderer(Type objectType, IObjectRenderer renderer)
    {
        lock (_lock)
        {
            _renderers[objectType] = renderer;
        }
    }

    /// <inheritdoc />
    public IObjectRenderer? GetRenderer(Type objectType)
    {
        lock (_lock)
        {
            // Try exact type match first
            if (_renderers.TryGetValue(objectType, out var renderer))
            {
                return renderer;
            }

            // Try to find a renderer for a base type or interface
            var rendererEntry = _renderers.FirstOrDefault(kvp => 
                kvp.Key.IsAssignableFrom(objectType));

            return rendererEntry.Value;
        }
    }

    /// <inheritdoc />
    public RenderFragment GetRenderFragment(object obj)
    {
        if (obj == null)
        {
            return builder => { };
        }

        var renderer = GetRenderer(obj.GetType());
        if (renderer != null)
        {
            return renderer.GetRenderFragment(obj);
        }

        // Return a default render fragment that displays the object type
        return builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "activitypub-object-fallback");
            builder.OpenElement(2, "span");
            builder.AddAttribute(3, "class", "object-type");
            builder.AddContent(4, obj.GetType().Name);
            builder.CloseElement(); // span
            builder.AddContent(5, " (no renderer available)");
            builder.CloseElement(); // div
        };
    }
}
