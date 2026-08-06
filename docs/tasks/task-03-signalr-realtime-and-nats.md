# Task 03: In-Process Event Distribution & SignalR Real-Time Push

> **REWRITTEN 2026-08-06** per [`architecture/decision-log.md`](../architecture/decision-log.md) **D2**. NATS JetStream is removed. This file fully replaces the previous NATS-based spec; the filename is kept for link stability. Event distribution is: transactional outbox (PostgreSQL) → in-process dispatcher (`BackgroundService`) → SignalR typed hub → browser. Delivery guarantee is **at-least-once with client-side deduplication**. Exactly-once is explicitly NOT claimed anywhere in this task.

- **Prerequisites**: Task 01 (schema incl. `outbox_events` additions below), Task 02 (API host, JWT auth, DI, `NpgsqlDataSource`)
- **Consumed by**: Task 05 (frontend SignalR client), Task 09 (integration tests)
- **Complexity**: Medium

---

## 1. Scope

### In scope
1. `outbox_events` dispatch pipeline: `OutboxDispatcher` background service with `LISTEN/NOTIFY` wake-up and transactional batch claiming.
2. SignalR hub `DashboardPushHub` with a **typed client interface** (`IDashboardPushClient`) and MessagePack protocol.
3. Entity-type group subscriptions with server-side validation.
4. Reconnect catch-up: `GET /api/v1/events` cursor endpoint reading from `outbox_events`.
5. Integration tests (Testcontainers, real PostgreSQL 17).

### Out of scope
- NATS / any external broker (D2). Scale-out beyond one API instance (documented as future path in §7).
- Client-side consumption details beyond the dedup contract (Task 05).

### Schema dependencies on Task 01 (MUST exist before this task)
Task 01's `outbox_events` table MUST include, in addition to its existing columns:

```sql
-- Monotonic dispatch/catch-up cursor. UUIDs are not orderable; this is.
sequence_id BIGSERIAL NOT NULL UNIQUE,

-- Wake the dispatcher without polling.
CREATE OR REPLACE FUNCTION notify_outbox_new_event() RETURNS trigger AS $$
BEGIN
  PERFORM pg_notify('outbox_new_event', NEW.event_id::text);
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_outbox_notify
AFTER INSERT ON outbox_events
FOR EACH ROW EXECUTE FUNCTION notify_outbox_new_event();
```

If Task 01 is already implemented without these, add them in migration `src/Database/Migrations/00X_outbox_dispatch_support.sql` as part of this task.

---

## 2. Deliverables (exact files)

| Path | Content |
| :--- | :--- |
| `src/Backend/src/Tradebook.Api/RealTime/IDashboardPushClient.cs` | Typed client interface (§3.1) |
| `src/Backend/src/Tradebook.Api/RealTime/DashboardPushHub.cs` | Hub (§3.2) |
| `src/Backend/src/Tradebook.Api/RealTime/SignalRRegistration.cs` | DI + MessagePack setup (§3.4) |
| `src/Backend/src/Tradebook.Infrastructure/Outbox/OutboxEventRecord.cs` | Dispatch DTO (§3.3) |
| `src/Backend/src/Tradebook.Infrastructure/Outbox/OutboxDispatcher.cs` | Background service (§3.3) |
| `src/Backend/src/Tradebook.Api/Features/Events/GetEventsSinceEndpoint.cs` | Catch-up endpoint (§4) |
| `tests/Tradebook.IntegrationTests/RealTime/OutboxDispatchTests.cs` | §6 tests |
| `tests/Tradebook.IntegrationTests/RealTime/CatchUpEndpointTests.cs` | §6 tests |

---

## 3. Implementation contracts

### 3.1 Typed client interface

```csharp
namespace Tradebook.Api.RealTime;

public interface IDashboardPushClient
{
    /// <summary>Pushed for every committed mutation. payloadJson is the outbox payload verbatim.</summary>
    Task EntityChanged(Guid eventId, long sequenceId, string aggregateType, Guid aggregateId,
                       string eventType, string payloadJson);
}
```

This is the ONLY push contract. Do not add a second, generic event envelope; Task 05 consumes exactly this signature (via TypeGen-generated types, Task 08).

### 3.2 Hub

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Tradebook.Api.RealTime;

[Authorize] // JWT bearer; no anonymous connections
public sealed class DashboardPushHub : Hub<IDashboardPushClient>
{
    // Server-side whitelist — reject anything else.
    private static readonly HashSet<string> AllowedGroups = new(StringComparer.Ordinal)
    {
        "entity:PhysicalDelivery", "entity:Contract", "entity:CapacityBooking",
        "entity:GooCertificateTransaction", "entity:MarketPrice", "entity:Hedge",
    };

    public async Task Subscribe(string group)
    {
        if (!AllowedGroups.Contains(group))
            throw new HubException($"Unknown subscription group '{group}'.");
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
    }

