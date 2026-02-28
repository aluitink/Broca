# Web Client Enhancement Plan

**Branch:** `feature/web-client-enhancements`  
**Created:** February 28, 2026  
**Status:** Planning Phase

## Overview

This document outlines a multi-phase development plan for enhancing the Broca web client. The plan focuses on building robust base functionality in the Components layer (framework-agnostic), then polishing and refining the experience in the Web client layer (Fluent UI implementation).

## Architecture Philosophy

### Two-Layer Design

1. **Broca.ActivityPub.Components** (Base Layer)
   - Framework-agnostic Blazor components
   - Core ActivityPub UI logic and state management
   - Default unstyled renderers (plain HTML/CSS)
   - Template-based customization via `RenderFragment<T>`
   - Minimal dependencies, maximum reusability

2. **Broca.Web** (Presentation Layer)
   - Fluent UI-based implementations
   - Overrides for all base renderers
   - Application-specific pages and routing
   - Theme and styling
   - Enhanced user interactions

### Development Approach

- **Build base → Polish presentation**: Implement core functionality in Components first, then enhance with Fluent UI
- **Component-first**: Break features into small, composable components
- **Template-driven**: All components should expose templates for customization
- **Type-safe**: Leverage ActivityStreams types fully
- **Performance-conscious**: Use virtualization, lazy loading, and efficient rendering

---

## Parallel Development Workstreams

To enable multiple developers to work simultaneously with minimal conflicts, the work has been organized into **8 independent workstreams**. Each workstream has clear file/directory boundaries and minimal dependencies on other streams.

### Workstream Assignment Strategy

- **Workstreams 1-4**: Can start immediately (Phase 1 work)
- **Workstreams 5-6**: Can start after basic components are in place
- **Workstreams 7-8**: Polish and optimization work (can run in parallel with others)

### Dependency Map

```
Timeline View:

Week 1-2:  [WS1: Renderers (Content)] ──┐
           [WS2: Renderers (Activity)] ──┼──> [WS5: Feeds & Timelines]
           [WS3: Media]                ──┼──> [WS6: Profiles & Social]
           [WS4: Post & Interaction]   ──┘     [WS7: Notifications & Search]
                                               [WS8: Polish & Performance]
                                                    (starts mid-Week 2)

Week 3+:   All workstreams running in parallel
           WS8 progressively polishes completed components
           WS9-10 can be added as capacity allows

Critical Path:
  WS1/WS2 (Renderers) → WS5 (Feeds) → Timeline Pages Complete
  WS3 (Media) → WS4 (PostComposer integration) → Posting Complete
  WS3 (Media) → WS6 (Profile avatar/banner) → Profile Complete
```

**Key Integration Points:**
1. **Week 2**: WS3 completes media upload → WS4 integrates into PostComposer → WS6 integrates into ProfileEditor
2. **Week 2-3**: WS1/WS2 complete renderers → WS5/WS7 can fully test feed components
3. **Week 3+**: WS8 begins CSS/polish work across completed components

---

### Workstream 1: Object Renderers (Content Types)

**Owner:** Developer A  
**Directory Focus:** `src/Broca.ActivityPub.Components/Renderers/`, `src/Broca.Web/Renderers/`  
**Conflict Risk:** Low (creating new files)  
**Status:** ✅ **COMPLETE** (2026-02-28)

**Scope:**
- ✅ Implement missing base renderers (Question, Event, Place, Audio, Page)
- ✅ Implement corresponding Fluent UI renderers
- ✅ Register new renderers in `FluentRendererExtensions.cs`
- ⚠️ Complete/enhance existing renderers (Article, Video) - _deferred to future work_

**Files Created:** ✅
- ✅ `src/Broca.ActivityPub.Components/Renderers/QuestionRenderer.razor`
- ✅ `src/Broca.ActivityPub.Components/Renderers/EventRenderer.razor`
- ✅ `src/Broca.ActivityPub.Components/Renderers/PlaceRenderer.razor`
- ✅ `src/Broca.ActivityPub.Components/Renderers/AudioRenderer.razor`
- ✅ `src/Broca.ActivityPub.Components/Renderers/PageRenderer.razor`
- ✅ `src/Broca.Web/Renderers/FluentQuestionRenderer.razor` (renamed from `QuestionRenderer.razor`)
- ✅ `src/Broca.Web/Renderers/FluentEventRenderer.razor` (renamed from `EventRenderer.razor`)
- ✅ `src/Broca.Web/Renderers/FluentPlaceRenderer.razor`
- ✅ `src/Broca.Web/Renderers/FluentPlaceRenderer.razor.css`
- ✅ `src/Broca.Web/Renderers/FluentAudioRenderer.razor` (renamed from `AudioRenderer.razor`)
- ✅ `src/Broca.Web/Renderers/FluentPageRenderer.razor`
- ✅ `src/Broca.Web/Renderers/FluentPageRenderer.razor.css`

**Files Modified:** ✅
- ✅ `src/Broca.Web/Renderers/FluentRendererExtensions.cs` (registered all 5 new content-type renderers)
- ✅ `src/Broca.Web/Renderers/FluentDeleteRenderer.razor` (fixed invalid FluentUI enum values)
- ✅ `src/Broca.Web/Renderers/FluentBlockRenderer.razor` (fixed invalid FluentUI enum values)
- ⚠️ `src/Broca.ActivityPub.Components/Renderers/ArticleRenderer.razor` (enhancement deferred)
- ⚠️ `src/Broca.ActivityPub.Components/Renderers/VideoRenderer.razor` (enhancement deferred)

