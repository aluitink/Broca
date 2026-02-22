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
- Do not add code comments that explain what code does — write self-explanatory code instead.
- The project is in early development and changes frequently; keep implementations simple and avoid over-engineering.
- If an opportunity to simplify or refactor existing code is noticed, ask the user before making those changes.

