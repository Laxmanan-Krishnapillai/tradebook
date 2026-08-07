# Task 17: Wolverine Messaging & Durable Transactional Outbox

> **GREENFIELD MODERNIZATION TASK (2026-08-07)** — This task supersedes the hand-rolled in-process transactional outbox delivered by Task 03 under [`architecture/decision-log.md`](../architecture/decision-log.md) D2. Wolverine 5.x's durable PostgreSQL inbox/outbox — persisted on the existing Dapper/Npgsql connection with **no EF Core and no Marten** — replaces the custom `BackgroundService` that used Postgres `LISTEN`/`NOTIFY` + `SELECT … FOR UPDATE SKIP LOCKED` to claim and dispatch outbox rows. SignalR + MessagePack **remains** the browser push transport; Wolverine handlers, not a bespoke dispatcher, fan domain events out to the hubs. The Task 03 event envelope + per-group sequence + dedup + catch-up contract is reimplemented on Wolverine handlers, never lost. This is a committed, repo-wide adoption across the entire backend.

- **Phase**: Backend Messaging Modernization (supersedes Task 03; lands before the Task 09/Task 10 verification waves)
- **Lead / Owner**: Messaging & Realtime Platform Specialist
- **Complexity**: High
- **Prerequisites**: Task 13 (coordinates Task 15 value objects inside messages, Task 16 event DTOs/contracts; supersedes Task 03's outbox)
- **Status**: Specified
- **Target Files**:
  - `src/Backend/src/Tradebook.Api/Program.cs` (Wolverine host + durable Postgres outbox/inbox; keep existing auth, FastEndpoints, SignalR) and `Tradebook.Api.csproj` (+ `WolverineFx`, `WolverineFx.Postgresql`)
  - `src/Backend/src/Tradebook.Core/Messaging/` (domain-event contracts from Task 16; value objects from Task 15)
  - `src/Backend/src/Tradebook.Api/Features/**` (command endpoints publish domain events via Wolverine transactional middleware)
  - `src/Backend/src/Tradebook.Api/RealTime/` (Wolverine → SignalR push handlers; keep `AddDashboardPush`/`MapDashboardPushHub` + MessagePack)
  - `src/Backend/src/Tradebook.Infrastructure/Outbox/` (**DELETED** — the hand-rolled `BackgroundService`/`LISTEN`-`NOTIFY`/`SKIP LOCKED` dispatcher is removed)
  - `src/Database/Migrations/0XX_wolverine_durability.sql`, `src/Database/Migrations/0XX_realtime_event_log.sql`
  - `tests/Tradebook.IntegrationTests/RealTime/` and `tests/Tradebook.IntegrationTests/Tradebook.IntegrationTests.csproj`

---

## 1. Detailed Scope & Feature Coverage

### 1.1 Problem Statement

Task 03 shipped a correct but bespoke transactional outbox (D2): an in-process `BackgroundService` woken by Postgres `LISTEN`/`NOTIFY`, claiming rows with `SELECT … FOR UPDATE SKIP LOCKED` inside an open transaction, dispatching to the SignalR fan-out, then marking `processed_at` and committing. Its retry, dead-letter, dedup, and durability semantics are hand-maintained project code the team must own forever. Wolverine 5.x provides that durability as a library on plain Npgsql, so the whole backend adopts it and the custom dispatcher is deleted.

### 1.2 Required Outcomes

- **Adopt Wolverine 5.x** for in-process messaging plus the **durable PostgreSQL outbox/inbox** via `opts.PersistMessagesWithPostgresql(connectionString)` on the existing Dapper/Npgsql connection — **no EF Core, no Marten**. Wolverine's durability tables are auto-provisioned on startup or applied as a SQL migration.
- **Every command endpoint** publishes its domain events through Wolverine under **transactional middleware**, so the Dapper write and the outgoing messages commit atomically in one Npgsql transaction; consumers are idempotent via inbox dedupe, preserving the at-least-once + idempotent guarantee.
- **Delete the custom dispatcher** under `src/Backend/src/Tradebook.Infrastructure/Outbox/`; no hand-rolled outbox remains anywhere.
- **SignalR stays the browser transport**: Wolverine handlers consume domain events and push to the SignalR hubs (keeping MessagePack), reimplementing the Task 03 envelope + per-group sequence + dedup + catch-up.
- **The `audit_log` PL/pgSQL triggers stay unchanged and authoritative** — audit rows remain DB-written inside the command transaction.

### 1.3 In Scope

- Add two pinned packages (`WolverineFx`, `WolverineFx.Postgresql`); wire `UseWolverine` + the Postgres store + durable local queues + resource auto-setup; convert every write slice under `Features/**` to publish domain events through the outbox enlisted in the command's Npgsql transaction.
- Reimplement the realtime fan-out as Wolverine handlers holding `IHubContext` (preserving envelope, sequence, dedup, catch-up), add the `realtime_event_log` table + catch-up query, and cover rollback, exactly-once, SignalR end-to-end, and a dispatch-latency baseline with Testcontainers PostgreSQL 17.

### 1.4 Out of Scope

- An external broker (NATS, Kafka, Azure Service Bus) or a second consumer process — that remains the D2 re-entry trigger; Wolverine runs in-process only here.
- EF Core, Marten, or any ORM; the store runs on the raw `NpgsqlDataSource`.
- Changing the `audit_log` triggers/schema (Task 01), the OCC `version` contract, or the wire shape of the Task 03 client envelope.
- Defining new event DTOs or value objects — owned by Task 16 and Task 15; Task 17 consumes them.

---

## 2. Key Deliverables & File Layout

```text
src/Backend/src/
├── Tradebook.Api/
│   ├── Program.cs                      # UseWolverine + PersistMessagesWithPostgresql + AddResourceSetupOnStartup
│   ├── Features/                       # write slices publish domain events via the Wolverine outbox
│   └── RealTime/                       # existing MessagePack hub + AddDashboardPush/MapDashboardPushHub (unchanged)
│       └── Handlers/DeliveryRealtimeHandler.cs   # new Wolverine handler → SignalR (only path to the hub)
├── Tradebook.Core/
│   └── Messaging/                      # domain-event records (Task 16) using value objects (Task 15)
└── Tradebook.Infrastructure/
    └── Outbox/                         # DELETED — hand-rolled BackgroundService dispatcher removed
src/Database/Migrations/
├── 0XX_wolverine_durability.sql        # wolverine_* tables (if not auto-provisioned)
└── 0XX_realtime_event_log.sql          # per-group sequence + dedup + catch-up table
tests/Tradebook.IntegrationTests/RealTime/
├── OutboxTransactionTests.cs           # rollback ⇒ no message; commit ⇒ exactly-once
├── IdempotentConsumerTests.cs          # simulated duplicate ⇒ single effect
├── SignalREndToEndTests.cs             # domain write ⇒ subscribed client, sequence + catch-up
└── DispatchLatencyBaselineTests.cs     # recorded baseline (D10, regression-style)
```

---

## 3. Architecture & Code Contract Blueprints

### 3.1 Wolverine Host + Durable Postgres Outbox/Inbox (`Program.cs`)

```csharp
// .NET 10 host: Wolverine owns messaging + the durable Postgres inbox/outbox. No EF Core, no Marten.
builder.Host.UseWolverine(opts =>
{
    opts.PersistMessagesWithPostgresql(builder.Configuration.GetConnectionString("Tradebook")!, "wolverine");
    opts.Policies.UseDurableLocalQueues();      // survives a crash; dispatch is at-least-once
    opts.Policies.AutoApplyTransactions();       // handlers run inside the durable outbox transaction
    opts.OnException<NpgsqlException>().ScheduleRetry(5.Seconds());
});
builder.Services.AddResourceSetupOnStartup();    // auto-provision wolverine_* tables (or apply the §2 migration)
```

### 3.2 Transactional Command Handler Publishing a Domain Event

The write and the outgoing message share one Npgsql transaction: rollback ⇒ no message, commit ⇒ the event lands in `wolverine_outgoing_envelopes` exactly once. The `audit_log` triggers fire in the same transaction.

```csharp
public sealed class RecordDeliveryEndpoint(NpgsqlDataSource dataSource, IMessageBus bus)
    : Endpoint<RecordDeliveryRequest, DeliveryResponse>
{
    public override async Task HandleAsync(RecordDeliveryRequest req, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await bus.EnlistInOutboxAsync(tx);                        // enlist the outbox in this ADO.NET tx
        var delivery = await conn.QuerySingleAsync<DeliveryRow>(  // Dapper write (+ audit trigger)
            Sql.InsertDelivery, req.ToParameters(), tx);
        await bus.PublishAsync(new DeliveryRecorded(delivery.Id, delivery.Version, req.CompanyId));
        await tx.CommitAsync(ct);                                 // atomic: row + outbox envelope
        await SendAsync(delivery.ToResponse(), cancellation: ct);
    }
}
```

### 3.3 Wolverine Handler Pushing to the SignalR Hub

This is the **only** path to the hub — idempotent (inbox dedupe + `realtime_event_log` upsert), preserving the envelope + sequence + dedup + catch-up contract.

```csharp
public sealed class DeliveryRealtimeHandler(
    IHubContext<DashboardPushHub, IDashboardClient> hub, NpgsqlDataSource dataSource)
{
    public async Task Handle(DeliveryRecorded message, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        // Allocate the next per-group sequence + record the id; ON CONFLICT (event_id) DO NOTHING ⇒ no-op.
        var envelope = await conn.QuerySingleOrDefaultAsync<RealtimeEnvelope>(Sql.AppendRealtimeEvent,
            new { message.EventId, Group = $"delivery-{message.CompanyId}",
                  AggregateType = "PhysicalDelivery", Payload = message.ToEnvelopePayload() });
        if (envelope is null) return;                    // already dispatched — dedupe, no double push
        await hub.Clients.Group(envelope.Group)          // MessagePack transport unchanged
            .DeliveryChanged(envelope);                  // { eventId, aggregateType, sequence, payload }
    }
}
```

### 3.4 Sequence / Idempotency Table (`realtime_event_log`)

```sql
-- Preserves the Task 03 envelope + per-group sequence + dedup + catch-up on top of Wolverine.
CREATE TABLE realtime_event_log (
    event_id       UUID        PRIMARY KEY,             -- dedupe key (== domain event id)
    group_name     TEXT        NOT NULL,                -- SignalR fan-out group
    aggregate_type TEXT        NOT NULL,                -- PhysicalDelivery | Contract | MarketPrice …
    sequence       BIGINT      NOT NULL,                -- per-group monotonic sequence
    payload        JSONB       NOT NULL,                -- envelope body replayed on catch-up
    occurred_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (group_name, sequence)                       -- catch-up: sequence > $last ORDER BY sequence
);
```

Primary references: [Wolverine durability guide](https://wolverinefx.net/guide/durability/), [Wolverine PostgreSQL persistence](https://wolverinefx.net/guide/durability/postgresql.html), the superseded [`tasks/task-03-signalr-realtime-and-nats.md`](task-03-signalr-realtime-and-nats.md), and [`architecture/decision-log.md`](../architecture/decision-log.md) D2 (outbox) and D10 (verification honesty).

---

## 4. Subagent Implementation Step-by-Step Workflow

1. **Capture the baseline** — run `dotnet build` and the integration suite before editing; record package versions and preserve unrelated working-tree changes.
2. **Add Wolverine** — reference `WolverineFx` + `WolverineFx.Postgresql`; wire `UseWolverine`, `PersistMessagesWithPostgresql`, durable queues, and `AddResourceSetupOnStartup` as in §3.1.
3. **Provision durability tables** — verify auto-setup creates the `wolverine_*` tables against a fresh PG17 container, or add `0XX_wolverine_durability.sql`.
4. **Migrate write slices** — convert every `Features/**` command to open an Npgsql tx, `EnlistInOutboxAsync`, do its Dapper write, publish its domain event, and commit (§3.2).
5. **Reimplement the fan-out** — move SignalR pushes into Wolverine handlers under `RealTime/Handlers/`; add `realtime_event_log` and its append/catch-up SQL; keep envelope, sequence, dedup, and catch-up.
6. **Delete the dispatcher** — remove `src/Backend/src/Tradebook.Infrastructure/Outbox/` entirely; drop any `LISTEN`/`NOTIFY`/`SKIP LOCKED` code and DI registrations.
7. **Add integration tests** — Testcontainers PG17 for rollback, exactly-once under a simulated duplicate, SignalR end-to-end with sequence + catch-up, and a recorded dispatch-latency baseline.
8. **Run the full §5 workflow** and update the backend `AGENTS.md` and `docs/tasks/README.md` only where paths changed.

---

## 5. Independent Verification & Acceptance Workflow

### 5.1 Verification Commands

```bash
# Build the backend; Wolverine packages resolve and no Marten/EF Core enters the graph.
dotnet build src/Backend/Tradebook.sln -c Release

# Outbox/inbox + SignalR end-to-end suite (Testcontainers PostgreSQL 17).
dotnet test tests/Tradebook.IntegrationTests --filter Category=RealTime
# Prove the hand-rolled dispatcher is gone and no ORM was introduced.
rg -n "BackgroundService|LISTEN|NOTIFY|SKIP LOCKED|FOR UPDATE" src/Backend/src/Tradebook.Infrastructure
rg -n "Marten|EntityFrameworkCore" src/Backend
test ! -d src/Backend/src/Tradebook.Infrastructure/Outbox
# Confirm the Wolverine durability resources exist (JasperFx/Wolverine CLI).
dotnet run --project src/Backend/src/Tradebook.Api -- resources check
```

The `rg` audits must return no production-source matches; the directory test and `resources check` exit zero. All acceptance tests run against Testcontainers PostgreSQL 17.

### 5.2 Acceptance Criteria

| ID | Acceptance criterion | Evidence |
| :--- | :--- | :--- |
| **MSG-01** | A rolled-back command produces **no** outgoing message and no SignalR push. | Testcontainers test throwing after the Dapper write; `wolverine_outgoing_envelopes` empty |
| **MSG-02** | A committed command delivers its event **exactly once** to an idempotent consumer, even under a simulated duplicate (inbox dedupe + `realtime_event_log`). | Same envelope id dispatched twice ⇒ single effect + one log row |
| **MSG-03** | A domain write reaches a subscribed SignalR client with correct sequence, and a reconnect replays catch-up from the last sequence. | MessagePack client end-to-end test |
| **MSG-04** | No hand-rolled outbox remains; `Infrastructure/Outbox/` is deleted and no `LISTEN`/`NOTIFY`/`SKIP LOCKED` dispatcher survives. | `rg` audit + directory-absent test |
| **MSG-05** | No EF Core and no Marten in the dependency graph; only `WolverineFx` + `WolverineFx.Postgresql` were added. | `rg` audit + `dotnet list package` |
| **MSG-06** | `audit_log` rows are still written by the DB triggers inside the command transaction; a rollback writes none. | Integration test asserting audit rows on commit, none on rollback |
| **MSG-07** | SignalR is reached **only** from a Wolverine handler path. | ArchUnitNET boundary rule + `rg` on `IHubContext` usage |
| **MSG-08** | Dispatch latency is recorded as a measured baseline (D10); a run fails only on a >20% regression, never an absolute gate. | Recorded baseline artifact on the reference machine |

---

## 6. Anti-Cheating & Integrity Guardrails

1. Leave no hand-rolled outbox anywhere — `src/Backend/src/Tradebook.Infrastructure/Outbox/` is deleted, and no `BackgroundService`/`LISTEN`-`NOTIFY`/`SKIP LOCKED`/`FOR UPDATE` dispatcher survives.
2. Do not introduce EF Core or Marten; persistence runs through `PersistMessagesWithPostgresql` on the existing `NpgsqlDataSource`, and only `WolverineFx` + `WolverineFx.Postgresql` are added.
3. Every message consumer MUST be idempotent — rely on inbox dedupe by envelope id plus the `realtime_event_log` upsert; guarantee an exactly-once *effect*, never assume exactly-once *delivery*.
4. Do not lose the Task 03 sequence/dedup/catch-up contract — the envelope shape, per-group monotonic sequence, client dedup, and catch-up replay are reimplemented on Wolverine handlers.
5. Keep the `audit_log` PL/pgSQL triggers unchanged and authoritative; audit rows stay DB-written inside the command transaction and are never moved into a handler.
6. Never push to SignalR outside a Wolverine handler path — endpoints publish domain events; only `RealTime/Handlers/**` hold `IHubContext`; enforce it with an ArchUnitNET rule.
7. The Dapper write and the outbox enlistment share one Npgsql transaction (rollback ⇒ zero messages); keep SignalR + MessagePack as the transport — Wolverine stays in-process, the broker being the D2 re-entry trigger.
8. Record the dispatch-latency baseline per D10 (no absolute gate; fail only on a >20% regression), and do not mark the task Implemented until every §5 command has run against Testcontainers PostgreSQL 17 and can fail with a non-zero exit.