**Dependencies:** None (started immediately) - ✅ **COMPLETED**

---

### Workstream 2: Activity Renderers

**Owner:** Developer B  
**Directory Focus:** `src/Broca.ActivityPub.Components/Renderers/`, `src/Broca.Web/Renderers/`  
**Conflict Risk:** Low (creating new files)  
**Status:** ✅ **COMPLETE** (2026-02-28)

**Scope:**
- ✅ Implement activity-specific renderers (Update, Delete, Block, Undo)
- ✅ Implement corresponding Fluent UI renderers
- ✅ Register renderers

**Files Created:** ✅
- ✅ `src/Broca.ActivityPub.Components/Renderers/UpdateRenderer.razor`
- ✅ `src/Broca.ActivityPub.Components/Renderers/DeleteRenderer.razor`
- ✅ `src/Broca.ActivityPub.Components/Renderers/BlockRenderer.razor`
- ✅ `src/Broca.ActivityPub.Components/Renderers/UndoRenderer.razor`
- ✅ `src/Broca.Web/Renderers/FluentUpdateRenderer.razor`
- ✅ `src/Broca.Web/Renderers/FluentDeleteRenderer.razor`
- ✅ `src/Broca.Web/Renderers/FluentBlockRenderer.razor`
- ✅ `src/Broca.Web/Renderers/FluentUndoRenderer.razor`

**Files Modified:** ✅
- ✅ `src/Broca.Web/Renderers/FluentRendererExtensions.cs` (registered Update, Delete, Block, Undo renderers)

**Dependencies:** None — ✅ **COMPLETED**

---

### Workstream 3: Media & Attachments

**Owner:** Developer C  
**Directory Focus:** `src/Broca.ActivityPub.Components/MediaGallery.razor`, `src/Broca.Web/Components/Media/` (new)  
**Conflict Risk:** Low (mostly new components)  
**Status:** ✅ **COMPLETE** (2026-02-28)

**Scope:**
- ✅ Enhance MediaGallery component (lightbox, navigation, zoom)
- ✅ Create MediaUpload component (base and Fluent)
- ✅ Implement drag-and-drop, preview, validation

**Files Created:** ✅
- ✅ `src/Broca.ActivityPub.Components/MediaUpload.razor`
- ✅ `src/Broca.ActivityPub.Components/MediaUpload.razor.css`
- ✅ `src/Broca.Web/Components/Media/FluentMediaGallery.razor`
- ✅ `src/Broca.Web/Components/Media/FluentMediaGallery.razor.css`
- ✅ `src/Broca.Web/Components/Media/FluentMediaUpload.razor`
- ✅ `src/Broca.Web/Components/Media/FluentMediaUpload.razor.css`

**Files Modified:** ✅
- ✅ `src/Broca.ActivityPub.Components/MediaGallery.razor` (lightbox, zoom, keyboard nav, mixed media, metadata)
- ✅ `src/Broca.ActivityPub.Components/MediaGallery.razor.css`
- ⚠️ `src/Broca.Web/Components/Media/FluentMediaUpload.razor` (fixed invalid FluentUI enum values post-interruption)

**Dependencies:** None (can start immediately)  
**Note:** Invalid enum values (`Typography.Caption`, `FontWeight.Semibold`, `Icons.Regular.Size48.FolderOpen`) fixed during WS3 completion review — see [fluent-ui-styling-guidelines.md](../docs/fluent-ui-styling-guidelines.md) for valid values.

---

### Workstream 4: Post Composition & Interaction

**Owner:** Developer D  
**Directory Focus:** `src/Broca.ActivityPub.Components/PostComposer.razor`, `src/Broca.ActivityPub.Components/InteractionBar.razor`, `src/Broca.Web/Components/Interactions/`  
**Conflict Risk:** Medium (modifying existing components)  
**Status:** ✅ **COMPLETE** (2026-02-28)

**Scope:**
- ✅ Enhance PostComposer (character counter, CW, visibility, draft auto-save, keyboard shortcuts)
- ✅ Enhance InteractionBar (bookmarks, reply toggle, optimistic UI, inline reply composer)
- ✅ Complete individual interaction buttons (LikeButton, BookmarkButton, ShareButton)
- ✅ Enhance FluentPostComposer and FluentInteractionBar
- ✅ Create ReplyButton with inline compose support
- ✅ Create MoreOptionsMenu (mute, block, report, copy link)

**Files Created:** ✅
- ✅ `src/Broca.ActivityPub.Components/ReplyButton.razor`
- ✅ `src/Broca.Web/Components/Interactions/LikeButton.razor`
- ✅ `src/Broca.Web/Components/Interactions/BookmarkButton.razor`
- ✅ `src/Broca.Web/Components/Interactions/ShareButton.razor`
- ✅ `src/Broca.Web/Components/Interactions/MoreOptionsMenu.razor`

**Files Modified:** ✅
- ✅ `src/Broca.ActivityPub.Components/PostComposer.razor` (character counter, CW, visibility, drafts, Ctrl+Enter)
- ✅ `src/Broca.ActivityPub.Components/InteractionBar.razor` (bookmark, inline reply, optimistic counts)
- ✅ `src/Broca.Web/Renderers/FluentPostComposerRenderer.razor`
- ✅ `src/Broca.Web/Renderers/FluentInteractionBarRenderer.razor`
- ✅ `src/Broca.Web/Components/PostComposerDialog.razor`
- ⚠️ `src/Broca.Web/Renderers/FluentDeleteRenderer.razor` (fixed `FontWeight.Semibold` → `FontWeight.Bold`)

