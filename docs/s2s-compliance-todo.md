# ActivityPub Server-to-Server Compliance — Work Items

Last reviewed: 2026-02-28. Mastodon is the primary target for interop, with secondary targets including Threads, Pixelfed, and Pleroma.

---

## Status Summary

**Critical items:** 0 remaining (all resolved)  
**High priority items:** 0 remaining (all resolved)  
**Medium priority items:** 5 remaining  
**Low priority items:** 2 remaining

---

## ✅ Completed Items

### ~~C5~~ · ~~`Reject` and `Undo` IRI-reference objects (`ILink`)~~ ✅

**Status:** COMPLETED with tests  
**Files:** `InboxProcessor.cs`, `OutboxProcessor.cs`  
Both `HandleUndoAsync` and `HandleRejectAsync` now properly handle ILink references for Mastodon compatibility.  
**Tests:** `ServerToServerTests.S2S_UserUndoesFollow_FollowerRemovedOnRemoteServer`, `ClientToServerTests.C2S_UndoFollowByIriReference_FollowingRemoved`, `ClientToServerTests.C2S_RejectFollowByIriReference_PendingFollowerRemoved`

---

### ~~H1~~ · ~~Clock-skew validation on incoming HTTP signatures~~ ✅

**Status:** COMPLETED with tests  
**File:** `ActivityPubControllerBase.cs` → `ValidateRequestClockSkew`  
All inbox endpoints now validate Date/Created headers against a 12-hour past / 5-minute future window.  
**Tests:** `ServerToServerTests` includes clock skew validation tests

---

### ~~H2~~ · ~~Follower-fan-out delivery shared inbox grouping~~ ✅

**Status:** COMPLETED with tests  
**File:** `ActivityDeliveryService.cs` → `QueueActivityForDeliveryAsync`  
Followers are now grouped by `sharedInbox ?? personalInbox` to minimize redundant deliveries.  
**Tests:** `SharedInboxTests.FollowerFanOut_MultipleFollowersSameServer_UsesSharedInbox`

---

### ~~H3~~ · ~~Outbox `POST` endpoint authentication~~ ✅

**Status:** COMPLETED with tests  
**File:** `OutboxController.cs` → `VerifyCallerSignatureAsync`  
Outbox POST now requires HTTP signature verification when `RequireHttpSignatures` is enabled.  
**Tests:** `OutboxAuthenticationTests.cs`

---

### ~~H4~~ · ~~`Update{Actor}` from remote servers~~ ✅

**Status:** COMPLETED with tests  
**File:** `InboxProcessor.cs` → `HandleIncomingUpdateAsync`  
Remote profile updates are now processed with security checks (sender must match updated actor, only updates cached actors).  
**Tests:** `ServerToServerTests.S2S_UpdatePerson_RefreshesLocalCachedActor`, `S2S_UpdatePerson_IgnoredWhenActorNotCached`

---

### ~~H5~~ · ~~Shared inbox `Digest` header validation~~ ✅

**Status:** COMPLETED  
**File:** `SharedInboxController.cs` → `VerifySignatureAsync`  
Digest header is now validated against body hash for shared inbox POST requests.

---

### ~~L1~~ · ~~`410 Gone` for deleted resources~~ ✅

**Status:** COMPLETED  
**File:** `ObjectController.cs`  
Returns `410 Gone` when accessing deleted objects instead of `404 Not Found`.

---

### ~~L3~~ · ~~Concrete `Link` type in pattern-matches~~ ✅

**Status:** COMPLETED  
All pattern-matches now use `is ILink` (interface) instead of `is Link` (concrete type) per project guidelines.

---

## 🟠 High — Spec Violations / Interop Issues

*All high priority items have been resolved.*

---

## 🟡 Medium — Missing Features / Spec Gaps

### M1 · `Move` activity not handled (account migration)

**File:** `InboxProcessor.cs`

`Move` is used by Mastodon for account portability. Currently falls to `_ => true` (accepted but not processed).

**Fix:** Handle `Move` by updating the follower's following list: replace the old actor IRI with the new one (if the new actor's `alsoKnownAs` references the old one, to prevent spoofing).

**Priority:** Medium - required for full Mastodon compatibility, but low usage.

---

### M2 · Actor document missing `featured` collection (pinned posts)

**File:** `ActorController.cs` → `Get`

Mastodon expects a `featured` property on the actor document pointing to an `OrderedCollection` of pinned post URIs. Without it, Mastodon logs warnings and will never show pinned posts.

**Fix:** Add `featured` to the actor's extension data pointing to `{baseUrl}/users/{username}/collections/featured`. The collections infrastructure exists; needs to be exposed on the actor document.

**Priority:** Medium - affects user features but doesn't break federation.

---

### M3 · Actor document missing `alsoKnownAs` field

**File:** `ActorController.cs` → `Get`

Required for `Move`-based account migration. Actors need to be able to declare aliases.

**Fix:** Support an `alsoKnownAs` property in the actor's stored extension data and expose it in the actor document response.

**Priority:** Medium - required for M1 (Move activity) to be fully functional.

---

### M4 · Followers / Following collections expose full member list without authentication

**File:** `ActorController.cs` → `GetFollowers`, `GetFollowing`

For locked accounts (`manuallyApprovesFollowers = true`) this leaks the full social graph. Mastodon hides this behind authentication for locked actors.

**Fix:** Check if the actor has `manuallyApprovesFollowers = true` and, if so, require the requester to be authenticated (or return only the count with no items).

**Priority:** Medium - privacy concern for locked accounts.

---

### M5 · `NodeInfo` usage stats return hardcoded values

**File:** `NodeInfoService.cs` → `GetInstanceStatsAsync`

Many relay servers and crawlers use `totalUsers`, `activeHalfyear`, and `activeMonth` to decide whether to federate. Currently returns hardcoded values (1, 1, 1, 0) with a TODO comment.

**Fix:** Implement real stats by adding count methods to `IActorRepository` and `IActivityRepository`:
- `TotalUsers`: count all actors
- `ActiveUsersMonth`: count actors with outbox activity in past 30 days
- `ActiveUsersHalfYear`: count actors with outbox activity in past 180 days
- `LocalPosts`: count Create activities in all outboxes

**Priority:** Medium - affects relay/instance discovery.

---

## 🟢 Low / Cosmetic

### L2 · `application/ld+json; profile=…` content-type handling

**Files:** All controllers with `[Consumes]` / `[Produces]` attributes

Mastodon sends `Content-Type: application/ld+json; profile="https://www.w3.org/ns/activitystreams"`. ASP.NET Core may not match this against `"application/ld+json"` depending on version and configuration.

**Fix:** Validate with live Mastodon instance. If needed, add explicit media-type handling or a custom input formatter.

**Priority:** Low - most modern ASP.NET Core versions handle this correctly.

---

### L4 · HTTP signature `ParseSignatureParts` splits on first `=` only

**File:** `HttpSignatureService.cs` → `ParseSignatureParts`

The outer `Split(',')` then inner `Split('=', 2)` approach is correct for most cases, but any Signature header component whose *value* legitimately contains a comma (after line-folding) would be mishandled.

**Fix:** Consider a more robust parser that handles quoted-string values per RFC 7230.

**Priority:** Low - rarely encountered in practice.

---

## Priority Order (recommended)

| Order | Item | Effort | Notes |
|-------|------|--------|-------|
| 1 | M2 — `featured` collection on actor | Small | Quick win — collection infrastructure exists |
| 2 | M3 — `alsoKnownAs` | Small | Required for M1 |
| 3 | M1 — `Move` activity | Medium | Depends on M3 |
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

