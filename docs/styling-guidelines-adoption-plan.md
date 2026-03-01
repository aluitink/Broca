# Styling Guidelines Adoption Plan

**Created:** February 28, 2026  
**Status:** In Progress  
**Scope:** `Broca.Web/Renderers/` and related CSS files

This document outlines a plan for bringing existing Fluent UI renderers into compliance with the [Fluent UI Styling Guidelines](fluent-ui-styling-guidelines.md).

---

## Executive Summary

The codebase has **40 renderer files** (21 `.razor` + 19 `.razor.css`) with varying levels of compliance. Most renderers follow the general structure but need targeted improvements in:

1. CSS organization (inline styles → scoped CSS)
2. Consistent class naming conventions
3. Accessibility enhancements
4. Loading/error state patterns
5. Responsive design coverage

**Estimated Effort:** ~3-4 days of focused work

---

## Current State Assessment

### Renderer Inventory

| Renderer | Has CSS File | Inline Styles | Uses Design Tokens | Compliance Level |
|----------|--------------|---------------|-------------------|------------------|
| FluentNoteRenderer | ✅ | Minimal | ✅ | 🟢 High |
| FluentArticleRenderer | ✅ | Moderate | ✅ | 🟡 Medium |
| FluentActorRenderer | ✅ | Minimal | ✅ | 🟢 High |
| FluentActivityRenderer | ✅ | Minimal | ✅ | 🟢 High |
| FluentAnnounceRenderer | ✅ | Minimal | ✅ | 🟢 High |
| FluentLikeRenderer | ✅ | Minimal | ✅ | 🟢 High |
| FluentFollowRenderer | ✅ | Minimal | ✅ | 🟢 High |
| FluentInteractionBarRenderer | ✅ | None | ✅ | 🟢 High |
| FluentEventRenderer | ✅ | None | ✅ | 🟢 High |
| FluentQuestionRenderer | ✅ | None | ✅ | 🟢 High |
| FluentImageRenderer | ❌ | Moderate | Partial | 🟡 Medium |
| FluentVideoRenderer | ❌ | Moderate | Partial | 🟡 Medium |
| FluentAudioRenderer | ✅ | None | ✅ | 🟢 High |
| FluentDocumentRenderer | ❌ | Unknown | Unknown | 🟡 Medium |
| FluentLinkRenderer | ❌ | Unknown | Unknown | 🟡 Medium |
| FluentPlaceRenderer | ✅ | Minimal | ✅ | 🟢 High |
| FluentPageRenderer | ✅ | Minimal | ✅ | 🟡 Medium |
| FluentDeleteRenderer | ✅ | Minimal | ✅ | 🟢 High |
| FluentBlockRenderer | ✅ | Minimal | ✅ | 🟢 High |
| FluentUndoRenderer | ✅ | Minimal | ✅ | 🟢 High |
| FluentUpdateRenderer | ✅ | Minimal | ✅ | 🟢 High |
| FluentCreateRenderer | ✅ | Minimal | ✅ | 🟢 High |
| FluentPostComposerRenderer | ✅ | Minimal | ✅ | 🟢 High |
| FluentAddContentButtonRenderer | ✅ | Minimal | ✅ | 🟢 High |

### Global CSS Files

| File | Purpose | Status |
|------|---------|--------|
| `wwwroot/css/app.css` | App-level styles | ✅ Well-organized |
| `wwwroot/css/renderers.css` | Shared renderer patterns | ⚠️ Needs consolidation |

---

## Gap Analysis

### 1. **CSS Organization Issues** ✅ Resolved (2026-02-28)

**Problem:** FluentEventRenderer and FluentQuestionRenderer use inline `<style>` blocks instead of scoped CSS files.

**Resolution:** Extracted all inline styles to scoped `.razor.css` files and renamed root classes to `fluent-event-card` / `fluent-question-card`. Also extracted inline `<style>` block from `FluentAudioRenderer`.

---

### 2. **Missing Scoped CSS Files** ⚠️ Partially Resolved (2026-02-28)

**Problem:** Several renderers lack `.razor.css` files for component isolation.

**Resolved:**
- ✅ `FluentEventRenderer.razor.css` — created
- ✅ `FluentQuestionRenderer.razor.css` — created
- ✅ `FluentAudioRenderer.razor.css` — created