**Dependencies:** 
- Should coordinate with Workstream 3 for media upload integration in PostComposer
- Can work in parallel but plan integration point

---

### Workstream 5: Feeds & Timelines

**Owner:** Developer E  
**Directory Focus:** `src/Broca.ActivityPub.Components/ActivityFeed.razor`, `src/Broca.ActivityPub.Components/ReplyThread.razor`, `src/Broca.Web/Pages/` (timeline pages)  
**Conflict Risk:** Medium (modifying existing feed)  
**Status:** 🟡 **PARTIALLY COMPLETE** (2026-02-28)

**Scope:**
- ✅ ActivityFeed: "Load newer" with count, empty state, auto-refresh, error recovery
- ✅ ReplyThread: collapse/expand, auto-collapse deep threads, load more replies
- ✅ ConversationView: full conversation display with parent chain + replies — **COMPLETE** (2026-02-28)
- ⚠️ ActivityFeed: feed filtering by type/actor/date — _FeedFilter component not yet created_
- ⚠️ ReplyThread: context loading (parent posts) — _TODO comment in code; ConversationView provides this_
- ⚠️ Timeline page variants — _Pages/Timeline/ directory does not exist_

**Files Created:** ✅
- ✅ `src/Broca.ActivityPub.Components/ConversationView.razor` (full conversation display)
- ✅ `src/Broca.ActivityPub.Components/ConversationView.razor.css`
- ✅ `src/Broca.Web/Components/Conversations/FluentConversationView.razor`
- ✅ `src/Broca.Web/Components/Conversations/FluentConversationView.razor.css`
- ✅ `src/Broca.Web/Components/Conversations/_Imports.razor`

**Remaining Work:**
- `src/Broca.ActivityPub.Components/FeedFilter.razor` (filter by type, actor, date)
- `src/Broca.Web/Pages/Timeline/HomeTimeline.razor`
- `src/Broca.Web/Pages/Timeline/LocalTimeline.razor`
- `src/Broca.Web/Pages/Timeline/FederatedTimeline.razor`
- `src/Broca.Web/Pages/Timeline/HashtagTimeline.razor`
- Wire `FeedFilter` into `ActivityFeed` for type/actor/date filtering
- Implement ReplyThread context loading (parent post chain)
- Wire ConversationView into existing pages (e.g., Post.razor)

**Dependencies:** 
- WS1/WS2 complete — all renderers available ✅

---

### Workstream 6: Profiles & Social

**Owner:** Developer F  
**Directory Focus:** `src/Broca.ActivityPub.Components/ActorProfile.razor`, `src/Broca.ActivityPub.Components/ActorBrowser.razor`, `src/Broca.Web/Components/ProfileEditor.razor`, `src/Broca.Web/Pages/` (profile-related)  
**Conflict Risk:** Medium (modifying existing components)  
**Status:** 🟡 **PARTIALLY COMPLETE** (2026-02-28)

**Scope:**
- ✅ ActorProfile: statistics parameter/structure (`ShowStatistics`, `LoadStatisticsAsync`)
- ⚠️ ActorProfile: statistics load has TODO — actual follower/post counts not implemented
- ⚠️ ActorProfile: pinned posts, banner image, verification links — _not yet implemented_
- ✅ ProfileEditor exists in `src/Broca.Web/Components/ProfileEditor.razor`
- ✅ ActorBrowser exists
- ✅ `src/Broca.Web/Components/Users/UserCard.razor` exists (basic user card)
- ⚠️ RelationshipsList component — _not yet created_

**Remaining Work:**
- `src/Broca.ActivityPub.Components/RelationshipsList.razor` (blocks, mutes list)
- Implement `LoadStatisticsAsync` — fetch follower/following/post counts from collections
- ActorProfile banner image and pinned posts display
- ActorProfile verification link display
- ProfileEditor: avatar/banner upload via MediaUpload, custom fields editor
- ActorBrowser: debounced search, follow/unfollow from results

**Dependencies:** 
- WS3 complete — avatar/banner upload available via MediaUpload

---

### Workstream 7: Notifications & Search

**Owner:** Developer G  
**Directory Focus:** `src/Broca.ActivityPub.Components/NotificationFeed.razor`, `src/Broca.ActivityPub.Components/Search/` (new), `src/Broca.Web/Pages/`  
**Conflict Risk:** Low (mostly new components and enhancements to isolated component)  
**Status:** 🟡 **PARTIALLY COMPLETE** (2026-02-28)

**Scope:**
- ✅ NotificationFeed: filtering by type (All, Mentions, Likes, Boosts, Follows), unread count badge, mark-all-read
- ⚠️ NotificationFeed: grouping by type, bulk delete — _not yet implemented_
- ⚠️ NotificationIndicator — `src/Broca.Web/Components/Notifications/` directory does not exist
- ⚠️ SearchBar component — _not yet created_
- ⚠️ HashtagFeed component — _not yet created_
- ⚠️ Explore page — _exists but likely needs trending/suggestions work_

**Remaining Work:**
- `src/Broca.ActivityPub.Components/SearchBar.razor` (actors, hashtags, posts)
- `src/Broca.ActivityPub.Components/HashtagFeed.razor`
- `src/Broca.Web/Components/Notifications/NotificationIndicator.razor` (unread badge, quick dropdown)
- Wire NotificationIndicator into `src/Broca.Web/Layout/NavMenu.razor`
- NotificationFeed: group notifications by type
- NotificationFeed: bulk delete/dismiss
- Explore page: trending hashtags, suggested actors

