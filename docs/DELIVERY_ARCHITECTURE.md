# Activity Delivery System Architecture

## Component Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         Application Layer                        │
│                                                                  │
│  ┌────────────────┐         ┌─────────────────┐                │
│  │ OutboxController│────────▶│ OutboxProcessor │                │
│  └────────────────┘         └────────┬────────┘                │
│         │                             │                          │
│         │ POST /users/alice/outbox    │                          │
│         │                             │                          │
│         ▼                             ▼                          │
│  ┌────────────────────────────────────────────────┐            │
│  │         ActivityDeliveryService                │            │
│  │                                                 │            │
│  │  • QueueActivityForDeliveryAsync()            │            │
│  │  • ProcessPendingDeliveriesAsync()            │            │
│  │  • DeliverActivityAsync()                     │            │
│  └────────────┬───────────────────────────────────┘            │
│               │                                                  │
└───────────────┼──────────────────────────────────────────────────┘
                │
                │ Enqueue/Dequeue
                │
                ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Persistence Layer                           │
│                                                                  │
│  ┌─────────────────────────────────────────────────────┐       │
│  │       IDeliveryQueueRepository                      │       │
│  │                                                      │       │
│  │  ┌────────────────┐      ┌──────────────────────┐  │       │
│  │  │   In-Memory    │  OR  │   Your Database      │  │       │
│  │  │ Implementation │      │  (PostgreSQL, etc.)  │  │       │
│  │  └────────────────┘      └──────────────────────┘  │       │
│  │                                                      │       │
│  │  • EnqueueAsync()                                   │       │
│  │  • GetPendingDeliveriesAsync()                     │       │
│  │  • MarkAsDeliveredAsync()                          │       │
│  │  • MarkAsFailedAsync()                             │       │
│  └─────────────────────────────────────────────────────┘       │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                      Background Worker                           │
│                                                                  │
│  ┌─────────────────────────────────────────────────────┐       │
│  │     ActivityDeliveryWorker (HostedService)          │       │
│  │                                                      │       │
│  │     while (!stopping) {                             │       │
│  │         ProcessPendingDeliveriesAsync()             │       │
│  │         await Task.Delay(5 seconds)                 │       │
│  │     }                                                │       │
│  └─────────────────────────────────────────────────────┘       │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                      External Services                           │
│                                                                  │
│  ┌─────────────────┐  HTTP POST + Signature  ┌──────────────┐  │
│  │  Remote Server  │◀──────────────────────────│ HTTP Client  │  │
│  │  Inbox          │                           └──────────────┘  │
│  └─────────────────┘                                             │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

## Sequence Diagram: Activity Delivery Flow

```
User          OutboxController    OutboxProcessor    DeliveryService    DeliveryQueue    Worker         RemoteInbox
 │                   │                    │                  │                │            │                │
 │ POST /outbox      │                    │                  │                │            │                │
 │──────────────────▶│                    │                  │                │            │                │
 │                   │ ProcessActivityAsync()                │                │            │                │
 │                   │───────────────────▶│                  │                │            │                │
 │                   │                    │ SaveOutboxActivity()              │            │                │
 │                   │                    │──────────────────────────────────▶│            │                │
 │                   │                    │                  │                │            │                │
 │                   │                    │ QueueForDelivery()                │            │                │
 │                   │                    │─────────────────▶│                │            │                │
 │                   │                    │                  │ EnqueueBatch() │            │                │
 │                   │                    │                  │───────────────▶│            │                │
 │                   │                    │                  │                │            │                │
 │◀─ 201 Created ────│                    │                  │                │            │                │
 │                   │                    │                  │                │            │                │
 │                   │                    │                  │                │  (5 sec)   │                │
 │                   │                    │                  │                │───────────▶│                │
 │                   │                    │                  │                │            │ GetPending()   │
 │                   │                    │                  │                │            │───────────────▶│
 │                   │                    │                  │                │            │◀───────────────│
 │                   │                    │                  │                │            │                │
 │                   │                    │                  │◀─ProcessPending()──────────│                │
 │                   │                    │                  │                │            │                │
 │                   │                    │                  │ DeliverActivity()           │                │
 │                   │                    │                  │────────────────────────────────────────────▶│
 │                   │                    │                  │                │            │  POST + Sig   │
 │                   │                    │                  │◀────────────────────────────────────────────│
 │                   │                    │                  │                │            │  202 Accepted │
 │                   │                    │                  │ MarkAsDelivered()           │                │
 │                   │                    │                  │───────────────▶│            │                │
 │                   │                    │                  │                │            │                │
```

## State Machine: Delivery Item Lifecycle