**Still Needed:**
- `FluentImageRenderer.razor.css`
- `FluentVideoRenderer.razor.css`
- `FluentDocumentRenderer.razor.css`
- `FluentLinkRenderer.razor.css`

**Action:** Create scoped CSS files with properly prefixed class names.

---

### 3. **Inconsistent CSS Class Naming**

**Problem:** Class names don't consistently follow the `fluent-{type}-{element}` convention.

**Examples:**
```css
/* Current (inconsistent) */
.event-card { }
.event-header { }
.question-options { }

/* Guideline (consistent) */
.fluent-event-card { }
.event-header { }
.fluent-question-card { }
.question-options { }
```

**Resolved:**
- ✅ FluentEventRenderer — root class renamed to `fluent-event-card`
- ✅ FluentQuestionRenderer — root class renamed to `fluent-question-card`
- ✅ FluentAudioRenderer — root class renamed to `fluent-audio-card`

**Still Affected:**
- Several others with mixed patterns

**Action:** Standardize to `fluent-{type}-card` for root, `{type}-{element}` for children.

---

### 4. **Inline Style Attributes**

**Problem:** Many renderers use inline `style=""` attributes instead of CSS classes.

**Examples Found:**
```razor
<!-- Current -->
<FluentDivider Style="margin: 0;" />
<FluentStack ... Style="flex: 1;">
<img style="width: 100%; max-height: 400px; object-fit: cover; border-radius: 8px;" />

<!-- Preferred -->
<FluentDivider Class="section-divider" />
<FluentStack ... Class="flex-fill">
<img class="featured-image" />
```

**Files With Heavy Inline Styles:**
- FluentArticleRenderer (5+ inline styles)
- FluentImageRenderer (4+ inline styles)
- FluentVideoRenderer (3+ inline styles)
- FluentActorRenderer (3+ inline styles)
- FluentNoteRenderer (minimal, acceptable)

**Action:** Move repeated inline styles to CSS classes; keep one-off dynamic styles inline.

---

### 5. **Accessibility Gaps**

**Problem:** Some components lack proper ARIA attributes and semantic HTML.

**Missing Patterns:**
| Pattern | Current State | Required |
|---------|--------------|----------|
| Icon button labels | Some have `Title`, missing `aria-label` | Add `aria-label` to all icon-only buttons |
| Loading states | No `aria-busy` | Add `aria-busy="true"` during loading |
| Time elements | ✅ Using `<time datetime="">` | Good |
| Image alt text | Mostly present | Audit for completeness |

**Files to Audit:**
- All renderers with `FluentButton` (icon-only variants)
- FluentInteractionBarRenderer (action buttons)
- FluentActivityRenderer, FluentNoteRenderer (loading states)

**Action:** Add accessibility audit checklist item to each renderer PR.

---

### 6. **Loading & Error State Inconsistency**

**Problem:** Loading and error states aren't implemented consistently across renderers.

**Current Pattern (Varies):**
```razor
<!-- FluentAnnounceRenderer - has loading -->
@if (isLoading)
{
    <FluentProgressRing Style="width: 24px; height: 24px;" />
}

<!-- Many renderers - no loading state -->
@if (Object != null)
{
    <!-- content -->
}
```

**Guideline Pattern:**
```razor
@if (isLoading)
{
    <FluentSkeleton Style="height: 100px;" />
}
else if (hasError)
{
    <FluentMessageBar Intent="MessageIntent.Error">@errorMessage</FluentMessageBar>
}
else if (Object != null)
{
    <!-- content -->
}
else
{
    <!-- empty state -->
}
```

**Files Needing Loading/Error States:**
- FluentNoteRenderer
- FluentArticleRenderer
- FluentActorRenderer
- FluentEventRenderer
- FluentQuestionRenderer

**Action:** Add consistent loading/error state handling to renderers that fetch data.

---

### 7. **Responsive Design Coverage**

**Problem:** Only FluentInteractionBarRenderer has responsive media queries.

**Current State:**
```css
/* FluentInteractionBarRenderer.razor.css - has responsive */
@media (max-width: 640px) { ... }

/* Most other renderers - no breakpoints */
```

**Guideline Breakpoints:**
- Mobile: < 600px
- Tablet: 600px - 1024px
- Desktop: > 1024px

**Priority Components for Responsive Updates:**
1. FluentNoteRenderer (content cards)
2. FluentArticleRenderer (long-form content)
3. FluentActorRenderer (profile cards)
4. FluentEventRenderer (event details)