    public Task Unsubscribe(string group)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
}
```

Rules:
- The hub holds **no instance state** (hubs are transient per invocation). All fan-out happens through `IHubContext<DashboardPushHub, IDashboardPushClient>` from the dispatcher.
- No backpressure channel inside the hub. SignalR's own per-connection buffering applies; slow-client protection is `MaximumReceiveMessageSize` defaults plus the batch cap in §3.3.

### 3.3 Outbox dispatcher

Use the implementation from `architecture/master-architecture-blueprint.md` §4.3 verbatim as the contract (class `OutboxDispatcher`). Normative behavior an implementer MUST preserve:

1. Dedicated `LISTEN outbox_new_event` connection; wake on NOTIFY or a 1-second fallback (`listenConn.WaitAsync(TimeSpan.FromSeconds(1), ct)`).
2. Batch claim of ≤100 rows **inside an open transaction** with `FOR UPDATE SKIP LOCKED`, ordered by `sequence_id`.
3. Fan-out to `hub.Clients.Group($"entity:{AggregateType}")` for every row, then `UPDATE ... SET processed_at = clock_timestamp() WHERE event_id = ANY(@Ids)` **in the same transaction**, then commit.
4. On any exception: log, 2-second backoff, loop. Never mark rows processed on a failed dispatch batch.
5. `OutboxEventRecord` fields: `EventId (Guid)`, `SequenceId (long)`, `AggregateType (string)`, `AggregateId (Guid)`, `EventType (string)`, `Payload (string)`.

`AggregateType` values are the PascalCase entity names matching §3.2's group whitelist (e.g. `PhysicalDelivery`). Task 02's mutation endpoints write these exact strings into `outbox_events.aggregate_type`; a mismatch means silent non-delivery, so §6 test T5 asserts the mapping for every mutable entity.

### 3.4 Registration (Program.cs additions, Task 02 owns the file)

```csharp
builder.Services.AddSignalR().AddMessagePackProtocol();
builder.Services.AddHostedService<OutboxDispatcher>();
// ...
app.MapHub<DashboardPushHub>("/hubs/dashboard");
```

JWT for WebSockets: configure `JwtBearerEvents.OnMessageReceived` to read `access_token` from the query string for paths starting with `/hubs/` (standard SignalR pattern), since browsers cannot set Authorization headers on WebSocket upgrade.

---

## 4. Reconnect catch-up endpoint

Clients that reconnect missed pushes. Source of truth is `outbox_events` itself — no second event store.

- **Route**: `GET /api/v1/events?afterSequence={long}&limit={int<=500}` (default limit 500)
- **Auth**: same JWT policy as the hub.
- **Response**: `{ "events": [ { eventId, sequenceId, aggregateType, aggregateId, eventType, payloadJson } ], "latestSequence": long }` ordered by `sequenceId` ascending. `latestSequence` = `MAX(sequence_id)` in the table (0 when empty).
- **Client protocol (Task 05 contract)**: persist the highest seen `sequenceId`; on reconnect, call repeatedly until `events.length < limit`, applying dedup; then resume live handling. Dedup: LRU set of the last 10,000 `eventId`s; drop duplicates silently.
- Rows are returned regardless of `processed_at` — a row not yet dispatched will also arrive live and be deduped.

---

## 5. Configuration

| Setting (appsettings key) | Default | Meaning |
| :--- | :--- | :--- |
| `Outbox:BatchSize` | 100 | Max rows per claim transaction |
| `Outbox:FallbackPollSeconds` | 1 | LISTEN wait timeout |
| `Outbox:ErrorBackoffSeconds` | 2 | Delay after a failed batch |

Single API instance is assumed (Tier 1, decision-log D9); SignalR needs no backplane. Scale-out path (documented, not implemented): add a Redis backplane or reintroduce a broker per D2's re-entry condition — the outbox producer side needs zero changes.

---

## 6. Verification (all executable; no absolute latency gates — D10)

Integration tests use Testcontainers `postgres:17` with the Task 01 migrations applied, the real API host (`WebApplicationFactory` bound to the container), and a real SignalR client (`Microsoft.AspNetCore.SignalR.Client` + MessagePack).

| # | Test | Assertion |
| :--- | :--- | :--- |
| T1 | Insert delivery via `POST /api/v1/deliveries` → subscribed client | Client receives `EntityChanged` with matching `aggregateId` within 5s; outbox row has `processed_at IS NOT NULL` |
| T2 | Unauthenticated hub connection | Connection rejected (401/negotiate failure) |
| T3 | `Subscribe("entity:Nope")` | `HubException` surfaced to caller |
| T4 | At-least-once: stop dispatcher after fan-out, before commit (test hook: inject failing `ExecuteAsync` on the mark-processed statement, then restart service) | Event delivered ≥1 time; after restart, row is re-dispatched and marked processed; client dedup keeps exactly one application |
| T5 | For each mutable entity (PhysicalDelivery, Contract, CapacityBooking, GooCertificateTransaction, Hedge): mutate → group delivery | `aggregate_type` string matches a whitelisted group; a subscribed client receives it (catches SubjectMap-style drift) |
| T6 | Catch-up: write 25 events with dispatcher stopped → `GET /api/v1/events?afterSequence=0&limit=10` | 3 pages, ordered, complete; `latestSequence` correct |
| T7 | NOTIFY wake-up: with dispatcher idle, insert one outbox row directly via SQL | Delivered without waiting for a full fallback interval (assert delivery < fallback×3 — generous, not a perf gate) |

Record (do not gate on) the measured insert→push latency in the test log output.

## 7. Anti-cheating rules

- Tests MUST run against real PostgreSQL (Testcontainers). In-memory fakes of the outbox are forbidden.
- No test may pass by polling the DB instead of receiving an actual SignalR message.
- Do not catch-and-ignore in the dispatcher beyond the documented backoff path; swallowing exceptions to green a test is a spec violation.
- The catch-up endpoint MUST be the same code path the frontend uses (no test-only endpoint).