```
                    ┌─────────────┐
                    │   PENDING   │
                    └──────┬──────┘
                           │
                           │ Worker picks up
                           │
                           ▼
                    ┌─────────────┐
              ┌────▶│ PROCESSING  │
              │     └──────┬──────┘
              │            │
              │            ├─────────────────┐
              │            │                 │
              │            ▼                 ▼
              │     ┌─────────────┐   ┌─────────────┐
              │     │  DELIVERED  │   │   FAILED    │
              │     └─────────────┘   └──────┬──────┘
              │            │                  │
              │            │                  │ Retry < Max?
              │            │                  │
              │            │                  ├─ Yes ─▶ Set NextAttemptAt
              │            │                  │         (exponential backoff)
              │            │                  │              │
              │            │                  │              └────────┐
              │            │                  │                       │
              │            │                  └─ No ──▶ ┌──────────┐ │
              │            │                            │   DEAD   │ │
              │            │                            └──────────┘ │
              │            │                                         │
              │            ▼                                         │
              │     (Cleanup after 7 days)                          │
              │                                                      │
              └──────────────────────────────────────────────────────┘
```

## Data Flow: Single Delivery

```
┌─────────────────────────────────────────────────────────────────┐
│ 1. Fetch Sender's Private Key                                   │
│    actorRepository.GetActorByUsernameAsync()                    │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│ 2. Serialize Activity to JSON                                   │
│    JsonSerializer.Serialize(activity)                           │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│ 3. Sign HTTP Request                                            │
│    HttpSignatureService.ApplyHttpSignatureAsync()               │
│    - (request-target)                                           │
│    - host                                                       │
│    - date                                                       │
│    - digest                                                     │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│ 4. Send HTTP POST                                               │
│    POST https://remote.server/users/bob/inbox                   │
│    Content-Type: application/activity+json                      │
│    Signature: keyId="...", headers="...", signature="..."      │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│ 5. Process Response                                             │
│    ├─ 200-299, 202 → MarkAsDeliveredAsync()                   │
│    └─ Other → MarkAsFailedAsync() + Schedule Retry            │
└─────────────────────────────────────────────────────────────────┘
```

## Retry Timeline Example

```
Activity Posted: 10:00:00
│
├─ Attempt 1 fails: 10:00:01
│  └─ Retry scheduled for: 10:01:01 (1 min later)
│
├─ Attempt 2 fails: 10:01:01
│  └─ Retry scheduled for: 10:06:01 (5 min later)
│
├─ Attempt 3 fails: 10:06:01
│  └─ Retry scheduled for: 10:21:01 (15 min later)
│
├─ Attempt 4 fails: 10:21:01
│  └─ Retry scheduled for: 11:21:01 (1 hour later)
│
├─ Attempt 5 fails: 11:21:01
│  └─ Retry scheduled for: 15:21:01 (4 hours later)
│
└─ Attempt 6 fails: 15:21:01
   └─ Marked as DEAD (no more retries)

Total time before giving up: ~5 hours 21 minutes
```

## Parallel Processing

```
Worker Cycle (every 5 seconds):
│
├─ Fetch 100 pending items from queue
│
└─ Process in parallel (max 10 concurrent)
   │
   ├─ Task 1: Deliver to mastodon.social/inbox
   ├─ Task 2: Deliver to pixelfed.social/inbox
   ├─ Task 3: Deliver to lemmy.ml/inbox
   ├─ Task 4: Deliver to peertube.tv/inbox
   ├─ Task 5: Deliver to example.com/inbox
   ├─ Task 6: Deliver to another.server/inbox
   ├─ Task 7: Deliver to yet.another/inbox
   ├─ Task 8: Deliver to one.more/inbox
   ├─ Task 9: Deliver to last.one/inbox
   └─ Task 10: Deliver to final.server/inbox
   │
   └─ Wait for all to complete
      │
      └─ Next batch of 10...
```

## Monitoring Dashboard Concept

```
┌──────────────────────────────────────────────────────┐
│          Activity Delivery Queue Status              │
├──────────────────────────────────────────────────────┤
│                                                      │
│  Pending:      [████████████░░░░░░░░] 1,234         │
│  Processing:   [██░░░░░░░░░░░░░░░░░░]    42         │
│  Delivered:    [████████████████████] 45,678        │
│  Failed:       [███░░░░░░░░░░░░░░░░░]   234         │
│  Dead:         [█░░░░░░░░░░░░░░░░░░░]    12         │
│                                                      │
│  Oldest Pending: 2 minutes ago                      │
│  Success Rate: 99.4%                                │
│  Avg Delivery Time: 1.2s                            │
│                                                      │
└──────────────────────────────────────────────────────┘
```

## Configuration Examples

### Development (Default)
```json
{
  "ActivityPub": {
    "EnableActivityDelivery": true,
    "BaseUrl": "https://localhost:7001"
  }
}
```

### Production with Monitoring
```json
{
  "ActivityPub": {
    "EnableActivityDelivery": true,
    "BaseUrl": "https://myserver.com",
    "DefaultCollectionPageSize": 20
  },
  "Logging": {
    "LogLevel": {
      "Broca.ActivityPub.Server.Services.ActivityDeliveryService": "Information",
      "Broca.ActivityPub.Server.Services.ActivityDeliveryWorker": "Information"
    }
  }
}
```

### Disabled Delivery (Testing)
```json
{
  "ActivityPub": {
    "EnableActivityDelivery": false
  }
}
```