**Dependencies:** 
- WS1/WS2 complete — renderers available for notification content

---

### Workstream 8: Polish, UX & Performance

**Owner:** Developer H (or rotating team)  
**Directory Focus:** All components (CSS, accessibility, performance)  
**Conflict Risk:** High (touches many files) - coordinate carefully  
**Status:** ❌ **NOT STARTED** (2026-02-28)

**Scope:**
- UI/UX polish (consistent spacing, typography, colors, dark mode)
- Animations and transitions
- Responsive design (mobile, tablet, desktop)
- Performance optimization (virtualization audit, lazy loading, caching)
- Accessibility audit and enhancements
- Documentation
- Testing

**Work Organization:**
- This workstream should work in short sprints coordinated with other teams
- Focus on completing files after primary workstreams finish with them
- Use separate CSS files when possible to avoid conflicts
- Coordinate with all teams on shared files

**Files to Modify:**
- CSS files across all components
- All `.razor` files for accessibility improvements
- Performance-related code changes

**Files to Create:**
- `src/Broca.Web/wwwroot/css/themes/dark.css` (dark mode)
- `src/Broca.Web/wwwroot/css/animations.css`
- `src/Broca.Web/wwwroot/css/responsive.css`
- Documentation files
- Test files

**Dependencies:** 
- Should start after Workstreams 1-4 are well underway
- Coordinate timing with all other workstreams

---

### Additional Parallel Work: Advanced Features (Phase 4)

Once core functionality is in place, additional workstreams can be spun up for advanced features. These are largely independent:

**Workstream 9: Lists & Collections**
- **Owner:** Developer I
- **Focus:** ListManager, BookmarksList, list feeds
- **Risk:** Low (new components)
- **Timing:** After Phase 1-2 complete

**Workstream 10: Moderation & Safety**
- **Owner:** Developer J
- **Focus:** ContentFilterSettings, ReportDialog, filtering systems
- **Risk:** Low (new components)
- **Timing:** After Phase 1-2 complete

These can be standalone tasks assigned as needed.

---

### Conflict Resolution Strategy

**High-Risk Conflict Zones:**

1. **`FluentRendererExtensions.cs`** (Workstreams 1-2)
   - **Strategy:** Workstream 1 owns content type renderers (Question, Event, Place, etc.), Workstream 2 owns activity renderers (Update, Delete, etc.)
   - Use separate method calls or line ranges
   - Coordinate merge timing
   - Create PRs in sequence rather than parallel for this file

2. **Shared Components** (PostComposer, InteractionBar)
   - **Strategy:** Workstream 4 has exclusive ownership during enhancement phase
   - Other workstreams should not touch these until Workstream 4 completes
   - Plan integration points in advance

3. **CSS Files** (Workstream 8 touches everything)
   - **Strategy:** Workstream 8 creates separate files (themes, animations) when possible
   - Coordinate timing - polish CSS after component functionality is complete
   - Use CSS modules or scoped styles to reduce conflicts

4. **Layout/Navigation Components** (Workstream 7 adds notification indicator)
   - **Strategy:** Coordinate with team before modifying shared layout files
   - Use separate components that are imported rather than inline additions

**Coordination Meetings:**
- Weekly sync to review progress and upcoming conflicts
- Coordinate PR merge order for conflicting files
- Use feature flags for partially complete features

**Git Strategy:**
- Each workstream creates sub-branches from `feature/web-client-enhancements`
- Branch naming: `feature/web-client-enhancements/workstream-N-description`
- Merge to main feature branch frequently (daily if possible)
- Keep PRs small and focused

---

## Phase 1: Core Component Foundation

**Priority:** High  
**Goal:** Establish solid base components with complete functionality

### 1.1 Object Rendering System

**Status:** ✅ Complete (WS1 + WS2)  
**Gaps:** ArticleRenderer and VideoRenderer enhancements deferred

#### Tasks:
- [x] **Audit existing renderers** ✅

- [x] **Base Component Renderers** (`Broca.ActivityPub.Components/Renderers/`)
  - [x] Implement `QuestionRenderer.razor` (polls/questions with voting)
  - [x] Implement `EventRenderer.razor` (events with time/location)
  - [x] Implement `PlaceRenderer.razor` (location display)
  - [x] Implement `AudioRenderer.razor` (audio player)
  - [ ] Complete `ArticleRenderer.razor` (long-form content) _(deferred)_
  - [ ] Enhance `VideoRenderer.razor` (video player controls) _(deferred)_
  - [x] Implement `PageRenderer.razor` (web pages)

- [x] **Fluent UI Renderers** (`Broca.Web/Renderers/`)
  - [x] `FluentQuestionRenderer.razor` (styled polls with FluentUI components)
  - [x] `FluentEventRenderer.razor` (event cards with calendar integration)
  - [x] `FluentPlaceRenderer.razor` (location cards with map preview)
  - [x] `FluentAudioRenderer.razor` (styled audio player)
  - [ ] Update `FluentVideoRenderer.razor` (HTML5 video with Fluent controls) _(deferred)_
  - [x] Register all new renderers in `FluentRendererExtensions.cs`

- [x] **Activity Renderers** ✅ (WS2 complete)
  - [x] Base: `UpdateRenderer.razor` (show what was updated)
  - [x] Base: `DeleteRenderer.razor` (tombstone display)
  - [x] Base: `BlockRenderer.razor` (block notification)
  - [x] Base: `UndoRenderer.razor` (undo display)
  - [x] Fluent: `FluentUpdateRenderer.razor`, `FluentDeleteRenderer.razor`, `FluentBlockRenderer.razor`, `FluentUndoRenderer.razor`

