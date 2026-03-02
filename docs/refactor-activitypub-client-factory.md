# Refactor Plan: ActivityPub Client Factory & Signature Architecture

## Problem Statement

The server codebase has grown a horizontal dependency pattern where low-level signing
primitives (`HttpSignatureService`, `ICryptoProvider`, `IHttpClientFactory`) are injected
directly into high-level services and controllers. This creates three concrete problems:

1. **Signing logic is duplicated.** `ActivityDeliveryService` manually constructs HTTP requests,
   signs them, and sends them — doing exactly what `ActivityPubClient.PostAsync` already does,
   but with raw primitives because the DI-registered `IActivityPubClient` is locked to one
   identity.

2. **`HttpSignatureService` conflates two unrelated roles**: signing outbound requests
   (a client concern) and verifying inbound requests (a server concern). All five server
   files that inject it do so for different reasons.

3. **`IActivityPubClient` in the server DI container is anonymous** (no credentials), so
   any code that needs to make a *signed* outbound request bypasses it entirely and reaches
   directly for `HttpSignatureService` + `IHttpClientFactory`.

---

## Current Dependency Map

```
ActivityDeliveryService
  ├── IDeliveryQueueRepository  ← legitimate
  ├── IActorRepository          ← legitimate (credential + follower lookup)
  ├── IActivityRepository       ← legitimate (dead-letter side effects)
  ├── IActivityPubClient        ← used for anonymous GetActorAsync only
  ├── HttpSignatureService      ← used to sign delivery POSTs and actor GETs
  ├── ICryptoProvider           ← transitively needed by HttpSignatureService usage
  ├── IHttpClientFactory        ← used to create HTTP clients for signing
  └── IOptions<...> / ILogger   ← legitimate

InboxController
  ├── IInboxHandler             ← legitimate
  ├── IActivityRepository       ← legitimate
  ├── IActorRepository          ← legitimate
  ├── HttpSignatureService      ← SPLIT USE: VerifyHttpSignatureAsync + SendSignedGetAsync
  ├── IActivityPubClient        ← used for anonymous GetActorAsync (public key fetching)
  ├── ISystemIdentityService    ← used to get system key for signed actor GETs
  ├── IHttpClientFactory        ← used for signed GET HTTP client
  ├── AttachmentProcessingService ← legitimate
  └── ObjectEnrichmentService   ← legitimate

SharedInboxController
  ├── IInboxHandler             ← legitimate
  ├── IActorRepository          ← legitimate
  ├── HttpSignatureService      ← verify only (VerifyHttpSignatureAsync)
  └── IActivityPubClient        ← anonymous GetActorAsync for public key fetching
                                   (inconsistent: InboxController does signed GET here)

OutboxController
  ├── IActivityRepository       ← legitimate
  ├── IActorRepository          ← legitimate
  ├── OutboxProcessor           ← legitimate
  ├── AttachmentProcessingService ← legitimate
  ├── ObjectEnrichmentService   ← legitimate
  └── HttpSignatureService      ← verify only (VerifyHttpSignatureAsync, ComputeContentDigestHash,
                                   GetSignatureKeyId)

ProxyController
  ├── IHttpClientFactory        ← used for signing proxy requests
  ├── HttpSignatureService      ← sign only (ApplyHttpSignatureAsync)
  └── ISystemIdentityService    ← used to get system key for proxy signing

InboxProcessor
  ├── IActivityRepository       ← legitimate
  ├── IActorRepository          ← legitimate
  ├── IActivityBuilderFactory   ← legitimate
  ├── HttpSignatureService      ← INJECTED BUT NEVER CALLED (dead dependency)
  ├── AdminOperationsHandler    ← legitimate
  ├── AttachmentProcessingService ← legitimate
  ├── ActivityDeliveryService   ← legitimate
  └── IActivityPubClient        ← used for GetAsync<Actor> (anonymous)
```

---

## Target Architecture

### Core concept: the two roles of `HttpSignatureService` must be separated

| Role | Belongs in | New abstraction |
|---|---|---|
| Sign outbound HTTP requests | `Broca.ActivityPub.Client` | Stays inside `ActivityPubClient` (internal) |
| Verify inbound HTTP signatures | `Broca.ActivityPub.Server` | New `IHttpSignatureVerifier` interface |

### Core concept: per-actor signed clients via a factory

A new `IActivityPubClientFactory` (in the Client package) creates `IActivityPubClient`
instances with explicit actor credentials. Server code that needs to make signed HTTP
requests creates a client from the factory rather than calling `HttpSignatureService`
directly.

### Target dependency map

