using Broca.ActivityPub.Components.Renderers;
using KristofferStrube.ActivityStreams;
using Microsoft.AspNetCore.Components;

namespace Broca.ActivityPub.Components.Services;

/// <summary>
/// Default proxy renderer for Note objects.
/// </summary>
internal class DefaultNoteRendererProxy : ObjectRendererBase<Note>
{
    protected override RenderFragment Render(Note obj)
    {
        return builder =>
        {
            builder.OpenComponent<NoteRenderer>(0);
            builder.AddAttribute(1, "Note", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Default proxy renderer for Article objects.
/// </summary>
internal class DefaultArticleRendererProxy : ObjectRendererBase<Article>
{
    protected override RenderFragment Render(Article obj)
    {
        return builder =>
        {
            builder.OpenComponent<ArticleRenderer>(0);
            builder.AddAttribute(1, "Article", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Default proxy renderer for Image objects.
/// </summary>
internal class DefaultImageRendererProxy : ObjectRendererBase<Image>
{
    protected override RenderFragment Render(Image obj)
    {
        return builder =>
        {
            builder.OpenComponent<ImageRenderer>(0);
            builder.AddAttribute(1, "Image", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Default proxy renderer for Video objects.
/// </summary>
internal class DefaultVideoRendererProxy : ObjectRendererBase<Video>
{
    protected override RenderFragment Render(Video obj)
    {
        return builder =>
        {
            builder.OpenComponent<VideoRenderer>(0);
            builder.AddAttribute(1, "Video", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Default proxy renderer for Document objects.
/// </summary>
internal class DefaultDocumentRendererProxy : ObjectRendererBase<Document>
{
    protected override RenderFragment Render(Document obj)
    {
        return builder =>
        {
            builder.OpenComponent<DocumentRenderer>(0);
            builder.AddAttribute(1, "Document", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Default proxy renderer for Actor/Person objects.
/// </summary>
internal class DefaultActorRendererProxy : ObjectRendererBase<KristofferStrube.ActivityStreams.Object>
{
    protected override RenderFragment Render(KristofferStrube.ActivityStreams.Object obj)
    {
        return builder =>
        {
            builder.OpenComponent<ActorRenderer>(0);
            builder.AddAttribute(1, "Actor", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Default proxy renderer for Activity objects.
/// </summary>
internal class DefaultActivityRendererProxy : ObjectRendererBase<Activity>
{
    protected override RenderFragment Render(Activity obj)
    {
        return builder =>
        {
            builder.OpenComponent<ActivityRenderer>(0);
            builder.AddAttribute(1, "Activity", obj);
            builder.CloseComponent();
        };
    }
}

/// <summary>
/// Default proxy renderer for Link objects.
/// </summary>
internal class DefaultLinkRendererProxy : ObjectRendererBase<Link>
{
    protected override RenderFragment Render(Link obj)
    {
        return builder =>
        {
            builder.OpenComponent<LinkRenderer>(0);
            builder.AddAttribute(1, "Link", obj);
            builder.CloseComponent();
        };
    }
}
