# Web Client Enhancement Plan

**Branch:** `feature/web-client-enhancements`  
**Created:** February 28, 2026  
**Last Audited:** February 28, 2026

## Architecture

1. **`Broca.ActivityPub.Components`** — framework-agnostic Blazor components, default unstyled renderers, core logic and state management.
2. **`Broca.Web`** — Fluent UI overrides for all renderers, application pages, routing, and theming.

---

## Workstream Status

| Workstream | Primary Focus | Status |
|------------|---------------|--------|
| **WS1: Object Renderers** | Content type renderers (Question, Event, Place, Audio, Page) | ✅ Complete |
| **WS2: Activity Renderers** | Activity renderers (Update, Delete, Block, Undo) | ✅ Complete |
| **WS3: Media & Attachments** | MediaGallery, MediaUpload, drag-and-drop | ✅ Complete |
| **WS4: Post & Interaction** | PostComposer, InteractionBar enhancements | ✅ Complete |
| **WS5: Feeds & Timelines** | ActivityFeed, ReplyThread, ConversationView, timeline pages | ✅ Complete |
| **WS6: Profiles & Social** | ActorProfile, ProfileEditor, ActorBrowser, RelationshipsList | ✅ Complete |
| **WS7: Notifications & Search** | NotificationFeed, SearchBar, HashtagFeed, Explore | 🟡 Partial |
| **WS8: Polish & Performance** | CSS, accessibility, performance, testing | ❌ Not started |

---

## Completed Work

### Renderers — `Broca.ActivityPub.Components/Renderers/` and `Broca.Web/Renderers/`

- Base + Fluent renderers: `Question`, `Event`, `Place`, `Audio`, `Page`, `Update`, `Delete`, `Block`, `Undo`
- All registered in `FluentRendererExtensions.cs`
- Fixed invalid FluentUI enum values in `FluentDeleteRenderer`, `FluentBlockRenderer`
- _Deferred:_ `ArticleRenderer` and `VideoRenderer` enhancements

### Media — `Broca.ActivityPub.Components/` and `Broca.Web/Components/Media/`

- `MediaGallery.razor` — lightbox, zoom, keyboard nav, mixed media, metadata display
- `MediaUpload.razor` — drag-and-drop, preview, validation, progress, multi-file, alt text
- `FluentMediaGallery.razor` — Fluent UI styled lightbox with transitions
- `FluentMediaUpload.razor` — Fluent file picker, progress bars
- _Deferred:_ swipe navigation, image cropping

### Post Composition — `PostComposer.razor`, `FluentPostComposerRenderer.razor`, `PostComposerDialog.razor`

- Character counter, content warnings, poll creation, draft auto-save, Ctrl+Enter submit
- Visibility levels (public, unlisted, followers, direct)
- Emoji picker integration, rich text toolbar, ARIA labels
- Dialog state persistence
- _Completed separately:_ `MentionAutocomplete.razor`, `HashtagAutocomplete.razor`, `LinkPreview.razor`, `AttachmentList.razor`
- _Deferred:_ integration into PostComposer UI, post scheduling UI component, minimize/expand dialog, multiple drafts

### Interaction — `InteractionBar.razor`, `FluentInteractionBarRenderer.razor`, `Broca.Web/Components/Interactions/`

- Bookmark, share/announce, reply count from `replies` collection, optimistic UI, loading/error states
- Fluent icons, hover tooltips, active/inactive state styling
- `LikeButton.razor`, `BookmarkButton.razor`, `ShareButton.razor`, `ReplyButton.razor`, `MoreOptionsMenu.razor`

### Feeds & Conversation — `ActivityFeed.razor`, `ReplyThread.razor`, `ConversationView.razor`

