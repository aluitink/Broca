# Broca.ActivityPub

A modular .NET implementation of the ActivityPub protocol.

## UI Component Architecture

The UI layer is split into two projects:

- **`Broca.ActivityPub.Components`** — framework-agnostic Blazor component library containing core ActivityPub UI components (feeds, profiles, cards, etc.) and a renderer abstraction (`IObjectRenderer`, `ObjectRendererBase<T>`, `IObjectRendererRegistry`) with default plain-Blazor renderer implementations.
- **`Broca.Web`** — Blazor WebAssembly host application that references `Broca.ActivityPub.Components` and provides Fluent UI (`Microsoft.FluentUI.AspNetCore.Components`) overrides for all renderers. Fluent UI renderer implementations live in `Broca.Web/Renderers/` and are registered at startup via `FluentRendererExtensions.RegisterFluentRenderers()`.

When developing UI components:
- Logic and structure that is UI-framework-agnostic belongs in `Broca.ActivityPub.Components`.
- Fluent UI-specific visuals and interactions belong in `Broca.Web`.
- New renderable ActivityStreams types should have both a default renderer in `Broca.ActivityPub.Components/Renderers/` and a Fluent UI renderer in `Broca.Web/Renderers/`, registered in `FluentRendererExtensions`.

## Guidelines

- Do not create or update documentation files (e.g. `README.md`, `CONTRIBUTING.md`, XML doc comments, inline summaries) unless explicitly asked.
- Do not add code comments that explain what code does.
- The project is in early development and changes frequently; keep implementations simple and avoid over-engineering.
- If an opportunity to simplify or refactor existing code is noticed, ask the user before making those changes.
- Be targeted with your test execution.

## ActivityStreams `IObjectOrLink` Type Evaluation

Properties typed as `IEnumerable<IObjectOrLink>` (e.g. `Actor`, `Object`, `AttributedTo`, `InReplyTo`, `To`, etc.) can hold a mix of inline objects and unresolved references. The concrete runtime type is determined by the JSON:

| JSON value | Runtime type | Meaning |
|---|---|---|
| Plain string (`"https://..."`) | `ILink` (Href set, `Type=["Link"]`) | Unresolved IRI reference |
| `{"type":"Link",...}` or `{"type":"Mention",...}` | `ILink` | Qualified link |
| `{"type":"Note",...}` (or any known type) | `IObject` (concrete subtype) | Inline object |
| Object with no `type` | `ObjectOrLink` | Anonymous object |

**Always check `ILink` first** — a plain URL string is the most common form of an unresolved actor/object reference:

```csharp
var ref = activity.Actor?.FirstOrDefault();
if (ref is ILink link)
    actorId = link.Href?.ToString();      // unresolved — fetch if needed
else if (ref is Actor actor)
    actorId = actor.Id;                   // already inline
```

**`ILink` serializes back to a plain string** when `Href` is the only property set — so `is ILink` (not `is Link`) is the correct check for "unresolved reference".

**`IEnumerable<ILink>`** (e.g. `Object.Url`) always contains links — no need to type-check.

**`IImageOrLink`** (used for `Icon`/`Image`) only ever holds `Image` or `Link`.

**To dereference:** use `IActivityPubClient.GetAsync<T>(link.Href)`. Never assume an `IObjectOrLink` is a full object without checking `is IObject` first.

## JSON Serialization

**The KristofferStrube.ActivityStreams library types already have JSON converters attributed on them.** 
Standard `JsonSerializerOptions` with camelCase naming is sufficient — no custom converters need to be registered.

## 3rd Party Libraries
- If we need details for Kristoffer Strube's ActivityStreams .NET library, refer to the official GitHub repository: https://github.com/KristofferStrube/ActivityStreams
- Check the Blazor Fluent UI repository for component usage patterns.