**Action:** Add responsive styles to high-priority renderers.

---

### 8. **renderers.css Consolidation**

**Problem:** `wwwroot/css/renderers.css` contains duplicated/stale styles that overlap with scoped CSS.

**Current Size:** 252 lines

**Issues:**
- Some classes defined here AND in scoped CSS (e.g., `.note-content`, `.note-header`)
- Unclear which styles are actively used
- May cause specificity conflicts

**Action:** Audit renderers.css, remove duplicates, keep only truly shared patterns.

---

## Implementation Plan

### Phase 1: Critical Fixes (Day 1)

**Goal:** Fix the most visible issues and establish patterns.

| Task | Files | Est. Time |
|------|-------|-----------|
| ~~Extract FluentEventRenderer inline styles to CSS~~ ✅ | `FluentEventRenderer.razor`, `FluentEventRenderer.razor.css` | Done |
| ~~Extract FluentQuestionRenderer inline styles to CSS~~ ✅ | `FluentQuestionRenderer.razor`, `FluentQuestionRenderer.razor.css` | Done |
| ~~Standardize class names in extracted CSS~~ ✅ | Same files | Done |
| ~~Extract FluentAudioRenderer inline styles to CSS~~ ✅ | `FluentAudioRenderer.razor`, `FluentAudioRenderer.razor.css` | Done |
| Create missing CSS files (Image, Video, Document, Link) | 4 new `.razor.css` files | 1 hr |

**Deliverable:** All renderers have scoped CSS files; no inline `<style>` blocks.

---

### Phase 2: Style Standardization (Day 2)

**Goal:** Consistent styling patterns across all renderers.

| Task | Files | Est. Time |
|------|-------|-----------|
| Move inline `style=""` to CSS classes (Article) | FluentArticleRenderer | 30 min |
| Move inline `style=""` to CSS classes (Image/Video) | FluentImageRenderer, FluentVideoRenderer | 30 min |
| Move inline `style=""` to CSS classes (Actor) | FluentActorRenderer | 20 min |
| Standardize FluentDivider styling | All renderers | 20 min |
| Audit and update class naming conventions | All renderers | 1 hr |

**Deliverable:** Minimal inline styles; consistent class naming.

---

### Phase 3: Accessibility & States (Day 3)

**Goal:** WCAG 2.1 AA compliance and consistent UX.

| Task | Files | Est. Time |
|------|-------|-----------|
| Add `aria-label` to icon-only buttons | FluentInteractionBarRenderer, others | 30 min |
| Add loading state skeleton to FluentNoteRenderer | FluentNoteRenderer | 20 min |
| Add loading state to FluentArticleRenderer | FluentArticleRenderer | 20 min |
| Add error state handling to data-fetching renderers | Announce, Like, Delete, Update | 45 min |
| Add empty state pattern to feed renderers | ActivityFeed, NotificationFeed | 30 min |

**Deliverable:** Accessible interactive elements; consistent loading/error UX.

---

### Phase 4: Responsive & Polish (Day 4)

**Goal:** Mobile-friendly layouts and CSS cleanup.

| Task | Files | Est. Time |
|------|-------|-----------|
| Add responsive breakpoints to FluentNoteRenderer | CSS file | 30 min |
| Add responsive breakpoints to FluentArticleRenderer | CSS file | 30 min |
| Add responsive breakpoints to FluentActorRenderer | CSS file | 20 min |
| Audit and clean up renderers.css | `wwwroot/css/renderers.css` | 1 hr |
| Final review and testing | All | 1 hr |

**Deliverable:** Mobile-responsive renderers; clean CSS architecture.

---

## New Renderer Checklist

When creating new Fluent UI renderers, verify:

- [ ] Root element uses `FluentCard` with `Class="fluent-{type}-card"`
- [ ] Layout uses `FluentStack` with appropriate `Orientation` and `Gap`
- [ ] Actor attribution uses `FluentPersona` pattern from guidelines
- [ ] Text uses `FluentLabel` with correct `Typography` enum
- [ ] Badges use `FluentBadge` with `Appearance.Accent` or appropriate variant
- [ ] Icons use `Microsoft.FluentUI.AspNetCore.Components.Icons` with correct size
- [ ] Dividers use `FluentDivider` between major sections
- [ ] Menu uses `ObjectMenu` component for overflow actions
- [ ] Includes `AdditionalContent` render fragment parameter
- [ ] Has corresponding `.razor.css` file (no inline `<style>` blocks)
- [ ] CSS classes follow `fluent-{type}-card` / `{type}-{element}` convention
- [ ] Uses design tokens (not hard-coded colors)
- [ ] Includes loading state (`FluentSkeleton` or `FluentProgressRing`)
- [ ] Includes error state (`FluentMessageBar`)
- [ ] Icon-only buttons have `aria-label` attribute
- [ ] Images have `alt` attribute
- [ ] Time elements use `<time datetime="">`
- [ ] Responsive breakpoints for mobile (< 600px)
- [ ] Registered in `FluentRendererExtensions.cs`

---

## Files Reference

### Files to Modify

```
src/Broca.Web/Renderers/
├── FluentEventRenderer.razor           # Extract inline styles
├── FluentQuestionRenderer.razor        # Extract inline styles
├── FluentArticleRenderer.razor         # Reduce inline styles
├── FluentImageRenderer.razor           # Reduce inline styles
├── FluentVideoRenderer.razor           # Reduce inline styles
├── FluentActorRenderer.razor           # Minor cleanup
├── FluentNoteRenderer.razor            # Add loading state
├── FluentNoteRenderer.razor.css        # Add responsive
├── FluentInteractionBarRenderer.razor  # Add aria-labels
└── ... (most renderers need minor updates)

src/Broca.Web/wwwroot/css/
└── renderers.css                       # Audit and cleanup
```

### Files to Create

```
src/Broca.Web/Renderers/
├── FluentEventRenderer.razor.css       # New
├── FluentQuestionRenderer.razor.css    # New
├── FluentImageRenderer.razor.css       # New
├── FluentVideoRenderer.razor.css       # New
├── FluentAudioRenderer.razor.css       # New
├── FluentDocumentRenderer.razor.css    # New
└── FluentLinkRenderer.razor.css        # New
```

---

## Success Metrics

After completing this plan:

1. **Zero inline `<style>` blocks** in renderer files
2. **All renderers have scoped CSS files** (`.razor.css`)
3. **Consistent class naming** following `fluent-{type}-{element}` pattern
4. **< 10 inline `style=""` attributes** per renderer (for truly dynamic values only)
5. **100% ARIA coverage** on interactive elements
6. **Loading/error states** on all data-fetching renderers
7. **Responsive styles** on all primary content renderers
8. **renderers.css < 150 lines** after cleanup

---

## Appendix: Quick Reference

### Design Token Cheat Sheet

```css
/* Backgrounds */
--neutral-layer-1          /* Cards */
--neutral-layer-2          /* Nested content */
--neutral-layer-3          /* Quotes */
--neutral-layer-4          /* Footers */

/* Text */
--neutral-foreground-rest  /* Primary */
--neutral-foreground-hint  /* Secondary */

/* Accent */
--accent-fill-rest         /* Links, actions */
--error                    /* Likes, errors */

/* Borders */
--neutral-stroke-rest      /* Borders */
--neutral-stroke-divider-rest /* Dividers */

/* Sizing */
--control-corner-radius    /* 4px */
```

### Typography Quick Reference

```razor
<FluentLabel Typo="Typography.PageTitle">   <!-- Largest -->
<FluentLabel Typo="Typography.Header">      <!-- Section heads -->
<FluentLabel Typo="Typography.Subject">     <!-- Card titles -->
<FluentLabel Typo="Typography.Body">        <!-- Default text -->
@* Typography.Caption does not exist — use Body + Color.Neutral for small/meta text *@
<FluentLabel Typo="Typography.Body" Color="Color.Neutral">   <!-- Small/meta -->
```

> Valid `Typography` values: `Body`, `Subject`, `Header`, `PaneHeader`, `EmailHeader`, `PageTitle`, `HeroTitle`, `H1`–`H6`  
> Valid `FontWeight` values: `Normal`, `Bold`, `Bolder` (`Semibold` does **not** exist)

### Spacing Quick Reference

```razor
<FluentStack VerticalGap="4">    <!-- XS: icon-text -->
<FluentStack VerticalGap="8">    <!-- S: related items -->
<FluentStack VerticalGap="12">   <!-- M: card sections -->
<FluentStack VerticalGap="16">   <!-- L: between cards -->
<FluentStack VerticalGap="24">   <!-- XL: major sections -->
```