- `ActivityFeed`: load-newer with count, auto-refresh (configurable, pauses on scroll), empty state, error recovery
- `ActivityFeed`: search within feed (content, actors, activity types), scroll-to-top button, filter integration support
- `FeedFilter.razor` + `FluentFeedFilterRenderer.razor` — filter by activity type, content type, actor, date range
- `ReplyThread`: nested display, load-more replies, collapse/expand with configurable auto-collapse depth
- `ConversationView` + `FluentConversationView` — full conversation display, parent chain traversal, focal post highlighting, thread lines, chronological/threaded views
- `Post.razor` now uses `ConversationView` for better threaded conversation experience
- Timeline pages: `HomeTimeline.razor`, `LocalTimeline.razor`, `FederatedTimeline.razor`, `HashtagTimeline.razor` — dedicated pages with filters, search, and auto-refresh
- _Deferred:_ thread muting/following, OP highlighting, thread summary/metadata

### Profiles & Social — `ActorProfile.razor`, `ProfileEditor.razor`, `ActorBrowser.razor`, `RelationshipsList.razor`

- `ActorProfile`: banner image, statistics (followers/following/posts loaded from collections), pinned posts, custom fields (attachment), verification link badges
- `ProfileEditor`: avatar upload (FluentMediaUpload), banner upload, bio/summary editing, custom fields editor (add/remove, max 4)
- `ActorBrowser`: debounced search (400ms), follow/unfollow from results with callback, **recent searches display**, **suggested actors support**
- `ActorProfile`: **activity calendar (`ActivityCalendar.razor`, `FluentActivityCalendar.razor`)** — GitHub-style contribution graph
- `RelationshipsList`: collection-backed list with action buttons, virtualization support, customizable templates
- _Deferred:_ domain blocks, export/import

### Composition & Interaction Enhancements

- **`MentionAutocomplete.razor`** — actor search dropdown triggered by `@`, shows avatar, name, and handle
- **`HashtagAutocomplete.razor`** — hashtag suggestions triggered by `#`, shows recent and popular tags with usage counts
- **`LinkPreview.razor`** — automatic URL detection, fetches OpenGraph/Twitter Card metadata, displays preview card with dismiss
- **`AttachmentList.razor`** + **`FluentAttachmentList.razor`** — displays uploaded attachments (images/video/audio/files) with thumbnails, alt text editing, and remove actions
- **`PostScheduler.cs`** service — manages scheduled posts with status tracking (pending/published/failed/cancelled)
- _Integration needed:_ Wire above components into PostComposer and FluentPostComposerRenderer

### Notifications — `NotificationFeed.razor`

- Filter by type: All, Mentions, Likes, Boosts, Follows
- Unread count badge, mark as read/unread, mark-all-read
- _Deferred:_ grouping by type, bulk delete/dismiss, notification preferences

### Advanced Features — `ReportDialog.razor`, `ContentFilterSettings.razor`, `BookmarksList.razor`

- `ReportDialog` + `FluentReportDialog`: report posts/actors with multiple reason selection, optional notes, forward to remote server, block after report
- `ContentFilterSettings` + `FluentContentFilterSettings`: keyword filters by context, content warning preferences, media autoplay settings, timeline filters
- `BookmarksList` + `FluentBookmarksList`: folder organization, search, load-more pagination, customizable item templates
- _Deferred:_ folder management dialogs (create/edit/delete), move bookmark dialog, export functionality

---

## Remaining Work

### High Priority

#### Notifications & Search (WS7)

- [ ] `NotificationIndicator.razor` (`Broca.Web/Components/Notifications/`) — unread badge, quick dropdown; wire into `NavMenu.razor`
- [ ] `NotificationFeed`: group by type, bulk delete/dismiss
- [ ] `SearchBar.razor` (base) — actors (WebFinger), hashtags, posts, recent history, suggestions
- [ ] `HashtagFeed.razor` — hashtag timeline, follow/unfollow hashtag, related hashtags
- [ ] `Explore.razor` — trending hashtags/posts, suggested actors

### Medium Priority

#### Post Composition Gaps

- [x] Mention autocomplete (`MentionAutocomplete.razor`) — searches actors while typing `@`, shows dropdown with actor suggestions
- [x] Hashtag autocomplete (`HashtagAutocomplete.razor`) — suggests hashtags based on recent/common tags, shows usage stats
- [x] Link preview generation (`LinkPreview.razor`) — fetches URL metadata (OpenGraph/Twitter Card), displays preview card with dismiss
- [x] Attachment preview with remove buttons (`AttachmentList.razor`, `FluentAttachmentList.razor`) — displays uploaded attachments with thumbnails, alt text editing, and remove actions
- [ ] Attachment integration into PostComposer — wire AttachmentList into composer UI
- [ ] Post scheduling (`PostScheduler.cs` service created) — UI component and integration needed