### 1.2 Interaction Components

**Status:** ✅ Complete (WS4)  
**Gaps:** None

#### Tasks:
- [x] **InteractionBar Enhancement** (`InteractionBar.razor`) ✅
  - [x] Add bookmark functionality
  - [x] Implement share/announce with options (direct boost)
  - [x] Add reply count fetching from `replies` collection
  - [x] Implement optimistic UI updates
  - [x] Add loading states for each action
  - [x] Add error handling and retry logic
  - [x] Support customizable button order and visibility

- [x] **Fluent InteractionBar** (`FluentInteractionBarRenderer.razor`) ✅
  - [x] Add Fluent icons for all actions
  - [x] Implement hover tooltips
  - [x] Add subtle animations for state changes
  - [x] Style active/inactive states

- [x] **Individual Interaction Buttons** (Web Components) ✅
  - [x] `LikeButton.razor` with state management
  - [x] `BookmarkButton.razor` with state management
  - [x] `ShareButton.razor` with boost/quote options
  - [x] `ReplyButton.razor` with inline composer option
  - [x] `MoreOptionsMenu.razor` (report, mute, block, copy link)

### 1.3 Post Composition

**Status:** ✅ Complete (WS4) — mention/hashtag autocomplete and link preview deferred  
**Gaps:** Mention autocomplete, hashtag autocomplete, link preview, scheduling not yet implemented

#### Tasks:
- [x] **PostComposer Base Component** (`PostComposer.razor`) ✅
  - [x] Add character counter with limit warnings
  - [ ] Implement mention autocomplete (search actors) _(not yet implemented)_
  - [ ] Implement hashtag autocomplete/suggestions _(not yet implemented)_
  - [x] Add emoji picker integration point
  - [x] Implement content warnings (CW/spoiler)
  - [x] Add poll/question creation
  - [ ] Support scheduling posts (draft with future publish date) _(not yet implemented)_
  - [x] Implement draft auto-save
  - [ ] Add link preview generation _(not yet implemented)_
  - [x] Support multiple visibility levels (public, unlisted, followers, direct)
  - [ ] Add audience selector (specific actors/collections) _(not yet implemented)_

- [x] **FluentPostComposer** (`FluentPostComposerRenderer.razor`) ✅
  - [x] Rich text toolbar
  - [x] Emoji picker with FluentUI modal
  - [ ] Visual attachment preview with remove buttons _(not yet implemented)_
  - [x] Fluent styling for all elements
  - [x] Accessibility improvements (ARIA labels, keyboard shortcuts)

- [x] **PostComposerDialog** (`Broca.Web/Components/PostComposerDialog.razor`) ✅
  - [x] Enhance modal dialog for composing
  - [ ] Add minimize/expand functionality _(not yet implemented)_
  - [ ] Support multiple draft posts _(not yet implemented)_
  - [x] Implement dialog state persistence

### 1.4 Media Handling

**Status:** ✅ Complete (WS3)  
**Gaps:** Touch gestures / image cropping deferred to WS8

#### Tasks:
- [x] **MediaGallery Component** (`MediaGallery.razor`) ✅
  - [x] Add lightbox/fullscreen view
  - [ ] Implement swipe navigation _(deferred to WS8)_
  - [x] Add zoom controls
  - [x] Support mixed media types (images, videos)
  - [x] Add keyboard navigation
  - [x] Display media metadata (alt text, dimensions, type)

- [x] **MediaUpload Component** (`Broca.ActivityPub.Components`) ✅
  - [x] Create `MediaUpload.razor` base component
  - [x] Support drag-and-drop upload
  - [x] Image preview before upload
  - [x] Basic client-side validation (size, type)
  - [x] Upload progress indication
  - [x] Support multiple file selection
  - [x] Alt text input for accessibility

- [x] **FluentMediaGallery** (`Broca.Web`) ✅
  - [x] Fluent UI styled lightbox
  - [x] Smooth transitions and animations
  - [ ] Touch gesture support _(deferred to WS8)_

- [x] **FluentMediaUpload** (`Broca.Web`) ✅
  - [x] Fluent UI file picker
  - [x] Progress bars with FluentUI components
  - [ ] Image cropping/editing tools _(stretch goal, deferred)_

---

## Phase 2: Feed and Timeline Experience

**Priority:** High  
**Goal:** Create engaging, performant activity feeds

### 2.1 ActivityFeed Enhancements

**Status:** 🟡 Partially Complete (WS5)  
**Gaps:** FeedFilter and timeline page variants not yet created

#### Tasks:
- [x] **ActivityFeed Base** (`ActivityFeed.razor`)
  - [ ] Add filter support (by type, by actor, by date range) _(FeedFilter component not yet created)_
  - [ ] Implement search within feed _(not yet implemented)_
  - [x] Add "Load newer" button with count indicator
  - [x] Improve auto-refresh UX (configurable interval, pause when scrolled)
  - [x] Add empty state handling (no activities)
  - [x] Implement error recovery (retry failed loads)
  - [x] Support mixed activity/object feeds
  - [ ] Add "scroll to top" button _(not yet implemented)_

- [ ] **Feed Filtering Component** (New) ❌
  - [ ] Create `FeedFilter.razor` base component
  - [ ] Support filtering by activity type (Create, Announce, Like, etc.)
  - [ ] Support filtering by content type (Note, Article, Image, etc.)
  - [ ] Date range filtering
  - [ ] Actor filtering (show only specific actors)