```
ActivityDeliveryService
  ├── IDeliveryQueueRepository
  ├── IActorRepository          ← still needed for credential + follower lookup
  ├── IActivityRepository
  └── IActivityPubClientFactory ← replaces HttpSignatureService, ICryptoProvider,
                                   IHttpClientFactory, and anonymous IActivityPubClient

InboxController
  ├── IInboxHandler
  ├── IActivityRepository
  ├── IActorRepository
  ├── IHttpSignatureVerifier    ← replaces HttpSignatureService (verify path)
  ├── IActivityPubClientFactory ← replaces IActivityPubClient + ISystemIdentityService
  │                                + IHttpClientFactory (for signed actor GETs)
  ├── AttachmentProcessingService
  └── ObjectEnrichmentService

SharedInboxController
  ├── IInboxHandler
  ├── IActorRepository
  ├── IHttpSignatureVerifier    ← replaces HttpSignatureService
  └── IActivityPubClientFactory ← replaces anonymous IActivityPubClient (signed GET)

OutboxController
  ├── IActivityRepository
  ├── IActorRepository
  ├── OutboxProcessor
  ├── AttachmentProcessingService
  ├── ObjectEnrichmentService
  └── IHttpSignatureVerifier    ← replaces HttpSignatureService

ProxyController
  ├── IActivityPubClientFactory ← replaces HttpSignatureService + IHttpClientFactory
  └── ISystemIdentityService    ← still needed to get system credentials for factory

InboxProcessor
  ├── IActivityRepository
  ├── IActorRepository
  ├── IActivityBuilderFactory
  ├── AdminOperationsHandler
  ├── AttachmentProcessingService
  ├── ActivityDeliveryService
  └── IActivityPubClient        ← keep for anonymous GetAsync<Actor> (or replace with factory)
  (HttpSignatureService removed — was dead already)
```

---

## Implementation Phases

### Phase 1 — Add `IActivityPubClientFactory` to the Client package

**New file: `src/Broca.ActivityPub.Core/Interfaces/IActivityPubClientFactory.cs`**

```csharp
public interface IActivityPubClientFactory
{
    IActivityPubClient CreateAnonymous();
    IActivityPubClient CreateForActor(string actorId, string publicKeyId, string privateKeyPem);
}
```

**New file: `src/Broca.ActivityPub.Client/Services/ActivityPubClientFactory.cs`**

The factory holds the shared infrastructure dependencies (`IHttpClientFactory`,
`IWebFingerService`, `HttpSignatureService`, `ILogger<ActivityPubClient>`) that are
the same for every client instance. It creates `ActivityPubClient` instances with
`Options.Create(new ActivityPubClientOptions { ... })`.

```csharp
public class ActivityPubClientFactory : IActivityPubClientFactory
{
    // injected: IHttpClientFactory, IWebFingerService, HttpSignatureService,
    //           IOptions<ActivityPubClientOptions> (for defaults), ILogger<ActivityPubClient>

    public IActivityPubClient CreateAnonymous()
        => new ActivityPubClient(..., Options.Create(new ActivityPubClientOptions()));

    public IActivityPubClient CreateForActor(string actorId, string publicKeyId, string privateKeyPem)
        => new ActivityPubClient(..., Options.Create(new ActivityPubClientOptions
        {
            ActorId = actorId,
            PublicKeyId = publicKeyId,
            PrivateKeyPem = privateKeyPem
        }));
}
```

**Update: `src/Broca.ActivityPub.Client/Extensions/ServiceCollectionExtensions.cs`**

Register `IActivityPubClientFactory` as a singleton in `AddActivityPubClient()`.

**Cache sharing note**: Factory-created clients have their own in-memory cache. For the
delivery service this is fine (short-lived). For the inbox controllers doing public key
lookups, `IMemoryCache` (already injected) continues to cache public keys at the
controller level — no regression.

---

### Phase 2 — Extract `IHttpSignatureVerifier` to the Server package

