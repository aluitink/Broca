using Broca.ActivityPub.Components.Services;
using KristofferStrube.ActivityStreams;
using Microsoft.AspNetCore.Components;

namespace Broca.Web.Renderers;

/// <summary>
/// Extension methods for registering Fluent UI renderers.
/// </summary>
public static class FluentRendererExtensions
{
    /// <summary>
    /// Registers all Fluent UI renderers with the object renderer registry.
    /// </summary>
    /// <param name="registry">The renderer registry.</param>
    public static void RegisterFluentRenderers(this IObjectRendererRegistry registry)
    {
        // Register custom Fluent UI renderers for ActivityStreams types
        registry.RegisterRenderer(typeof(Note), new FluentNoteRendererProxy());
        registry.RegisterRenderer(typeof(Article), new FluentArticleRendererProxy());
        registry.RegisterRenderer(typeof(Person), new FluentActorRendererProxy());
        registry.RegisterRenderer(typeof(Actor), new FluentActorRendererProxy());
        registry.RegisterRenderer(typeof(Create), new FluentCreateRendererProxy());
        registry.RegisterRenderer(typeof(Like), new FluentLikeRendererProxy());
        registry.RegisterRenderer(typeof(Announce), new FluentAnnounceRendererProxy());
        registry.RegisterRenderer(typeof(Follow), new FluentFollowRendererProxy());
        registry.RegisterRenderer(typeof(Activity), new FluentActivityRendererProxy());
        registry.RegisterRenderer(typeof(Image), new FluentImageRendererProxy());
        registry.RegisterRenderer(typeof(Video), new FluentVideoRendererProxy());
        registry.RegisterRenderer(typeof(Document), new FluentDocumentRendererProxy());
        registry.RegisterRenderer(typeof(Link), new FluentLinkRendererProxy());
        registry.RegisterRenderer(typeof(Question), new FluentQuestionRendererProxy());
        registry.RegisterRenderer(typeof(Audio), new FluentAudioRendererProxy());
        registry.RegisterRenderer(typeof(Event), new FluentEventRendererProxy());
        registry.RegisterRenderer(typeof(Place), new FluentPlaceRendererProxy());
        registry.RegisterRenderer(typeof(Page), new FluentPageRendererProxy());
        
        // Activity type renderers
        registry.RegisterRenderer(typeof(Update), new FluentUpdateRendererProxy());
        registry.RegisterRenderer(typeof(Delete), new FluentDeleteRendererProxy());
        registry.RegisterRenderer(typeof(Block), new FluentBlockRendererProxy());
        registry.RegisterRenderer(typeof(Undo), new FluentUndoRendererProxy());
    }
}