- [ ] **Timeline Variants** (Web-specific pages) ❌
  - [ ] Home timeline (following + algorithms)
  - [ ] Local timeline (same server)
  - [ ] Federated timeline (known servers)
  - [ ] Hashtag timeline
  - [ ] List timelines (custom actor lists)

### 2.2 Thread and Conversation View

**Status:** � **MOSTLY COMPLETE** (2026-02-28)  
**Gaps:** Context loading in ReplyThread; thread metadata/highlighting

#### Tasks:
- [x] **ReplyThread Enhancement** (`ReplyThread.razor`) ✅
  - [x] Improve nested threading display (indentation, lines)
  - [x] Add "load more replies" for long threads
  - [ ] Implement context loading (parent posts) _(TODO in code - however ConversationView provides this)_
  - [ ] Add thread muting/following _(not yet implemented)_
  - [x] Support collapsing/expanding threads (auto-collapse at configurable depth)
  - [ ] Highlight OP (original poster) in thread _(not yet implemented)_
  - [ ] Add thread summary/metadata (reply count, participants) _(not yet implemented)_

- [x] **ConversationView Component** ✅ **COMPLETE** (2026-02-28)
  - [x] Create `ConversationView.razor` for full conversation display
  - [x] Show all participants
  - [x] Timeline/chronological and threaded views
  - [x] Support parent chain traversal (loads conversation context)
  - [x] Focal post highlighting
  - [x] Integration with ReplyThread for children
  - **Files:** `ConversationView.razor`, `ConversationView.razor.css`, `FluentConversationView.razor`, `FluentConversationView.razor.css`

- [x] **Fluent Thread Rendering** ✅
  - [x] Visual thread lines connecting replies (in FluentConversationView)
  - [x] Timeline markers for chronological view
  - [x] Focal post visual emphasis

---

## Phase 3: User Experience and Interactions

**Priority:** Medium  
**Goal:** Polish interactions and add social features

### 3.1 Actor and Profile Features

**Status:** 🟡 Partially Complete (WS6)  
**Gaps:** Statistics not fully loaded; pinned posts, banner, RelationshipsList missing

#### Tasks:
- [x] **ActorProfile Enhancement** (`ActorProfile.razor`) — partial
  - [x] Add statistics (structure/parameter in place; `LoadStatisticsAsync` has TODO — counts not loaded)
  - [ ] Show pinned posts _(not yet implemented)_
  - [ ] Display custom fields/metadata _(not yet implemented)_
  - [ ] Add banner/header image support _(not yet implemented)_
  - [ ] Show verification links _(not yet implemented)_
  - [ ] Activity calendar/heatmap _(not yet implemented)_

- [x] **ProfileEditor** (`ProfileEditor.razor` in Web) ✅ (exists)
  - [ ] Avatar upload/change _(MediaUpload integration pending)_
  - [ ] Banner upload/change _(MediaUpload integration pending)_
  - [ ] Bio/summary editing (rich text) _(not yet implemented)_
  - [ ] Custom fields editor _(not yet implemented)_

- [x] **ActorBrowser** (`ActorBrowser.razor`) ✅ (exists)
  - [ ] Improve search UX (debounced search) _(not yet implemented)_
  - [ ] Quick follow/unfollow from results _(not yet implemented)_
  - [ ] Recent searches / suggested actors _(not yet implemented)_

- [ ] **Relationship Management** (New)
  - [ ] Create `RelationshipsList.razor` (blocks, mutes)
  - [ ] Mute/unmute functionality
  - [ ] Block/unblock functionality
  - [ ] Domain blocks
  - [ ] Export/import blocks/mutes

### 3.2 Notification System

**Status:** 🟡 Partially Complete (WS7)  
**Gaps:** Grouping, bulk delete, and NotificationIndicator not yet implemented

#### Tasks:
- [x] **NotificationFeed Enhancement** (`NotificationFeed.razor`) — partial
  - [ ] Group notifications by type _(not yet implemented)_
  - [x] Add filtering (mentions, likes, boosts, follows)
  - [x] Mark as read/unread
  - [x] Bulk actions: mark all read
  - [ ] Bulk delete/dismiss _(not yet implemented)_
  - [ ] Notification preferences (per type) _(not yet implemented)_

- [ ] **Notification Indicator** (New in `Broca.Web`) ❌
  - [ ] Create `NotificationIndicator.razor` for nav menu
  - [ ] Unread count badge
  - [ ] Quick notification dropdown
  - [ ] Desktop notifications integration point

- [ ] **Real-time Updates** (Future)
  - [ ] WebSocket/SignalR integration for live notifications
  - [ ] Live feed updates
  - [ ] Typing indicators (DMs)

### 3.3 Search and Discovery

**Status:** ❌ Not Started (WS7)  
**Gaps:** SearchBar, HashtagFeed not yet created; Explore page needs enhancements

#### Tasks:
- [ ] **Search Component** (New) ❌
  - [ ] Create `SearchBar.razor` base component
  - [ ] Search actors (WebFinger)
  - [ ] Search hashtags
  - [ ] Search posts/content
  - [ ] Recent searches history
  - [ ] Search suggestions

- [ ] **Explore Page** (`Broca.Web/Pages/Explore.razor`) ❌
  - [ ] Trending hashtags
  - [ ] Trending posts
  - [ ] Suggested actors to follow
  - [ ] Featured content

- [ ] **HashtagFeed Component** (New) ❌
  - [ ] Create `HashtagFeed.razor` for hashtag timelines
  - [ ] Follow/unfollow hashtags
  - [ ] Related hashtags

