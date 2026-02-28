# Fluent UI Styling Guidelines for Broca Web Client

**Version:** 1.0  
**Created:** February 28, 2026  
**Scope:** Broca.Web Fluent UI renderers and components

## Overview

This document provides comprehensive styling guidelines for the Broca web client, ensuring consistent visual design across all components that render ActivityPub/ActivityStreams content using Microsoft Fluent UI for Blazor.

---

## Table of Contents

1. [Design Philosophy](#design-philosophy)
2. [Fluent UI Design Tokens](#fluent-ui-design-tokens)
3. [Component Architecture](#component-architecture)
4. [ActivityStreams Data Mapping](#activitystreams-data-mapping)
5. [Common UI Patterns](#common-ui-patterns)
6. [Typography Standards](#typography-standards)
7. [Spacing & Layout](#spacing--layout)
8. [Color Usage](#color-usage)
9. [Icons & Visual Indicators](#icons--visual-indicators)
10. [Interactive States](#interactive-states)
11. [Responsive Design](#responsive-design)
12. [Accessibility](#accessibility)
13. [Component-Specific Guidelines](#component-specific-guidelines)

---

## Design Philosophy

### Core Principles

1. **Content-First**: ActivityPub content should be the focus; UI chrome should be minimal and supportive
2. **Consistency**: Similar data types should render similarly across different contexts
3. **Hierarchy**: Use typography and spacing to establish clear visual hierarchy
4. **Familiarity**: Follow established social media patterns users expect (timelines, cards, interactions)
5. **Accessibility**: All components must meet WCAG 2.1 AA compliance

### Two-Layer Architecture

| Layer | Project | Purpose | Styling Approach |
|-------|---------|---------|------------------|
| **Base** | `Broca.ActivityPub.Components` | Structural HTML, logic | Minimal CSS, semantic classes |
| **Presentation** | `Broca.Web` | Fluent UI visuals | Full Fluent UI components, design tokens |

**Guideline**: Base components should be functional without Fluent UI. Fluent renderers add visual polish.

---

## Fluent UI Design Tokens

### Color Tokens (Use These, Not Hard-Coded Values)

```css
/* Backgrounds */
--neutral-layer-1          /* Primary surface */
--neutral-layer-2          /* Elevated/nested surfaces */
--neutral-layer-3          /* Further nesting */
--neutral-layer-4          /* Deepest nesting, footers */

/* Foregrounds */
--neutral-foreground-rest  /* Primary text */
--neutral-foreground-hint  /* Secondary/muted text */

/* Accent Colors */
--accent-fill-rest         /* Primary action color */
--accent-fill-hover        /* Hover state */
--accent-fill-active       /* Active/pressed state */

/* Semantic Colors */
--error                    /* Error states, unlike indicators */
--success                  /* Success states, confirmations */
--warning                  /* Warnings, pending states */
--info                     /* Informational content */

/* Borders & Dividers */
--neutral-stroke-rest      /* Standard borders */
--neutral-stroke-divider-rest /* Dividers, separators */

/* Control Styling */
--control-corner-radius    /* Standard border radius (4px) */
```

### When to Use Each Token

| Use Case | Token | Example |
|----------|-------|---------|
| Card backgrounds | `--neutral-layer-1` | `FluentCard` default |
| Nested content (quotes, replies) | `--neutral-layer-2` | Reply context, summaries |
| Quoted/embedded content | `--neutral-layer-3` | Boosted content background |
| Footer areas | `--neutral-layer-4` | Interaction bar backgrounds |
| Primary text | `--neutral-foreground-rest` | Post content, names |
| Timestamps, metadata | `--neutral-foreground-hint` | "2 hours ago", "@username" |
| Links, actions | `--accent-fill-rest` | Usernames, hashtags, URLs |
| Like indicator | `--error` | Heart icon when liked |
| Boost indicator | `--accent-fill-rest` | Boost icon when active |

---

## Component Architecture

### Standard Card Structure

All content objects (Note, Article, Event, etc.) follow this visual hierarchy:

```
┌─────────────────────────────────────────────────────────┐
│ HEADER: Actor persona + Timestamp + Menu                │
│ ┌───────────────────────────────────────────────────┐  │
│ │ 👤 Display Name                              ⋮   │  │
│ │    @username               2 hours ago           │  │
│ └───────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────┤
│ TITLE (if applicable)                                   │
│ Article Title / Event Name                              │
├─────────────────────────────────────────────────────────┤
│ CONTENT                                                 │
│ Main text content, summary, or body                     │
├─────────────────────────────────────────────────────────┤
│ MEDIA/ATTACHMENTS                                       │
│ Images, videos, audio in gallery format                 │
├─────────────────────────────────────────────────────────┤
│ METADATA (type-specific)                                │
│ Tags, location, time range, link preview                │
├─────────────────────────────────────────────────────────┤
│ INTERACTION BAR                                         │
│ 💬 Reply   🔁 Boost   ❤️ Like   🔖 Bookmark            │
└─────────────────────────────────────────────────────────┘
```

### Fluent Component Mapping

| Card Section | Fluent Component | Usage |
|--------------|------------------|-------|
| Container | `FluentCard` | Wraps entire content item |
| Layout | `FluentStack` | Orientation.Vertical with VerticalGap |
| Actor | `FluentPersona` | Name + avatar + username |
| Timestamp | `FluentLabel` with `Typography.Body`, `Color.Neutral` | Relative time |
| Type Badge | `FluentBadge` | Activity type indicator |
| Title | `FluentLabel` with `Typography.Subject` or `Typography.PageTitle` | Content titles |
| Dividers | `FluentDivider` | Section separations |
| Actions | `FluentButton` with `Appearance.Stealth` | Interaction buttons |
| Menu | `FluentMenu` / `ObjectMenu` | Overflow actions |

---

## ActivityStreams Data Mapping

This section maps ActivityStreams properties to their visual representation.

### Universal Properties (Present on Most Objects)

| Property | Visual Representation | Fluent Component | Notes |
|----------|----------------------|------------------|-------|
| `id` | Not displayed directly | N/A | Used for navigation, API calls |
| `type` | Badge or icon | `FluentBadge` | Show for activities, optional for objects |
| `name` | Title/heading | `FluentLabel` (Subject/PageTitle) | First entry from array |
| `summary` | Italic subtitle or card | `FluentLabel` or nested `FluentCard` | Often used as CW/spoiler |
| `content` | Main body | `MarkupString` in div | Render as HTML, sanitize |
| `published` | Timestamp | `<time>` with relative format | "2h ago", hover shows full date |
| `updated` | "Edited" indicator | Small text label | Show if differs from published |
| `attributedTo` | Actor persona | `FluentPersona` | Resolve to actor display |
| `attachment` | Media gallery | `MediaGallery` | Images, videos, audio |
| `tag` | Hashtag list | `FluentBadge` pills | Clickable, navigate to hashtag feed |
| `url` | External link | `FluentAnchor` | "View original" link |

### Actor-Specific Properties

| Property | Visual Representation | Component | Notes |
|----------|----------------------|-----------|-------|
| `preferredUsername` | @handle | `FluentLabel` with `@` prefix | `Color.Neutral` |
| `name` | Display name | `FluentLabel` (Header weight) | Bold |
| `icon` | Avatar | `FluentPersona` image | 32px inline, 75px profile |
| `image` | Banner | Full-width image | Profile header background |
| `followers` | Count | Text with icon | "123 Followers" |
| `following` | Count | Text with icon | "45 Following" |

### Activity-Specific Rendering

| Activity Type | Visual Treatment | Icon | Badge Color |
|---------------|------------------|------|-------------|
| `Create` | Show created object only | None needed | N/A |
| `Announce` (Boost) | "🔁 Boosted" badge + object | `ArrowRepeatAll` | Accent |
| `Like` | "❤️ Liked" badge + object | `Heart` | Error (red) |
| `Follow` | "Followed" notification | `PersonAdd` | Accent |
| `Undo` | "Undid [action]" | `ArrowUndo` | Neutral |
| `Update` | "Edited" indicator | `Edit` | Warning |
| `Delete` | Tombstone/removed notice | `Delete` | Neutral/muted |
| `Block` | "Blocked" notification | `PersonProhibited` | Error |

### Content Object Styling by Type

#### Note (Short-form content)
- **Container**: `FluentCard` with standard padding
- **Content**: Full width, `line-height: 1.6`
- **Character limit visual**: None (let content flow)
- **Media**: Gallery below content, max 4 visible

#### Article (Long-form content)
- **Container**: `FluentCard` with enhanced padding
- **Featured image**: Full-width, max-height 400px, rounded corners
- **Title**: `Typography.PageTitle`, bold
- **Read time**: Calculated estimate with clock icon
- **Tags**: Displayed at bottom with tag icon

#### Event
- **Container**: `FluentCard` with calendar accent
- **Header**: Calendar icon + title + "Event" badge
- **Details section**: Start/end times, location with icons
- **RSVP action**: Prominent accent button

#### Question/Poll
- **Container**: `FluentCard` with question icon
- **Options**: Radio-style buttons (before vote), progress bars (after)
- **Results**: Percentage bars, vote counts
- **Meta**: Time remaining, total votes

#### Place/Location
- **Compact mode**: Icon + name inline
- **Full mode**: Card with address, coordinates, map link
- **Map integration**: External link to OpenStreetMap/provider

#### Audio
- **Player**: Native HTML5 audio with custom styling
- **Metadata**: Duration, title, artist (if available)
- **Waveform**: Optional visualization (future)

#### Video
- **Player**: Native HTML5 video, responsive
- **Thumbnail**: Poster image if available
- **Duration badge**: Overlay on corner

#### Image
- **Gallery view**: Grid layout, 1-4 images
- **Lightbox**: Full-screen on click
- **Alt text**: Tooltip/accessible description

---

## Common UI Patterns

### Actor Attribution (Header Pattern)

Use consistently at the top of content cards:

```razor
<FluentStack Orientation="Orientation.Horizontal" HorizontalGap="12" 
             VerticalAlignment="VerticalAlignment.Center">
    <FluentPersona Name="@GetActorDisplay()"
                   Image="@GetActorImageUrl()"
                   ImageSize="32px"
                   TextPosition="@TextPosition.End">
        <FluentLabel Typo="Typography.Body" Color="Color.Neutral">
            @@@GetActorUsername()
        </FluentLabel>
    </FluentPersona>
    
    <FluentLabel Typo="Typography.Body" Color="Color.Neutral">
        <time datetime="@timestamp.ToString("o")">
            @FormatRelativeTime(timestamp)
        </time>
    </FluentLabel>
</FluentStack>
```

**Sizing guidelines**:
- Inline/feed: 32px avatar
- Profile/featured: 48-75px avatar
- Mini/notification: 24px avatar

### Activity Type Badge

Use for activities in feeds to indicate action type:

```razor
<FluentBadge Appearance="Appearance.Accent">
    @GetActivityTypeIcon() @GetActivityType()
</FluentBadge>
```

### Interaction Bar

Standard pattern for all interactable content:

```razor
<div class="fluent-interaction-bar">
    <FluentButton Appearance="@(IsActive ? Appearance.Accent : Appearance.Stealth)"
                  IconStart="@icon"
                  Title="@tooltip">
        @if (count > 0)
        {
            <span class="count">@FormatCount(count)</span>
        }
    </FluentButton>
    <!-- Repeat for each action -->
</div>
```

**Button visibility rules**:
- Reply: Always visible
- Boost: Visible if content is public/unlisted
- Like: Always visible for authenticated users
- Bookmark: Always visible for authenticated users
- Share: Optional, for copy link / external share
- More: Overflow menu (report, mute, block, etc.)

### Loading States

Use consistent loading indicators:

```razor
@* Inline loading *@
<FluentProgressRing Style="width: 20px; height: 20px;" />

@* Full card skeleton *@
<FluentCard>
    <FluentSkeleton Style="height: 32px; width: 60%;" />
    <FluentSkeleton Style="height: 100px; width: 100%; margin-top: 12px;" />
</FluentCard>
```

### Error States

```razor
<FluentMessageBar Intent="MessageIntent.Error">
    @ErrorMessage
</FluentMessageBar>
```

### Empty States

```razor
<FluentStack Orientation="Orientation.Vertical" 
             HorizontalAlignment="HorizontalAlignment.Center"
             Style="padding: 2rem; text-align: center;">
    <FluentIcon Value="@(new Icons.Regular.Size48.DocumentBulletList())" 
                Color="Color.Neutral" />
    <FluentLabel Typo="Typography.Subject" Color="Color.Neutral">
        No items to display
    </FluentLabel>
</FluentStack>
```

---

## Typography Standards

> **Source of truth:** [`Typography.cs`](https://github.com/microsoft/fluentui-blazor/blob/dev/src/Core/Enums/Typography.cs) · [`FontWeight.cs`](https://github.com/microsoft/fluentui-blazor/blob/dev/src/Core/Enums/FontWeight.cs)

### Valid `Typography` Enum Values

```
Body, Subject, Header, PaneHeader, EmailHeader,
PageTitle, HeroTitle, H1, H2, H3, H4, H5, H6
```

> ⚠️ `Typography.Caption` **does not exist** — use `Typography.Body` with `Color.Neutral` for caption-like text.

### Valid `FontWeight` Enum Values

```
Normal (400), Bold (600), Bolder (800)
```

> ⚠️ `FontWeight.Semibold` **does not exist** — use `FontWeight.Bold`.

### Fluent Typography Scale

| Use Case | Typography Enum | Example |
|----------|-----------------|--------|
| Page headers | `Typography.PageTitle` | Profile name page |
| Section headers | `Typography.Header` | "Followers", "Posts" |
| Pane/panel headers | `Typography.PaneHeader` | Sidebar panel titles |
| Card titles | `Typography.Subject` | Article title, event name |
| Body text | `Typography.Body` | Post content, descriptions |
| Captions / metadata | `Typography.Body` + `Color.Neutral` | Timestamps, counts, hints |

### Text Styling Patterns

```razor
@* Primary content *@
<FluentLabel Typo="Typography.Body">Main content text</FluentLabel>

@* Secondary/muted (caption-like) — Typography.Caption does not exist *@
<FluentLabel Typo="Typography.Body" Color="Color.Neutral">Metadata, timestamps, counts</FluentLabel>

@* Emphasized — FontWeight.Semibold does not exist, use Bold *@
<FluentLabel Typo="Typography.Body" Weight="FontWeight.Bold">Important</FluentLabel>

@* Italic (summaries, quotes) *@
<FluentLabel Typo="Typography.Body" Style="font-style: italic;">Summary text</FluentLabel>
```

### Text Hierarchy in Cards

1. **Actor name**: Bold, standard size
2. **Username**: Neutral color, preceded by @
3. **Timestamp**: Neutral color, right-aligned or after username
4. **Title**: Subject typography, bold
5. **Content**: Body typography, comfortable line-height
6. **Metadata**: `Typography.Body` with `Color.Neutral` (there is no `Typography.Caption`)

---

## Spacing & Layout

### Standard Spacing Values

Follow Fluent UI 4px grid system:

| Size | Value | Use Case |
|------|-------|----------|
| XS | 4px | Icon-to-text gap |
| S | 8px | Related elements |
| M | 12px | Within-component sections |
| L | 16px | Card padding, between cards |
| XL | 24px | Major sections |

### FluentStack Gap Patterns

```razor
@* Tight grouping (icon + label) *@
<FluentStack HorizontalGap="4">

@* Related items (badges, buttons) *@
<FluentStack HorizontalGap="8">

@* Card sections (header, content, footer) *@
<FluentStack VerticalGap="12">

@* Cards in feed *@
<FluentStack VerticalGap="16">
```

### Card Padding

- Default `FluentCard`: Built-in padding (16px)
- Nested cards: Use `padding: 12px` or `padding: 1rem`
- Compact mode: `padding: 8px`

### Margin Between Cards

```css
.fluent-note-card,
.fluent-activity-card,
.fluent-article-card {
    margin-bottom: 16px; /* 1rem */
}
```

---

## Color Usage

### Semantic Color Application

| Context | Color Token | Example |
|---------|-------------|---------|
| Liked content | `--error` (red tint) | Heart icon fill |
| Boosted content | `--accent-fill-rest` | Repeat icon |
| Followed users | `--success` | Checkmark indicator |
| Pending actions | `--warning` | Processing spinner |
| Errors | `--error` | Error messages |
| Links | `--accent-fill-rest` | All clickable URLs |

### Activity Type Color Coding

| Activity | Badge Background | Text |
|----------|-----------------|------|
| Boost/Announce | `--accent-fill-rest` | White |
| Like | `--error` variant | White |
| Follow | `--success` variant | White |
| Block | `--error` | White |
| Update | `--warning` | Dark |
| Delete | `--neutral-layer-3` | Standard |

### Dark Mode Considerations

All color tokens automatically adapt to dark mode. Avoid hard-coded colors.

**Do:**
```css
color: var(--neutral-foreground-rest);
background: var(--neutral-layer-2);
```

**Don't:**
```css
color: #333333;
background: #f0f0f0;
```

---

## Icons & Visual Indicators

### Standard Icon Set (Fluent UI System Icons)

| Action/Type | Icon | Size |
|-------------|------|------|
| Reply | `ChatMultiple` | 20px |
| Boost/Repost | `ArrowRepeatAll` | 20px |
| Like | `Heart` / `HeartFilled` | 20px |
| Bookmark | `Bookmark` / `BookmarkFilled` | 20px |
| Share | `Share` | 20px |
| More options | `MoreHorizontal` | 20px |
| Person/Actor | `Person` | 24-48px |
| Calendar/Event | `CalendarLtr` | 24px |
| Location/Place | `Location` | 20-24px |
| Link | `Link` | 16px |
| Time/Duration | `Clock` | 16px |
| Edit | `Edit` | 16px |
| Delete | `Delete` | 16px |
| Question/Poll | `QuestionCircle` | 24px |
| Audio | `Speaker` | 24px |
| Video | `Video` | 24px |
| Image | `Image` | 24px |
| Document | `Document` | 24px |
| Tag/Hashtag | `Tag` | 16px |

### Icon Usage Guidelines

1. **Button icons**: Use 20px size
2. **Inline with text**: Use 16px size
3. **Header/feature icons**: Use 24px size
4. **Large decorative**: Use 48px size

```razor
@* Button icon *@
<FluentButton IconStart="@(new Icons.Regular.Size20.Heart())" />

@* Inline icon *@
<FluentIcon Value="@(new Icons.Regular.Size16.Clock())" />
<span>2 hours ago</span>

@* Header icon *@
<FluentIcon Value="@(new Icons.Regular.Size24.CalendarLtr())" Color="Color.Accent" />
```

> ⚠️ **Not all icons exist at all sizes.** The icon catalog is a partial subset — an icon available at Size20 may not exist at Size48. Always verify by checking existing usages in the codebase or by letting the build fail fast. Do **not** guess icon names.

### Confirmed Size48 Icons (verified in codebase)

| Icon class | Usage |
|------------|-------|
| `Icons.Regular.Size48.Apps` | Generic collection/empty state |
| `Icons.Regular.Size48.Person` | Actor placeholder |
| `Icons.Regular.Size48.People` | Relationships/followers empty state |
| `Icons.Regular.Size48.PersonAdd` | Follow suggestions empty state |
| `Icons.Regular.Size48.Video` | Video placeholder |
| `Icons.Regular.Size48.MailInbox` | Inbox empty state |
| `Icons.Regular.Size48.Send` | Outbox empty state |
| `Icons.Regular.Size48.Alert` | Notifications empty state |
| `Icons.Regular.Size48.Warning` | Error states |
| `Icons.Regular.Size48.DocumentBulletList` | Generic list empty state |

> `Icons.Regular.Size48.FolderOpen` **does not exist** — use `Icons.Regular.Size48.Apps` or a Size24 icon instead.

### State-Based Icon Variants

| State | Variant | Example |
|-------|---------|---------|
| Default/Inactive | `Regular` | `Icons.Regular.Size20.Heart()` |
| Active/Selected | `Filled` | `Icons.Filled.Size20.Heart()` |

---

## Interactive States

### Button Appearance States

| State | Appearance | Use Case |
|-------|------------|----------|
| Default action | `Appearance.Stealth` | Interaction bar buttons |
| Active/toggled | `Appearance.Accent` | Liked, boosted states |
| Primary CTA | `Appearance.Accent` | "Post", "Follow", "RSVP" |
| Secondary | `Appearance.Outline` | Poll options, cancel |
| Tertiary | `Appearance.Lightweight` | "View more", links |

### Hover & Focus States

Fluent UI components handle hover/focus automatically. For custom elements:

```css
.custom-element:hover {
    background-color: var(--neutral-fill-stealth-hover);
}

.custom-element:focus-visible {
    outline: 2px solid var(--focus-stroke-outer);
    outline-offset: 2px;
}
```

### Loading/Processing States

```razor
<FluentButton Disabled="@IsProcessing">
    @if (IsProcessing)
    {
        <FluentProgressRing Style="width: 16px; height: 16px;" />
        <span>Processing...</span>
    }
    else
    {
        <span>Submit</span>
    }
</FluentButton>
```

### Optimistic UI Updates

For interaction buttons, update visual state immediately:

```razor
@* Show filled heart immediately on click, revert if API fails *@
<FluentButton 
    IconStart="@(IsLiked ? new Icons.Filled.Size20.Heart() : new Icons.Regular.Size20.Heart())"
    Appearance="@(IsLiked ? Appearance.Accent : Appearance.Stealth)"
    OnClick="@HandleLikeClick" />
```

---

## Responsive Design

### Breakpoints

| Breakpoint | Width | Target |
|------------|-------|--------|
| Mobile | < 600px | Phones |
| Tablet | 600px - 1024px | Tablets, small laptops |
| Desktop | > 1024px | Desktops, large screens |

### Mobile Adaptations

```css
@media (max-width: 600px) {
    /* Reduce padding */
    .fluent-note-card,
    .fluent-activity-card {
        padding: 12px;
    }
    
    /* Stack horizontal layouts vertically */
    .note-header {
        flex-direction: column;
        align-items: flex-start;
    }
    
    /* Reduce avatar sizes */
    .actor-persona {
        --persona-image-size: 28px;
    }
    
    /* Full-width media */
    .note-attachments {
        grid-template-columns: 1fr;
    }
}
```

### Touch Targets

Ensure minimum 44x44px touch targets for interactive elements:

```css
.interaction-button {
    min-width: 44px;
    min-height: 44px;
}
```

---

## Accessibility

### Required Practices

1. **Alt text for images**: Always provide `alt` attribute
   ```razor
   <img src="@url" alt="@(altText ?? "Image attachment")" />
   ```

2. **ARIA labels for icon buttons**:
   ```razor
   <FluentButton IconStart="@icon" Title="Like this post" aria-label="Like" />
   ```

3. **Semantic HTML**:
   ```razor
   <article class="note-card">
       <header class="note-header">...</header>
       <div class="note-content">...</div>
       <footer class="note-actions">...</footer>
   </article>
   ```

4. **Time elements with datetime**:
   ```razor
   <time datetime="@timestamp.ToString("o")" title="@timestamp.ToString("f")">
       @FormatRelativeTime(timestamp)
   </time>
   ```

5. **Focus management**: Ensure keyboard navigation works
6. **Color contrast**: Use Fluent tokens which meet AA standards
7. **Reduced motion**: Respect `prefers-reduced-motion`

### Screen Reader Considerations

- Activity announcements: "Alice boosted Bob's post"
- Interaction counts: "12 likes" not just "12"
- Loading states: "Loading posts..." with `aria-busy`

---

## Component-Specific Guidelines

### Note Renderer

```
Structure: Card → Header (persona + time) → Content → Media → Interactions

Key considerations:
- Content may contain HTML (sanitize, render as MarkupString)
- Attachments vary in number (1-10+)
- Replies collection indicates thread depth
- InReplyTo shows parent context
```

**CSS classes:**
- `.fluent-note-card` - Container
- `.note-header` - Actor + timestamp row
- `.note-content` - Main text body
- `.note-attachments` - Media grid
- `.note-interactions` - Footer buttons

### Article Renderer

```
Structure: Card → Featured Image → Header (title + meta) → Summary → Content → Tags → Interactions

Key considerations:
- Articles have longer content (show read time)
- Featured image from `image` property
- May have multiple authors (attributedTo array)
- Tags should be clickable hashtags
```

**Special CSS:**
```css
.article-featured-image {
    max-height: 400px;
    object-fit: cover;
    border-radius: var(--control-corner-radius);
}

.article-content {
    line-height: 1.8;  /* More readable for long-form */
    font-size: 1.1rem; /* Slightly larger */
}
```

### Event Renderer

```
Structure: Card → Header (icon + title + badge) → Description → Details (time/location) → RSVP

Key considerations:
- Always show start time prominently
- Location may be a Place object or string
- End time is optional
- RSVP is primary action
```

**Visual hierarchy:**
1. Calendar icon + Event title
2. "Event" type badge
3. Start/end times with clock icons
4. Location with map icon
5. RSVP button (accent)

### Question/Poll Renderer

```
Structure: Card → Question text → Options (buttons or results) → Meta (time/votes) → Vote button

Key considerations:
- Show options as buttons before voting
- Show results as progress bars after voting
- Indicate user's selection with checkmark
- Respect poll expiration (disable after endTime)
```

**States:**
- Not voted: Option buttons active
- Voted: Results with percentages
- Expired: Results, "Poll closed" indicator

### Actor/Profile Renderer

```
Structure: Card → Banner (if available) → Avatar + Name + Username → Bio → Stats → Follow button

Key considerations:
- Banner is optional (image property)
- Avatar from icon property
- Stats from followers/following collection counts
- Follow button state depends on relationship
```

**Sizing:**
- Profile page avatar: 75-100px
- Card/inline avatar: 32-48px
- Mini reference: 24px

### Interaction Bar

```
Structure: Horizontal flex → Reply → Boost → Like → Bookmark → Share → More

Key considerations:
- Each button shows count if > 0
- Toggle state (filled icon + accent) for user actions
- Disable during API calls
- Show error state on failure
```

**Count formatting:**
- < 1000: Show exact (12)
- >= 1000: Abbreviate (1.2K)
- >= 1000000: Abbreviate (1.2M)

---

## CSS Class Naming Convention

### Prefix Rules

| Layer | Prefix | Example |
|-------|--------|---------|
| Base component | `activitypub-` | `.activitypub-note-content` |
| Fluent renderer | `fluent-` | `.fluent-note-card` |
| Specific element | `{component}-{element}` | `.note-header`, `.note-content` |

### Standard Element Names

- `-card`: Root container
- `-header`: Top section with metadata
- `-content`: Main body content
- `-footer`: Bottom section with actions
- `-title`: Primary heading
- `-meta`: Secondary information
- `-actions`: Button group
- `-attachments`: Media container

---

## File Organization

### CSS Files

```
src/Broca.Web/
├── wwwroot/css/
│   ├── app.css          # Global app styles
│   ├── renderers.css    # Shared renderer styles
│   ├── themes/          # (Future) Theme overrides
│   │   └── dark.css
│   └── responsive.css   # (Future) Breakpoint overrides
└── Renderers/
    ├── FluentNoteRenderer.razor.css      # Component-scoped
    ├── FluentArticleRenderer.razor.css
    └── ...
```

### When to Use Scoped vs Global CSS

| Scope | Use When |
|-------|----------|
| Component-scoped (`.razor.css`) | Styles only affect one component |
| Global (`renderers.css`) | Shared patterns across multiple renderers |
| Inline `Style` attribute | One-off, dynamic, or contextual styling |

---

## Checklist for New Renderers

When creating a new Fluent UI renderer:

- [ ] Wrap in `FluentCard` with `Class="fluent-{type}-card"`
- [ ] Use `FluentStack` for layout (Vertical for sections, Horizontal for rows)
- [ ] Add `FluentPersona` for actor attribution
- [ ] Use `FluentLabel` with appropriate `Typography` for text
- [ ] Add `FluentDivider` between major sections
- [ ] Include `FluentBadge` for type/status indicators (if activity)
- [ ] Use Fluent icons from `Microsoft.FluentUI.AspNetCore.Components.Icons`
- [ ] Handle loading state with `FluentProgressRing`
- [ ] Handle error state with `FluentMessageBar`
- [ ] Include `ObjectMenu` for overflow actions
- [ ] Support `AdditionalContent` render fragment
- [ ] Create corresponding `.razor.css` file
- [ ] Register in `FluentRendererExtensions.cs`
- [ ] Test responsive behavior
- [ ] Test keyboard navigation
- [ ] Verify color contrast

---

## Quick Reference: Fluent Component Imports

```razor
@using Microsoft.FluentUI.AspNetCore.Components
@using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons
```

Common components:
- `FluentCard`, `FluentStack`, `FluentDivider`
- `FluentLabel`, `FluentBadge`, `FluentAnchor`
- `FluentButton`, `FluentTextField`, `FluentTextArea`, `FluentSelect`
- `FluentPersona`, `FluentIcon`, `FluentProgressRing`
- `FluentMessageBar`, `FluentSkeleton`, `FluentMenu`

---

## Revision History

| Date | Version | Notes |
|------|---------|-------|
| 2026-02-28 | 1.0 | Initial guidelines document |
| 2026-02-28 | 1.1 | Corrected invalid enum values against FluentUI source: `Typography.Caption` → `Typography.Body + Color.Neutral`; `FontWeight.Semibold` → `FontWeight.Bold`; added full valid enum listings with GitHub source links; documented Size48 icon availability and known missing icons |
