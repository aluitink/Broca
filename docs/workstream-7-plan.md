# Workstream 7: Notifications & Search — Implementation Plan

**Branch:** `feature/web-client-enhancements`  
**Created:** February 28, 2026  
**Status:** Planning Phase  
**Owner:** Developer G  
**Conflict Risk:** Low (mostly new components and enhancements to isolated components)

---

## Current State

### Done
- **`NotificationFeed.razor`** — filtering by type (All/Mentions/Likes/Boosts/Follows), unread count badge, mark-as-read (individual + all), auto-refresh with configurable interval, pagination (load more), notification parsing from inbox activities
- **`Explore.razor`** page — actor search via `ActorBrowser`, in-memory recent search history (last 10)
- **`ActorBrowser.razor`** — WebFinger-based actor lookup with debounced input, search history, templatable

### Not Done
- Notification grouping by type (e.g. "Alice and 3 others liked your post")
- Bulk dismiss/delete notifications
- Persistent notification read state (currently in-memory only)
- `NotificationIndicator` component for NavMenu (unread badge)
- `SearchBar` component (unified search across actors, content, hashtags)
- `HashtagFeed` component
- Explore page enhancements (trending hashtags, suggested actors)