#### Profiles Gaps

- [x] `ActorBrowser`: recent searches display with clear button
- [x] `ActorBrowser`: suggested actors support with customizable templates
- [x] `ActorProfile`: activity calendar/heatmap (`ActivityCalendar.razor`, `FluentActivityCalendar.razor`) — GitHub-style contribution graph showing posting activity over time

#### Thread Display Gaps

- [ ] Thread muting/following
- [ ] Highlight OP (original poster) in thread
- [ ] Thread summary/metadata (reply count, participants)

### Lower Priority — Polish & Performance (WS8)

- [ ] Dark mode theme (`wwwroot/css/themes/dark.css`)
- [ ] Animations and transitions (`wwwroot/css/animations.css`)
- [ ] Responsive/mobile layout (`wwwroot/css/responsive.css`), touch gesture support, bottom nav
- [ ] Virtualization audit, image lazy loading, caching strategy, service worker
- [ ] Accessibility audit (ARIA, keyboard nav, screen reader, contrast, focus trapping)
- [ ] Unit + integration tests for core component logic

### Phase 4: Advanced Features

- [x] `ReportDialog.razor` — report posts/actors with reasons and status tracking
- [x] `FluentReportDialog.razor` — Fluent UI dialog with FluentCheckbox, FluentTextArea
- [x] `ContentFilterSettings.razor` — keyword filtering, CW preferences, media filtering
- [x] `FluentContentFilterSettings.razor` — Fluent UI settings panel with FluentSwitch, FluentSelect
- [x] `BookmarksList.razor` — folder/collection organization, search, export
- [x] `FluentBookmarksList.razor` — Fluent UI bookmarks list with folder sidebar, search
- [ ] PWA support — offline, install prompt, push notifications (open question)
- [ ] Multi-account support

---

## Design Principles

- **Progressive Enhancement** — working HTML first, JS enhances
- **Mobile-First** — design for mobile, scale to desktop
- **Accessibility-First** — build accessible from the start
- **Template-driven** — expose `RenderFragment<T>` for all customizable parts
- **Type-safe** — leverage ActivityStreams types; avoid `dynamic`/`object`

## Naming Conventions

- Base components: `{Feature}.razor`
- Fluent renderers: `Fluent{Feature}Renderer.razor`
- CSS classes: `activitypub-{component}` (base), `fluent-{component}` (Fluent)

---

## Revision History

| Date | Version | Notes |
|------|---------|-------|
| 2026-02-28 | 1.0 | Initial plan created |
| 2026-02-28 | 1.1 | Added parallel development workstream organization |
| 2026-02-28 | 1.2 | WS3 and WS4 marked complete; fixed invalid FluentUI enum values |
| 2026-02-28 | 1.3 | Audit: WS2 confirmed complete; WS5/WS6/WS7 partial status updated |
| 2026-02-28 | 1.4 | Audit: WS6 largely complete (RelationshipsList, ActorProfile banner/stats/pinned/custom fields/verification, ProfileEditor avatar+banner+bio+custom fields, ActorBrowser debounce+follow confirmed); completed sections consolidated |
| 2026-02-28 | 1.5 | Advanced features: ReportDialog, ContentFilterSettings, BookmarksList (base + Fluent renderers) implemented |
| 2026-02-28 | 1.5 | WS5 complete: FeedFilter (base + Fluent), ActivityFeed search/scroll-to-top/filter support, Timeline pages (Home/Local/Federated/Hashtag), ConversationView wired into Post.razor |
| 2026-02-28 | 1.6 | WS6 complete: MentionAutocomplete, HashtagAutocomplete, LinkPreview, AttachmentList (+ Fluent renderers), PostScheduler service, ActorBrowser recent searches/suggested actors, ActivityCalendar (+ Fluent renderer) implemented; all medium priority profile/composition gaps addressed |