---

## Phase 4: Advanced Features

**Priority:** Medium-Low  
**Goal:** Add sophisticated features for power users

### 4.1 Lists and Organization

#### Tasks:
- [ ] **Lists Management** (New)
  - [ ] Create `ListManager.razor` component
  - [ ] Create/edit/delete lists
  - [ ] Add/remove actors from lists
  - [ ] List privacy settings (private/public)
  - [ ] List feed view

- [ ] **Bookmarks and Collections** (New)
  - [ ] Create `BookmarksList.razor`
  - [ ] Organize bookmarks into folders/collections
  - [ ] Search within bookmarks
  - [ ] Export bookmarks

### 4.2 Moderation and Safety

#### Tasks:
- [ ] **Content Filtering** (New)
  - [ ] Create `ContentFilterSettings.razor`
  - [ ] Keyword filtering
  - [ ] Content warning handling preferences
  - [ ] Media filtering (NSFW, sensitive)

- [ ] **Reporting** (New)
  - [ ] Create `ReportDialog.razor`
  - [ ] Report posts/actors
  - [ ] Report reasons and categories
  - [ ] Report status tracking

### 4.3 Accessibility

#### Tasks:
- [ ] **Accessibility Audit**
  - [ ] Review all components for ARIA compliance
  - [ ] Keyboard navigation testing
  - [ ] Screen reader testing
  - [ ] Color contrast validation

- [ ] **Accessibility Enhancements**
  - [ ] Add skip links
  - [ ] Improve focus indicators
  - [ ] Add keyboard shortcuts guide
  - [ ] Support reduced motion preferences
  - [ ] Implement focus trapping in modals

---

## Phase 5: Polish and Optimization

**Priority:** Medium  
**Goal:** Refine the experience and improve performance

### 5.1 UI/UX Polish

#### Tasks:
- [ ] **Visual Design Refinement**
  - [ ] Consistent spacing and typography
  - [ ] Color scheme refinement
  - [ ] Dark mode support (full theme)
  - [ ] Loading states consistency
  - [ ] Error states consistency
  - [ ] Empty states design

- [ ] **Animations and Transitions**
  - [ ] Page transitions
  - [ ] Component enter/exit animations
  - [ ] Micro-interactions (button hover, click feedback)
  - [ ] Skeleton loaders for content

- [ ] **Responsive Design**
  - [ ] Mobile layout optimization
  - [ ] Tablet layout optimization
  - [ ] Desktop layout optimization
  - [ ] Touch gesture support
  - [ ] Bottom navigation for mobile

### 5.2 Performance Optimization

#### Tasks:
- [ ] **Component Performance**
  - [ ] Virtualization audit (ensure all large lists use it)
  - [ ] Lazy loading for below-fold content
  - [ ] Image lazy loading
  - [ ] Component memoization where appropriate
  - [ ] Reduce unnecessary re-renders

- [ ] **Data Management**
  - [ ] Implement caching strategy
  - [ ] Prefetch adjacent content
  - [ ] Optimize API calls (batching, deduplication)
  - [ ] Service worker for offline support

- [ ] **Bundle Optimization**
  - [ ] Code splitting
  - [ ] Tree shaking audit
  - [ ] Lazy load feature modules
  - [ ] Minimize CSS/JS bundles

### 5.3 Developer Experience

#### Tasks:
- [ ] **Documentation**
  - [ ] Component API documentation
  - [ ] Usage examples for each component
  - [ ] Styling/customization guide
  - [ ] Architecture decision records

- [ ] **Testing**
  - [ ] Unit tests for component logic
  - [ ] Integration tests for key workflows
  - [ ] Visual regression tests
  - [ ] Accessibility tests

- [ ] **Storybook/Component Gallery** (Optional)
  - [ ] Setup Storybook for Components library
  - [ ] Document each component with examples
  - [ ] Interactive playground

---

## Implementation Guidelines

### Component Creation Checklist

When creating a new component:

1. **Base Component** (`Broca.ActivityPub.Components`)
   - [ ] Create `.razor` file with minimal styling
   - [ ] Define all `[Parameter]` properties with XML docs
   - [ ] Implement core logic and state management
   - [ ] Expose `RenderFragment<T>` templates for customization
   - [ ] Add minimal CSS (structural only, no decorative styling)
   - [ ] Handle loading, error, and empty states

2. **Fluent UI Component** (`Broca.Web/Renderers` or `Components`)
   - [ ] Create corresponding Fluent implementation
   - [ ] Use Fluent UI components and patterns
   - [ ] Add full styling and theming
   - [ ] Register renderer in `FluentRendererExtensions.cs` (if applicable)

3. **Documentation**
   - [ ] Add XML documentation to public properties/methods
   - [ ] Create usage example (inline or separate)
   - [ ] Update relevant README if needed

4. **Testing**
   - [ ] Write unit tests for component logic
   - [ ] Test with various parameter combinations
   - [ ] Test error conditions

### Code Standards

- **Keep components small**: Each component should have a single responsibility
- **Use composition**: Build complex UIs from simple components
- **Template everything**: Expose templates for all customizable parts
- **Type safety**: Leverage ActivityStreams types, avoid `dynamic` or `object`
- **Async patterns**: Use `async`/`await` properly, show loading states
- **Error handling**: Always handle errors gracefully, provide feedback
- **Accessibility**: ARIA labels, semantic HTML, keyboard support
- **Performance**: Use virtualization for large lists, lazy loading for media