/// <summary>
/// Proxy renderer for Note objects using FluentNoteRenderer.
/// </summary>
internal class FluentNoteRendererProxy : ObjectRendererBase<Note>
{
    protected override RenderFragment Render(Note obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentNoteRenderer>(0);
            builder.AddAttribute(1, "Note", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Article objects using FluentArticleRenderer.
/// </summary>
internal class FluentArticleRendererProxy : ObjectRendererBase<Article>
{
    protected override RenderFragment Render(Article obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentArticleRenderer>(0);
            builder.AddAttribute(1, "Article", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Actor/Person objects using FluentActorRenderer.
/// </summary>
internal class FluentActorRendererProxy : ObjectRendererBase<KristofferStrube.ActivityStreams.Object>
{
    protected override RenderFragment Render(KristofferStrube.ActivityStreams.Object obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentActorRenderer>(0);
            builder.AddAttribute(1, "Actor", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Follow activities using FluentFollowRenderer.
/// </summary>
internal class FluentFollowRendererProxy : ObjectRendererBase<Follow>
{
    protected override RenderFragment Render(Follow obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentFollowRenderer>(0);
            builder.AddAttribute(1, "Follow", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Activity objects using FluentActivityRenderer.
/// </summary>
internal class FluentActivityRendererProxy : ObjectRendererBase<Activity>
{
    protected override RenderFragment Render(Activity obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentActivityRenderer>(0);
            builder.AddAttribute(1, "Activity", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Create objects using FluentCreateRenderer.
/// </summary>
internal class FluentCreateRendererProxy : ObjectRendererBase<Create>
{
    protected override RenderFragment Render(Create obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentCreateRenderer>(0);
            builder.AddAttribute(1, "Create", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Like objects using FluentLikeRenderer.
/// </summary>
internal class FluentLikeRendererProxy : ObjectRendererBase<Like>
{
    protected override RenderFragment Render(Like obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentLikeRenderer>(0);
            builder.AddAttribute(1, "Like", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Announce objects using FluentAnnounceRenderer.
/// </summary>
internal class FluentAnnounceRendererProxy : ObjectRendererBase<Announce>
{
    protected override RenderFragment Render(Announce obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentAnnounceRenderer>(0);
            builder.AddAttribute(1, "Announce", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Image objects using FluentImageRenderer.
/// </summary>
internal class FluentImageRendererProxy : ObjectRendererBase<Image>
{
    protected override RenderFragment Render(Image obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentImageRenderer>(0);
            builder.AddAttribute(1, "Image", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Video objects using FluentVideoRenderer.
/// </summary>
internal class FluentVideoRendererProxy : ObjectRendererBase<Video>
{
    protected override RenderFragment Render(Video obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentVideoRenderer>(0);
            builder.AddAttribute(1, "Video", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Document objects using FluentDocumentRenderer.
/// </summary>
internal class FluentDocumentRendererProxy : ObjectRendererBase<Document>
{
    protected override RenderFragment Render(Document obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentDocumentRenderer>(0);
            builder.AddAttribute(1, "Document", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Link objects using FluentLinkRenderer.
/// </summary>
internal class FluentLinkRendererProxy : ObjectRendererBase<Link>
{
    protected override RenderFragment Render(Link obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentLinkRenderer>(0);
            builder.AddAttribute(1, "Link", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Question objects using FluentQuestionRenderer.
/// </summary>
internal class FluentQuestionRendererProxy : ObjectRendererBase<Question>
{
    protected override RenderFragment Render(Question obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentQuestionRenderer>(0);
            builder.AddAttribute(1, "Question", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Audio objects using FluentAudioRenderer.
/// </summary>
internal class FluentAudioRendererProxy : ObjectRendererBase<Audio>
{
    protected override RenderFragment Render(Audio obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentAudioRenderer>(0);
            builder.AddAttribute(1, "Audio", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Event objects using FluentEventRenderer.
/// </summary>
internal class FluentEventRendererProxy : ObjectRendererBase<Event>
{
    protected override RenderFragment Render(Event obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentEventRenderer>(0);
            builder.AddAttribute(1, "Event", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Place objects using FluentPlaceRenderer.
/// </summary>
internal class FluentPlaceRendererProxy : ObjectRendererBase<Place>
{
    protected override RenderFragment Render(Place obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentPlaceRenderer>(0);
            builder.AddAttribute(1, "Place", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Page objects using FluentPageRenderer.
/// </summary>
internal class FluentPageRendererProxy : ObjectRendererBase<Page>
{
    protected override RenderFragment Render(Page obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentPageRenderer>(0);
            builder.AddAttribute(1, "Page", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Update activities using FluentUpdateRenderer.
/// </summary>
internal class FluentUpdateRendererProxy : ObjectRendererBase<Update>
{
    protected override RenderFragment Render(Update obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentUpdateRenderer>(0);
            builder.AddAttribute(1, "Update", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Delete activities using FluentDeleteRenderer.
/// </summary>
internal class FluentDeleteRendererProxy : ObjectRendererBase<Delete>
{
    protected override RenderFragment Render(Delete obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentDeleteRenderer>(0);
            builder.AddAttribute(1, "Delete", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Block activities using FluentBlockRenderer.
/// </summary>
internal class FluentBlockRendererProxy : ObjectRendererBase<Block>
{
    protected override RenderFragment Render(Block obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentBlockRenderer>(0);
            builder.AddAttribute(1, "Block", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Proxy renderer for Undo activities using FluentUndoRenderer.
/// </summary>
internal class FluentUndoRendererProxy : ObjectRendererBase<Undo>
{
    protected override RenderFragment Render(Undo obj)
    {
        return builder =>
        {
            builder.OpenComponent<FluentUndoRenderer>(0);
            builder.AddAttribute(1, "Undo", obj);
            builder.CloseComponent();
        };
    }
}
