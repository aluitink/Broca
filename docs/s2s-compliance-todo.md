# ActivityPub Server-to-Server Compliance — Work Items

Last reviewed: 2026-02-28. Mastodon is the primary target for interop, with secondary targets including Threads, Pixelfed, and Pleroma.

---

## Status Summary

**Critical items:** 0 remaining (all resolved)  
**High priority items:** 0 remaining (all resolved)  
**Medium priority items:** 3 remaining  
**Low priority items:** 2 remaining


## 🟡 Medium — Missing Features / Spec Gaps

### ✅ M1 · `Move` activity handled (COMPLETED)

**File:** `InboxProcessor.cs`

`Move` is used by Mastodon for account portability. When a user migrates to a new account, followers are automatically migrated.

**Implementation:** Handles `Move` by updating the follower's following list: replaces the old actor IRI with the new one if the new actor's `alsoKnownAs` references the old one (prevents spoofing).

**Status:** ✅ Implemented and tested with security validation.

---

### M2 · Actor document missing `featured` collection (pinned posts)

**File:** `ActorController.cs` → `Get`

Mastodon expects a `featured` property on the actor document pointing to an `OrderedCollection` of pinned post URIs. Without it, Mastodon logs warnings and will never show pinned posts.

**Fix:** Add `featured` to the actor's extension data pointing to `{baseUrl}/users/{username}/collections/featured`. The collections infrastructure exists; needs to be exposed on the actor document.

**Priority:** Medium - affects user features but doesn't break federation.

---

### ✅ M3 · `alsoKnownAs` field supported (COMPLETED)

**File:** `ActorController.cs`

Required for `Move`-based account migration. Actors can declare aliases via the `alsoKnownAs` property.

**Implementation:** Supported via actor's ExtensionData dictionary. Set as a JSON array of actor URI strings.

**Status:** ✅ No code changes required - ExtensionData already serializes arbitrary fields in actor documents.

---

### M4 · Followers / Following collections expose full member list without authentication

**File:** `ActorController.cs` → `GetFollowers`, `GetFollowing`

For locked accounts (`manuallyApprovesFollowers = true`) this leaks the full social graph. Mastodon hides this behind authentication for locked actors.

**Fix:** Check if the actor has `manuallyApprovesFollowers = true` and, if so, require the requester to be authenticated (or return only the count with no items).

**Priority:** Medium - privacy concern for locked accounts.

---

## 🟢 Low / Cosmetic

### L4 · HTTP signature `ParseSignatureParts` splits on first `=` only

**File:** `HttpSignatureService.cs` → `ParseSignatureParts`

The outer `Split(',')` then inner `Split('=', 2)` approach is correct for most cases, but any Signature header component whose *value* legitimately contains a comma (after line-folding) would be mishandled.

**Fix:** Consider a more robust parser that handles quoted-string values per RFC 7230.

**Priority:** Low - rarely encountered in practice.

---

## Priority Order (recommended)

| Order | Item | Effort | Notes |
|-------|------|--------|-------|
| 1 | ~~M1 — `Move` activity~~ | ~~Medium~~ | ✅ **Completed** |
| 2 | ~~M3 — `alsoKnownAs`~~ | ~~Small~~ | ✅ **Completed** (no code changes needed) |
| 3 | M2 — `featured` collection on actor | Small | Quick win — collection infrastructure exists |
| 4 | M4 — Followers list auth for locked accounts | Small | Privacy fix |
| 5 | M5 — NodeInfo real stats | Medium | Requires repo changes |
| 6 | L2 — Content-type testing | Small | Verification only |
| 7 | L4 — HTTP signature parser robustness | Small | Edge case |

---

## New Gaps Identified

None at this time. All critical and high-priority federation issues have been resolved.

---

## Test Coverage

The following test suites cover the completed items:

- **ServerToServerTests.cs** — S2S federation, Follow/Undo/Reject, Update{Actor}, clock-skew validation
- **SharedInboxTests.cs** — Shared inbox delivery, addressing, signature validation, follower fan-out
- **ClientToServerTests.cs** — C2S operations including Undo/Reject by IRI reference
- **OutboxAuthenticationTests.cs** — Outbox POST authentication

---

## Notes

- All critical (🔴) and high-priority (🟠) items have been resolved and tested.
- Remaining items are medium (🟡) and low (🟢) priority enhancements.
- The project now has solid baseline S2S federation compatibility with Mastodon.