### Naming Conventions

- **Base components**: `{Feature}.razor` (e.g., `PostComposer.razor`)
- **Fluent renderers**: `Fluent{Feature}Renderer.razor` (e.g., `FluentPostComposerRenderer.razor`)
- **Services**: `{Feature}Service.cs` (e.g., `ActorResolutionService.cs`)
- **CSS classes**: `activitypub-{component}` prefix for base, `fluent-{component}` for Fluent

---

## Milestones

### Milestone 1: Core Functionality (Phase 1)
**Target:** 4-6 weeks  
**Deliverables:**
- Complete object renderer coverage
- Enhanced interaction components
- Improved post composition
- Media handling (display + upload)

### Milestone 2: Feed Experience (Phase 2)
**Target:** 2-3 weeks  
**Deliverables:**
- Enhanced activity feeds with filtering
- Improved thread/conversation view
- Timeline variants

### Milestone 3: Social Features (Phase 3)
**Target:** 3-4 weeks  
**Deliverables:**
- Enhanced profiles and relationships
- Notification system improvements
- Search and discovery features

### Milestone 4: Advanced & Polish (Phases 4-5)
**Target:** 4-5 weeks  
**Deliverables:**
- Lists and organization
- Moderation tools
- Accessibility improvements
- Performance optimizations
- UI/UX polish

---

## Success Criteria

### Functional
- [ ] All common ActivityStreams types render correctly
- [ ] Users can post various content types (text, images, links, polls)
- [ ] Full interaction capabilities (like, boost, reply, bookmark)
- [ ] Working profile management
- [ ] Functional notification system
- [ ] Comprehensive search

### Non-Functional
- [ ] Fast load times (<3s initial, <1s navigation)
- [ ] Smooth scrolling and interactions (60fps)
- [ ] Responsive on all device sizes
- [ ] WCAG 2.1 AA accessibility compliance
- [ ] Works offline (cached content)

### Developer Experience
- [ ] Clear component API
- [ ] Comprehensive documentation
- [ ] Easy to customize and extend
- [ ] Test coverage >80% for core logic

---

## Notes

### Design Principles

1. **Progressive Enhancement**: Start with working HTML, enhance with JavaScript
2. **Mobile-First**: Design for mobile, scale up to desktop
3. **Accessibility-First**: Build accessible from the start, not bolt on later
4. **Performance-First**: Consider performance implications of every feature
5. **Minimal by Default**: Avoid unnecessary complexity, add features intentionally

### Open Questions

- Should we support PWA features (offline, install prompt, push notifications)?
- What level of customization should the Components library expose?
- Should we build a theme system for the Web client?
- Do we need a state management library (Fluxor, etc.) or is built-in state sufficient?
- Should we implement a DM/direct messaging UI?

### Future Considerations

- OAuth authentication (replacing API keys)
- Multi-account support
- Cross-posting to other platforms
- Analytics and insights
- Scheduling and automation
- Advanced moderation tools (AI-assisted filtering)
- Federation/server management UI (admin tools)

---

## Revision History

| Date | Version | Notes |
|------|---------|-------|
| 2026-02-28 | 1.0 | Initial plan created |
| 2026-02-28 | 1.1 | Added parallel development workstream organization |
| 2026-02-28 | 1.2 | WS3 (Media) and WS4 (Post/Interaction) marked complete; fixed invalid FluentUI enum values in FluentMediaUpload and FluentDeleteRenderer; created ReplyButton.razor and MoreOptionsMenu.razor |
| 2026-02-28 | 1.3 | Audit pass: WS2 confirmed complete (UpdateRenderer, DeleteRenderer, BlockRenderer, UndoRenderer + Fluent counterparts all created and registered); WS5/WS6/WS7 updated with accurate partial-complete status; Phase 1–3 task checklists updated to reflect actual implementation state |

---

## Quick Reference: Workstream Summary

| Workstream | Owner | Primary Focus | Conflict Risk | Status |
|------------|-------|---------------|---------------|--------|
| **WS1: Object Renderers** | Dev A | Content type renderers (Question, Event, Place, Audio, Page) | Low | ✅ Complete |
| **WS2: Activity Renderers** | Dev B | Activity renderers (Update, Delete, Block, Undo) | Low | ✅ Complete |
| **WS3: Media & Attachments** | Dev C | MediaGallery, MediaUpload, drag-and-drop | Low | ✅ Complete |
| **WS4: Post & Interaction** | Dev D | PostComposer, InteractionBar enhancements | Medium | ✅ Complete (mention/hashtag autocomplete, link preview deferred) |
| **WS5: Feeds & Timelines** | Dev E | ActivityFeed, ReplyThread, timeline pages | Medium | 🟡 Partial — FeedFilter, ConversationView, Timeline pages remaining |
| **WS6: Profiles & Social** | Dev F | ActorProfile, ProfileEditor, relationships | Medium | 🟡 Partial — RelationshipsList, statistics load, banner/pinned, ProfileEditor integrations remaining |
| **WS7: Notifications & Search** | Dev G | NotificationFeed, SearchBar, HashtagFeed, Explore | Low | 🟡 Partial — NotificationIndicator, SearchBar, HashtagFeed, grouping, Explore remaining |
| **WS8: Polish & Performance** | Dev H | CSS, accessibility, performance, testing | High | ❌ Not started |

**Color Key:**
- 🟢 **Low Risk**: Independent work, mostly new files
- 🟡 **Medium Risk**: Modifies existing files, coordinate timing
- 🔴 **High Risk**: Touches many files, requires close coordination