**New file: `src/Broca.ActivityPub.Core/Interfaces/IHttpSignatureVerifier.cs`**
*(or place in Server package if it shouldn't leak into Core)*

```csharp
public interface IHttpSignatureVerifier
{
    Task<bool> VerifyAsync(
        IDictionary<string, string> headers,
        string publicKeyPem,
        CancellationToken cancellationToken = default);

    bool VerifyDigest(byte[] bodyBytes, string digestHeader);

    string? GetSignatureKeyId(string signatureHeader);
}
```

**New file: `src/Broca.ActivityPub.Server/Services/HttpSignatureVerifier.cs`**

Wraps the existing `HttpSignatureService.VerifyHttpSignatureAsync`,
`ComputeContentDigestHash`, and `GetSignatureKeyId` methods. The concrete implementation
can delegate to `HttpSignatureService` internally or duplicate the small amount of logic.

**Register in server DI**: `services.AddScoped<IHttpSignatureVerifier, HttpSignatureVerifier>();`

`HttpSignatureService` remains registered for now (it is still needed by
`ActivityPubClientFactory` internally). Its direct injection into server components is
eliminated progressively.

---

### Phase 3 — Refactor `ActivityDeliveryService`

**File: `src/Broca.ActivityPub.Server/Services/ActivityDeliveryService.cs`**

**Remove from constructor**:
- `IActivityPubClient` (anonymous, replaced by factory)
- `HttpSignatureService`
- `ICryptoProvider`
- `IHttpClientFactory`

**Add to constructor**:
- `IActivityPubClientFactory`

**`DeliverActivityAsync` changes**:

1. Credential extraction from `senderActor.ExtensionData` stays — this is a server concern.
2. Replace the manual `HttpRequestMessage` + `_signatureService.ApplyHttpSignatureAsync` +
   `_httpClientFactory.CreateClient` block with:

```csharp
var client = _clientFactory.CreateForActor(item.SenderActorId, publicKeyId, privateKeyPem);
var response = await client.PostAsync(new Uri(item.InboxUrl), item.Activity, cancellationToken);
```

3. Replace `FetchActorWithSignatureAsync` (which calls `_signatureService.SendSignedGetAsync`)
   with:

```csharp
var client = _clientFactory.CreateForActor(item.SenderActorId, publicKeyId, privateKeyPem);
var actor = await client.GetActorAsync(new Uri(item.TargetActorId), cancellationToken);
```

4. Replace `_activityPubClient.GetActorAsync` (anonymous, queue-time resolution) with
   `_clientFactory.CreateAnonymous().GetActorAsync(...)`.

**Net result**: `ActivityDeliveryService` constructor drops from 9 to 6 parameters.

---

### Phase 4 — Refactor `InboxController`

**File: `src/Broca.ActivityPub.Server/Controllers/InboxController.cs`**

**Remove from constructor**:
- `HttpSignatureService`
- `IActivityPubClient`
- `IHttpClientFactory`

**Add to constructor**:
- `IHttpSignatureVerifier`
- `IActivityPubClientFactory`

**`VerifyHttpSignatureAsync` private method**:
- Replace `_signatureService.VerifyHttpSignatureAsync` call with `_signatureVerifier.VerifyAsync`
- Replace `_signatureService.ComputeContentDigestHash` with `_signatureVerifier.VerifyDigest`
- Replace `_signatureService.GetSignatureKeyId` with `_signatureVerifier.GetSignatureKeyId`

**`FetchActorWithSignatureAsync` private method**:
- Replace `_httpClientFactory.CreateClient` + `_signatureService.SendSignedGetAsync` with:

```csharp
// System actor signs the public key fetch (authorized fetch support)
var systemActor = await _systemIdentityService.GetSystemActorAsync(cancellationToken);
var systemKey = await _systemIdentityService.GetSystemPrivateKeyAsync(cancellationToken);
var client = _clientFactory.CreateForActor(systemActor.Id!, $"{systemActor.Id}#main-key", systemKey);
return await client.GetActorAsync(new Uri(actorUrl), cancellationToken);
```

Note: `ISystemIdentityService` stays in the constructor — it provides credentials that are
passed to the factory. Alternatively, a helper method on the server-side factory could be
pre-constructed for the system actor if this pattern is used in many places (see Phase 6).

---

### Phase 5 — Refactor `SharedInboxController`

**File: `src/Broca.ActivityPub.Server/Controllers/SharedInboxController.cs`**

**Remove from constructor**:
- `HttpSignatureService`
- `IActivityPubClient`

**Add to constructor**:
- `IHttpSignatureVerifier`
- `IActivityPubClientFactory`

**`VerifySignatureAsync`**: replace `_signatureService.VerifyHttpSignatureAsync` with
`_signatureVerifier.VerifyAsync`.

**`FetchPublicKeyAsync`**: replace `_activityPubClient.GetActorAsync` with
`_clientFactory.CreateForActor(systemActor...).GetActorAsync(...)` (signed GET, consistent
with `InboxController`). This fixes the inconsistency where `InboxController` does a
signed GET but `SharedInboxController` does an anonymous one.

---

### Phase 6 — Refactor `OutboxController`

**File: `src/Broca.ActivityPub.Server/Controllers/OutboxController.cs`**

**Remove from constructor**:
- `HttpSignatureService`

**Add to constructor**:
- `IHttpSignatureVerifier`

**`VerifyCallerSignatureAsync`**: replace all three `_signatureService.*` calls with
`_signatureVerifier.*` equivalents. No HTTP client changes needed here.

---

### Phase 7 — Refactor `ProxyController`

**File: `src/Broca.ActivityPub.Server/Controllers/ProxyController.cs`**

**Remove from constructor**:
- `HttpSignatureService`
- `IHttpClientFactory`

**Add to constructor**:
- `IActivityPubClientFactory`

The proxy GET/POST handlers currently:
1. Get system actor credentials from `ISystemIdentityService`
2. Create an `HttpClient` from `IHttpClientFactory`
3. Build a request and call `_signatureService.ApplyHttpSignatureAsync`

Replace with:

```csharp
var systemActor = await _systemIdentityService.GetSystemActorAsync();
var systemKey = await _systemIdentityService.GetSystemPrivateKeyAsync();
var client = _clientFactory.CreateForActor(systemActor.Id!, $"{systemActor.Id}#main-key", systemKey);

// GET:
var result = await client.GetAsync<JsonElement>(targetUri, useCache: false);

// POST:
var response = await client.PostAsync(targetUri, data);
```

`ISystemIdentityService` stays — it is needed for credential retrieval.

---

### Phase 8 — Remove dead dependency from `InboxProcessor`

**File: `src/Broca.ActivityPub.Server/Services/InboxProcessor.cs`**

Remove `HttpSignatureService` from the constructor. It is injected but never called.
No logic changes required.

---

### Phase 9 — Optional: `IServerClientProvider` convenience wrapper

If the pattern of `ISystemIdentityService` + `IActivityPubClientFactory` → system-actor
client appears in three or more places, extract it into a server-side helper registered
in DI:

```csharp
public interface IServerClientProvider
{
    IActivityPubClient Anonymous { get; }
    Task<IActivityPubClient> GetSystemClientAsync(CancellationToken ct = default);
    Task<IActivityPubClient> GetClientForUsernameAsync(string username, CancellationToken ct = default);
}
```

This is an optional consolidation after Phases 4-7 are complete and the pattern becomes
clear. Do not introduce it prematurely.

---

### Phase 10 — Cleanup and DI registration

**`src/Broca.ActivityPub.Server/Extensions/ServiceCollectionExtensions.cs`**

- Remove direct `HttpSignatureService` registration from server DI
  (it is now only registered transitively via the client package's `AddActivityPubClient()`).
- Add `IHttpSignatureVerifier` registration.
- The anonymous `IActivityPubClient` singleton registration can be removed if no remaining
  server code uses it — otherwise it can stay for the one usage in `InboxProcessor.GetAsync`.

---

## Risk Areas

| Area | Risk | Mitigation |
|---|---|---|
| Per-request client instances | No shared cache between factory-created clients | Controller-level `IMemoryCache` for public keys already handles this |
| `ActivityPubClient.PostAsync` vs manual delivery | `PostAsync` must handle all content-types and header edge cases currently in `DeliverActivityAsync` | Audit `PostAsync` against the manual code before switching; add or fix if needed |
| Authorized fetch consistency | `SharedInboxController` does anonymous GET; `InboxController` does signed GET | Phase 5 aligns both to signed GET via factory |
| Thread safety of factory-created clients | Each client has its own `Dictionary<>` cache (not thread-safe for concurrent writes) | Already the same situation with the singleton client; short-lived factory clients avoid the problem |

---

## File Change Summary

| File | Change type | Summary |
|---|---|---|
| `Core/Interfaces/IActivityPubClientFactory.cs` | **New** | Factory interface |
| `Client/Services/ActivityPubClientFactory.cs` | **New** | Factory implementation |
| `Client/Extensions/ServiceCollectionExtensions.cs` | **Modify** | Register factory |
| `Core/Interfaces/IHttpSignatureVerifier.cs` | **New** | Verifier interface |
| `Server/Services/HttpSignatureVerifier.cs` | **New** | Verifier implementation wrapping existing logic |
| `Server/Extensions/ServiceCollectionExtensions.cs` | **Modify** | Register verifier, remove HttpSignatureService |
| `Server/Services/ActivityDeliveryService.cs` | **Modify** | Phase 3 |
| `Server/Controllers/InboxController.cs` | **Modify** | Phase 4 |
| `Server/Controllers/SharedInboxController.cs` | **Modify** | Phase 5 |
| `Server/Controllers/OutboxController.cs` | **Modify** | Phase 6 |
| `Server/Controllers/ProxyController.cs` | **Modify** | Phase 7 |
| `Server/Services/InboxProcessor.cs` | **Modify** | Phase 8 — remove dead dependency only |

---

## What Does NOT Change

- `IActivityBuilderFactory` / `ActivityBuilderFactory` — unrelated; stays as-is.
- `AdminOperationsHandler` — no signing/verifying dependencies; unchanged.
- `SystemIdentityService` — stays and is still used for credential retrieval in Phases 4, 5, 7.
- `AttachmentProcessingService` / `RemoteActorMediaController` — `IHttpClientFactory`
  usage is for downloading binary blobs, not ActivityPub signing; not in scope.
- `InboxProcessor` activity-handling logic — only the dead dependency is removed.
- `CryptographyService` — internal to `HttpSignatureService`; no public surface change.
- Test projects — will need constructor updates to match new signatures, but no logic changes.
