# Task 10: Tradebook Platform Integration, Continuous Verification & Sentinel Master Blueprint

> **DESCOPE NOTICE (2026-08-06 — applied to this spec)** — per [`architecture/decision-log.md`](../architecture/decision-log.md): Merkle/WORM verification was deleted (**D6** — the C# and SQL roots were mathematically incompatible anyway; replaced by a backup-restore rehearsal check), all NATS checks were deleted (**D2** — realtime is Task 03's in-process outbox dispatcher + SignalR, spec kept under its legacy filename [`task-03-signalr-realtime-and-nats.md`](task-03-signalr-realtime-and-nats.md)), `/api/v1/mutations/batch` and `perform3WayMerge` were deleted (**D5** — sync is per-entity REST with `version`-column optimistic concurrency), TimescaleDB references were deleted (**D3** — Task 01's spec keeps its legacy filename [`task-01-database-and-timescaledb-setup.md`](task-01-database-and-timescaledb-setup.md)), Native AOT was deleted (**D7**), the 35,000 req/sec gate and every other absolute performance gate were deleted (**D10** — Task 09's k6 baseline-regression model applies, [`task-09-e2e-testing-and-nbomber-harness.md`](task-09-e2e-testing-and-nbomber-harness.md), legacy filename), and JWT remains mandatory for business and realtime routes while the binding repository guide exempts the two health probes and login. Verify scripts never wrap checks in `|| true` or any other error suppression — a check that cannot fail is not a check. All cross-links resolve against the post-descope file set in `docs/tasks/` under their legacy filenames.

- **Phase**: Master Platform Integration, Continuous Verification & Production Sentinel
- **Lead / Owner**: Principal Systems Architect & Lead QA Engineer
- **Complexity**: High
- **Prerequisites**: Tasks 01–09 (all specs in `docs/tasks/`, legacy filenames — the renamed-content files are linked in the notice above)
- **Target Files**:
  - `docs/tasks/task-10-platform-integration-master-blueprint.md`
  - `src/Backend/src/Tradebook.Api/Features/Health/HealthEndpoints.cs`
  - `scripts/platform-verify.sh`
  - `scripts/backup-restore-rehearsal.sh`
  - `scripts/production-runbook.sh`

---

## 1. Objectives, Scope, Dependencies & Prerequisites

### 1.1 Objectives
1. **End-to-End System Integration**: Seamlessly connect all Tradebook layers — PostgreSQL 17 System of Record (plain, no extensions beyond `btree_gist`), .NET 10 FastEndpoints REPR API in a standard JIT container, the in-process outbox dispatcher, SignalR MessagePack push hub, React 19 SPA with optimistic mutations, and the `ChartAdapter` visualization layer.
2. **Backup Integrity via Rehearsal**: Replace cryptographic audit verification with an executable **backup-restore rehearsal**: restore the latest nightly `pg_dump` into a fresh `postgres:17` container and compare per-table row counts against the source. A mismatch fails the run.
3. **Optimistic Concurrency Integrity**: Server-authoritative conflict handling via the `version BIGINT` column — stale writes receive HTTP 409 and the client shows a conflict prompt. There is no client-side merge engine.
4. **Full Platform Production Launch Runbook & Environment Verification**: Automated pre-flight environment checks, Terraform Tier 1 provisioning (Azure, D14), Azure Container Apps ingress TLS and WebSocket orchestration, and container health probes (`/health/live`, `/health/ready`).
5. **Continuous Agent Verification Protocol & Sentinel Master Acceptance Criteria**: Automated CI/CD guardrails (TypeGen DTO zero-drift, ArchUnitNET boundaries, Stryker mutation testing ≥80%, Playwright E2E suites, k6 baseline-regression checks per Task 09) and a 10-domain Sentinel Master Acceptance Criteria Matrix.

### 1.2 Scope
- **In-Scope**: End-to-end topology wiring, payload contracts, backup-restore rehearsal tooling, optimistic-concurrency conflict contract, environment boot scripts, health probe endpoints, production deployment runbooks, agent verification protocols, sentinel acceptance matrix, and anti-cheating verification mandates.
- **Out-of-Scope**: Writing custom third-party cloud infrastructure components outside Terraform definitions.

### 1.3 Dependencies & Prerequisites
- **Task 01**: Plain PostgreSQL 17 schema — bi-temporal `audit_log` (`TSTZRANGE` + `btree_gist` exclusion), `outbox_events` (including `sequence_id BIGSERIAL` and the `outbox_new_event` NOTIFY trigger), `version BIGINT` on mutable entities. Migrations live in `src/Database/Migrations`.
- **Task 02**: .NET 10 FastEndpoints modular monolith (standard JIT container), JWT policies on every endpoint except the two health probes and login, FluentValidation, per-entity REST mutations with `version`-column optimistic concurrency.
- **Task 03**: `OutboxDispatcher` background service (`LISTEN outbox_new_event`), `DashboardPushHub` at `/hubs/dashboard` (MessagePack, typed client), catch-up endpoint `GET /api/v1/events`.
- **Task 04**: Semantic layer single query path: JSON AST → C# `SemanticQueryCompiler` → parameterized SQL → JSON result set; identifier whitelist enforced.
- **Task 05**: React 19 + Vite SPA, TanStack Query v5 optimistic mutations with rollback, HTTP 409 conflict prompt flow, cmdk command palette, in-memory session-scoped undo/redo.
- **Task 06**: `ChartAdapter` contract + registry; Apache ECharts + TradingView Lightweight Charts engines; LTTB Web Worker downsampling.
- **Task 07**: Terraform Tier 1 modules under `infra/terraform` (Azure per D14), root `docker-compose.yml` (`postgres:17` + api only), multi-stage Dockerfile.
- **Task 08**: Root `AGENTS.md` context maps, TypeGen C#-to-TS pipeline (`tgconfig.json`, output `src/Frontend/src/api/generated`), ArchUnitNET tests in `tests/Tradebook.ArchitectureTests`, Stryker.NET break threshold 80.
- **Task 09**: Playwright E2E suite (`tests/e2e`) and k6 baseline-regression harness (`tests/performance`, committed `baseline.json`).

---

## 2. End-to-End System Integration Flow & Data Topology

### 2.1 End-to-End System Integration Architecture Diagram

```
+-----------------------------------------------------------------------------------------------------------------------+
|                                    TRADEBOOK END-TO-END SYSTEM INTEGRATION TOPOLOGY                                   |
+-----------------------------------------------------------------------------------------------------------------------+
|                                                                                                                       |
|   +---------------------------------------------------------------------------------------------------------------+   |
|   |                                        REACT 19 FRONTEND SPA                                                  |   |
|   |  - Ephemeral UI State: Zustand store (active modal, focused cell, sidebar)                                    |   |
|   |  - Entity Cache: TanStack Query v5 — per-mutation optimistic updates with rollback on error (D5)              |   |
|   |  - Concurrency UX: HTTP 409 on stale version -> refetch -> conflict prompt; in-memory undo/redo stack         |   |
|   |  - Analytics: JSON AST -> POST /api/v1/analytics/query (single server-side query path, D4)                    |   |
|   |  - Visualizations: ChartAdapter registry -> ECharts + Lightweight Charts; LTTB worker downsampling (D8)       |   |
|   +---------------------------------------------------------------------------------------------------------------+   |
|                              |                                                     ^                                  |
|            HTTPS REST (per-entity /api/v1/*)                        SignalR WebSocket push (/hubs/dashboard)          |
|            (JWT bearer; validated payloads)                         (binary MessagePack; JWT via accessTokenFactory)  |
|                              v                                                     |                                  |
|   +---------------------------------------------------------------------------------------------------------------+   |
|   |                                  AZURE CONTAINER APPS INGRESS & TLS TERMINATION                                        |   |
|   |  - Managed TLS and WebSocket ingress for /hubs/*; the API serves static frontend assets                                      |   |
|   +---------------------------------------------------------------------------------------------------------------+   |
|                              |                                                                                        |
|                              v                                                                                        |
|   +---------------------------------------------------------------------------------------------------------------+   |
|   |                                .NET 10 FASTENDPOINTS MODULAR MONOLITH (JIT container, D7)                      |   |
|   |  - REPR Endpoint Slices (Request -> Endpoint -> Response) with FluentValidation                               |   |
|   |  - JWT policies on every business and realtime endpoint; actor identity from the token `sub` claim only                       |   |
|   |  - SignalR DashboardPushHub (typed client, MessagePack)                                                       |   |
|   |  - OutboxDispatcher BackgroundService: LISTEN outbox_new_event -> claim -> hub fan-out (D2)                   |   |
|   +---------------------------------------------------------------------------------------------------------------+   |
|                              |                                                                                        |
|                 Npgsql / Dapper SQL writes (single atomic transaction per command)                                    |
|                              v                                                                                        |
|   +---------------------------------------------------------------------------------------------------------------+   |
|   |                              POSTGRESQL 17 CONSOLIDATED PRIMARY DATABASE (plain, D3)                          |   |
|   |  - Relational core domain entities (`contracts`, `physical_deliveries`) with `version BIGINT` (OCC, D5)       |   |
|   |  - Bi-Temporal Audit Log (`TSTZRANGE` + btree_gist exclusion)                                                 |   |
|   |  - Transactional Outbox (`outbox_events` with `sequence_id` cursor + NOTIFY trigger)                          |   |
|   +---------------------------------------------------------------------------------------------------------------+   |
|                              |                                                                                        |
|                 Nightly pg_dump                                                                                       |
|                              v                                                                                        |
|   +---------------------------------------------------------------------------------------------------------------+   |
|   |  VERSIONED OBJECT STORAGE (Azure Blob versioning + retention, D6/D14)                                         |   |
|   |  - Nightly dumps; restore rehearsal verifies them (scripts/backup-restore-rehearsal.sh)                       |   |
|   +---------------------------------------------------------------------------------------------------------------+   |
|                                                                                                                       |
+-----------------------------------------------------------------------------------------------------------------------+
```

### 2.2 Integration Layer Contracts, Protocols & Payloads

#### 1. Boundary A: PostgreSQL 17 -> .NET 10 REPR API Integration
- **Mechanism**: `NpgsqlDataSource` connection pooling combined with Dapper.
- **Auth**: every mutation endpoint is configured with a named policy (e.g. `Policies("TraderPolicy")`); the actor identity comes from the JWT `sub` claim **only** — never from the request body (D11).
- **Atomic Transaction Guarantee**: Every write command executes within a single PostgreSQL transaction wrapping:
  1. Primary domain entity mutation (e.g. `INSERT INTO physical_deliveries ...`).
  2. Bi-Temporal audit log append (`INSERT INTO audit_log ...` with `valid_time` and `system_time`).
  3. Outbox event enqueue (`INSERT INTO outbox_events ...`).
- **Optimistic Concurrency (D5)**: `UPDATE ... SET ..., version = version + 1 WHERE id = @Id AND version = @ExpectedVersion`; zero affected rows → HTTP 409 Conflict → client refetches and shows the conflict prompt. No silent client-wins, ever.

```csharp
// Transactional execution contract — one PostgreSQL transaction per write command.
// actorId is parsed from the JWT `sub` claim (D11) — never taken from the request body.
public async Task<CreatePhysicalDeliveryResponse> ExecuteAtomicDeliveryMutationAsync(
    CreatePhysicalDeliveryCommand cmd,
    Guid actorId,
    NpgsqlConnection conn,
    NpgsqlTransaction tx,
    CancellationToken ct)
{
    // 1. Insert Physical Delivery Domain Entity (version starts at 1)
    const string deliverySql = @"
        INSERT INTO physical_deliveries (id, contract_id, contract_instance_id, book_type, supply_month,
                                         volume_nominated_mwh, volume_realised_mwh, price_eur_mwh, status, version)
        VALUES (@DeliveryId, @ContractId, @ContractInstanceId, @BookType, @SupplyMonth,
                @VolumeNominatedMwh, @VolumeRealisedMwh, @PriceEurMwh, 'Pending - No Invoice', 1);";
    await conn.ExecuteAsync(deliverySql, cmd, tx);

    // Update-path OCC guard (D5): zero affected rows -> HTTP 409 -> client refetch + conflict prompt.
    //   UPDATE physical_deliveries SET ..., version = version + 1
    //   WHERE id = @Id AND version = @ExpectedVersion;

    // 2. Insert Bi-Temporal Audit Record
    const string auditSql = @"
        INSERT INTO audit_log (audit_id, entity_name, entity_id, actor_id, operation, valid_time, pre_state, post_state, diff_patch)
        VALUES (@AuditId, 'PhysicalDelivery', @EntityId, @ActorId, 'INSERT', tstzrange(@SupplyMonth, NULL, '[)'), NULL, @PostState::jsonb, @DiffPatch::jsonb);";
    await conn.ExecuteAsync(auditSql, new {
        AuditId = Guid.NewGuid(),
        EntityId = cmd.DeliveryId.ToString(),
        ActorId = actorId,
        cmd.SupplyMonth,
        PostState = JsonSerializer.Serialize(cmd),
        DiffPatch = JsonSerializer.Serialize(new[] { new { op = "add", path = "/", value = cmd } })
    }, tx);

    // 3. Insert Transactional Outbox Event.
    // aggregate_type is the PascalCase entity name and MUST match Task 03's hub group
    // whitelist ('entity:PhysicalDelivery') — a mismatch means silent non-delivery.
    const string outboxSql = @"
        INSERT INTO outbox_events (event_id, aggregate_type, aggregate_id, event_type, payload)
        VALUES (@EventId, 'PhysicalDelivery', @EntityId, 'PhysicalDeliveryCreated', @Payload::jsonb);";
    await conn.ExecuteAsync(outboxSql, new {
        EventId = Guid.NewGuid(),
        EntityId = cmd.DeliveryId,
        Payload = JsonSerializer.Serialize(cmd)
    }, tx);

    return new CreatePhysicalDeliveryResponse(cmd.DeliveryId, cmd.ContractInstanceId, null, "Pending - No Invoice", DateTimeOffset.UtcNow);
}
```

#### 2. Boundary B: Outbox Table -> In-Process Dispatcher (Task 03, D2)
- **Mechanism**: `OutboxDispatcher`, an in-process `BackgroundService`, woken by a dedicated `LISTEN outbox_new_event` connection (1-second fallback poll).
- **Claim semantics**: batches of ≤100 rows claimed with `SELECT ... FOR UPDATE SKIP LOCKED` **inside an open transaction**, ordered by `sequence_id`.
- **Completion**: fan-out to the SignalR hub, then `UPDATE ... SET processed_at = clock_timestamp() WHERE event_id = ANY(@Ids)` **in the same transaction**, then commit. On any exception: log, 2-second backoff, loop — rows are never marked processed on a failed batch.
- **Guarantee**: at-least-once delivery; consumers deduplicate by `eventId`. Exactly-once is explicitly NOT claimed.

#### 3. Boundary C: Dispatcher -> SignalR Hub (MessagePack Push)
- **Mechanism**: the dispatcher fans out through `IHubContext<DashboardPushHub, IDashboardPushClient>` to entity groups (`entity:{AggregateType}`); the hub itself holds no instance state.
- **The ONLY push contract** (Task 03 §3.1 — consumed by Task 05 via TypeGen-generated types):

```csharp
public interface IDashboardPushClient
{
    Task EntityChanged(Guid eventId, long sequenceId, string aggregateType, Guid aggregateId,
                       string eventType, string payloadJson);
}
```

- **Serialization**: `Microsoft.AspNetCore.SignalR.Protocols.MessagePack` binary protocol.
- **Route & auth**: `app.MapHub<DashboardPushHub>("/hubs/dashboard")`; `[Authorize]` — no anonymous connections; JWT read from the query string via `JwtBearerEvents.OnMessageReceived` for `/hubs/*` paths.

#### 4. Boundary D: SignalR Hub -> React 19 UI
- **Mechanism**: `@microsoft/signalr` + `@microsoft/signalr-protocol-msgpack`, JWT supplied via `accessTokenFactory`.
- **Coalescing**: incoming updates pass through an RxJS `bufferTime(50)` window before dispatching to TanStack Query / Zustand stores, preventing re-render churn during bursts (a smoothing mechanism, not an asserted frame-rate gate — D10).
- **Deduplication**: LRU set of the last 10,000 `eventId`s; duplicates dropped silently (at-least-once delivery, Boundary B).
- **Reconnect catch-up (Task 03 §4)**: the client persists the highest seen `sequenceId`; on reconnect it calls `GET /api/v1/events?afterSequence={N}&limit=500` repeatedly until a page returns fewer than `limit` events, applying dedup, then resumes live handling.
- **Conflict UX (D5)**: a 409 on any mutation rolls back the optimistic update, refetches server state, and shows the conflict prompt. There is no offline queue and no batch replay.

#### 5. Boundary E: React 19 UI -> Analytics & Visualizations
- **Analytics (D4)**: one query path — JSON AST → `POST /api/v1/analytics/query` → C# `SemanticQueryCompiler` → parameterized SQL → JSON result set. Every identifier is validated against the compiled model whitelist; filter values are parameterized (D11).
- **Visualizations (D8)**: the `ChartAdapter` contract (Task 06) with a registry keyed by chart type:
  - *Apache ECharts* — default engine (OLAP charts, KPI sparklines).
  - *TradingView Lightweight Charts* — price/candlestick views.
  - *Tremor* — React KPI component kit wrapped ad hoc (not an engine).
- **Downsampling**: datasets >100,000 points are LTTB-downsampled off-main-thread in a Web Worker before hitting an adapter.

---

## 3. Bi-Temporal Audit Integrity & Backup-Restore Rehearsal

### 3.1 Backup-Restore Rehearsal (replaces cryptographic audit verification, D6)

The audit story is: append-only, trigger-maintained bi-temporal `audit_log` + nightly `pg_dump` to versioned object storage (Azure Blob versioning + retention, D14). Verification is **executable and falsifiable**:

1. **Audit invariant check** — the bi-temporal exclusion constraint guarantees no overlapping `system_time` ranges per entity; the sentinel re-checks it explicitly so constraint drift is caught:

```sql
-- Must return zero rows; any row is a verification failure.
SELECT a.entity_name, a.entity_id, a.audit_id, b.audit_id
FROM audit_log a
JOIN audit_log b
  ON a.entity_name = b.entity_name
 AND a.entity_id = b.entity_id
 AND a.audit_id < b.audit_id
 AND a.system_time && b.system_time;
```

2. **Backup-restore rehearsal** — restore the latest nightly `pg_dump` into a **fresh `postgres:17` container** and compare per-table row counts against the source. The rehearsal runs inside the nightly backup job, immediately after the dump, so source counts have not drifted.

```bash
#!/usr/bin/env bash
# scripts/backup-restore-rehearsal.sh
# Restores the latest nightly dump into a fresh postgres:17 container and compares
# per-table row counts against the source database. Any mismatch fails the run.
set -euo pipefail

: "${DATABASE_URL:?DATABASE_URL missing}"   # source database
: "${BACKUP_PATH:?BACKUP_PATH missing}"     # latest nightly pg_dump (custom format)

CONTAINER="tradebook-restore-rehearsal"
if docker container inspect "$CONTAINER" >/dev/null 2>&1; then
  docker rm -f "$CONTAINER"
fi

docker run -d --name "$CONTAINER" -e POSTGRES_PASSWORD=rehearsal -p 54329:5432 postgres:17

for i in $(seq 1 30); do
  if docker exec "$CONTAINER" pg_isready -U postgres >/dev/null 2>&1; then break; fi
  sleep 1
done
docker exec "$CONTAINER" pg_isready -U postgres   # readiness must be provable — this line can fail the run

RESTORE_URL="postgresql://postgres:rehearsal@localhost:54329/postgres"
pg_restore --no-owner --dbname="$RESTORE_URL" "$BACKUP_PATH"

COUNT_SQL=$(cat <<'SQL'
SELECT c.relname,
       (xpath('/row/cnt/text()',
              query_to_xml(format('SELECT count(*) AS cnt FROM %I.%I', n.nspname, c.relname),
                           false, true, '')))[1]::text::bigint AS rows
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'r' AND n.nspname = 'public'
ORDER BY c.relname;
SQL
)

psql "$DATABASE_URL" -At -c "$COUNT_SQL" > /tmp/source-counts.txt
psql "$RESTORE_URL"  -At -c "$COUNT_SQL" > /tmp/restored-counts.txt

diff /tmp/source-counts.txt /tmp/restored-counts.txt   # non-empty diff exits non-zero and fails the run

echo "Backup-restore rehearsal PASSED: per-table row counts match the source."
docker rm -f "$CONTAINER"
```

**Re-entry note (D6)**: WORM/object-lock retention returns only when a written compliance requirement names it. Until then, blob versioning + this rehearsal is the whole story.

### 3.2 Optimistic Concurrency & Conflict Handling Contract (replaces the client merge engine, D5)

Offline editing is out of scope; there is no client-side merge engine, no offline mutation queue, and no batch replay endpoint. The concurrency contract is server-authoritative:

1. **Version guard**: every mutable entity carries `version BIGINT`. Updates execute `UPDATE ... SET ..., version = version + 1 WHERE id = @Id AND version = @ExpectedVersion`.
2. **Conflict signal**: zero affected rows → the endpoint returns **HTTP 409 Conflict** with the current server state in the problem-details body.
3. **Client behavior**: TanStack Query rolls back the optimistic update, refetches, and shows the conflict prompt; the user explicitly chooses to reapply or discard. **No silent client-wins, ever.**
4. **Undo/redo**: an in-memory, session-scoped command stack (`Cmd+Z` / `Cmd+Shift+Z`); it never persists across reloads.
5. **Auditability**: every committed attempt lands in `audit_log`; rejected stale writes never touch domain tables.

Verification of this contract is owned by Task 09's Tier 2 concurrent-edit E2E spec (two browser contexts, real 409, visible conflict prompt).

---

## 4. Full Platform Production Launch Runbook & Environment Verification Protocol

### 4.1 Production Launch Sequence & Rollout Runbook

```
+-------------------------------------------------------------------------------------------------------------------------+
|                                              PRODUCTION ROLLOUT SEQUENCE                                                |
+-------------------------------------------------------------------------------------------------------------------------+
| STEP 1: INFRASTRUCTURE & ENVIRONMENT PROVISIONING (Tier 1, Azure — D9/D14)                                              |
|   ├── Apply Terraform configuration in infra/terraform (Container Apps, PostgreSQL Flexible Server 17,                  |
|   │   versioned Blob storage, Key Vault)                                                                                |
|   └── Verify managed PostgreSQL reachability and Blob versioning + retention policy                                     |
+-------------------------------------------------------------------------------------------------------------------------+
| STEP 2: DATABASE MIGRATIONS                                                                                             |
|   ├── Apply every migration in src/Database/Migrations, in order                                                        |
|   └── Verify btree_gist extension, audit_log triggers, outbox sequence + NOTIFY trigger                                 |
+-------------------------------------------------------------------------------------------------------------------------+
| STEP 3: BACKEND API SERVICE DEPLOYMENT                                                                                  |
|   ├── Deploy the .NET 10 JIT container image (D7)                                                                        |
|   └── Validate anonymous liveness probe /health/live and readiness probe /health/ready                   |
+-------------------------------------------------------------------------------------------------------------------------+
| STEP 4: MANAGED INGRESS & FRONTEND                                                                                        |
|   ├── Route Azure Container Apps TLS ingress to the API and /hubs/dashboard WebSocket endpoint                                  |
|   └── Serve the React 19 Vite production build from the API container                                                             |
+-------------------------------------------------------------------------------------------------------------------------+
| STEP 5: BACKUP JOB & RESTORE REHEARSAL                                                                                  |
|   ├── Schedule the nightly pg_dump to versioned Blob storage                                                            |
|   └── Run scripts/backup-restore-rehearsal.sh green at least once before go-live                                        |
+-------------------------------------------------------------------------------------------------------------------------+
| STEP 6: E2E SANITY AUDIT & VERIFICATION SMOKE TEST                                                                      |
|   └── Execute scripts/platform-verify.sh to confirm zero platform defects                                               |
+-------------------------------------------------------------------------------------------------------------------------+
```

### 4.2 Production Environment Boot & Verification Script (`scripts/platform-verify.sh`)

Every check below can fail the run. No check is wrapped in error suppression of any kind — a suppressed check is a spec violation (see §7).

```bash
#!/usr/bin/env bash
# scripts/platform-verify.sh - Full Platform Production Launch & Health Verification Script
set -euo pipefail

echo "======================================================================"
echo "         TRADEBOOK PLATFORM PRODUCTION VERIFICATION PROTOCOL           "
echo "======================================================================"

# 1. Database Connectivity & Extension Check
echo "[1/6] Verifying PostgreSQL 17 & required extensions..."
psql "${DATABASE_URL:?DATABASE_URL missing}" -c "SELECT version();" | grep -q "PostgreSQL 17"
psql "${DATABASE_URL}" -tAc "SELECT extname FROM pg_extension;" | grep -q "btree_gist"
echo " -> PostgreSQL 17 & extensions PASSED."

# 2. Backend API Health Check (the two exact platform probe routes are anonymous)
echo "[2/6] Querying health endpoints..."
LIVE_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/health/live)
READY_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/health/ready)
if [ "$LIVE_STATUS" -ne 200 ] || [ "$READY_STATUS" -ne 200 ]; then
    echo " -> ERROR: health check failed! live=$LIVE_STATUS ready=$READY_STATUS"
    exit 1
fi
echo " -> Health probes PASSED (HTTP 200 OK)."

# 3. Realtime Auth Enforcement & Catch-Up Endpoint
echo "[3/6] Verifying JWT enforcement on the hub and events endpoint..."
NEG_STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST "http://localhost:5000/hubs/dashboard/negotiate?negotiateVersion=1")
if [ "$NEG_STATUS" -ne 401 ]; then
    echo " -> ERROR: /hubs/dashboard negotiate without JWT returned $NEG_STATUS (expected 401)"
    exit 1
fi
EVENTS_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:5000/api/v1/events?afterSequence=0")
if [ "$EVENTS_STATUS" -ne 401 ]; then
    echo " -> ERROR: /api/v1/events without JWT returned $EVENTS_STATUS (expected 401)"
    exit 1
fi
echo " -> Realtime auth enforcement PASSED."

# 4. Outbox Dispatch Pipeline Integration Tests (Task 03 suite: dispatcher, hub, catch-up)
echo "[4/6] Running realtime integration tests..."
dotnet test tests/Tradebook.IntegrationTests/Tradebook.IntegrationTests.csproj --filter "FullyQualifiedName~RealTime"
echo " -> Realtime integration tests PASSED."

# 5. TypeGen Contract Drift Assert (Task 08 pipeline)
echo "[5/6] Verifying C# to TypeScript contract zero-drift..."
dotnet build src/Backend/Tradebook.sln -c Debug
dotnet typegen generate --project-folder .
if [ -n "$(git status --porcelain src/Frontend/src/api/generated)" ]; then
    echo " -> ERROR: contract drift detected in src/Frontend/src/api/generated!"
    exit 1
fi
echo " -> Contract synchronization PASSED."

# 6. Architecture Boundary Tests (owned by Task 08)
echo "[6/6] Executing ArchUnitNET boundary verification..."
dotnet test tests/Tradebook.ArchitectureTests/Tradebook.ArchitectureTests.csproj
echo " -> ArchUnitNET architectural boundaries PASSED."

echo "======================================================================"
echo " SUCCESS: Tradebook Platform Production Verification Complete."
echo "======================================================================"
```

---

### 4.3 ASP.NET Core Health Probe Endpoint Implementation (`HealthEndpoints.cs`)

The binding repository security rule exempts only the two exact health routes and `POST /api/v1/auth/login`. The probes are anonymous so Azure Container Apps can call them; readiness still performs a real PostgreSQL query. Every business and realtime endpoint remains JWT-protected.

```csharp
public static IEndpointRouteBuilder MapTradebookHealthEndpoints(
    this IEndpointRouteBuilder endpoints
)
{
    endpoints
        .MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = static _ => false })
        .AllowAnonymous();

    endpoints
        .MapHealthChecks(
            ReadyPath,
            new HealthCheckOptions
            {
                Predicate = static registration => registration.Tags.Contains("ready"),
            }
        )
        .AllowAnonymous();

    return endpoints;
}
```

Business endpoints use their own named policies (e.g. `Policies("TraderPolicy")`) and read the actor from the JWT `sub` claim only. The health routes are exactly `/health/live` and `/health/ready` — no other health route exists.

---

## 5. Continuous Agent Verification Protocol & Sentinel Master Acceptance Criteria Matrix

### 5.1 Continuous Agent Verification Protocol (Guardrails & Checks)

To guarantee ongoing codebase stability and prevent human or AI agent regression, CI/CD executes the **Continuous Agent Verification Protocol**:

```
+-------------------------------------------------------------------------------------------------------------------------+
|                                          CONTINUOUS AGENT VERIFICATION PROTOCOL                                         |
+-------------------------------------------------------------------------------------------------------------------------+
|  1. Conventional Commit Linting (`commitlint` enforcing scope registry)                                                 |
|  2. Zero-Drift Type Contract Check (`dotnet typegen generate --project-folder .` + git-porcelain assert)                |
|  3. ArchUnitNET Boundary Test Suite (`tests/Tradebook.ArchitectureTests`, owned by Task 08)                             |
|  4. Stryker.NET Mutation Testing Pipeline (asserting >=80% mutation score)                                              |
|  5. Hermetic Integration Tests (`Testcontainers.PostgreSql` & `Respawn`) incl. Task 03's realtime suite                 |
|  6. Playwright E2E Browser Automation Suite (optimistic UI, 409 conflict flow, real SignalR push)                       |
|  7. k6 Baseline-Regression Check (Task 09 model: any 4xx/5xx fails; >20% regression vs committed baseline fails)        |
+-------------------------------------------------------------------------------------------------------------------------+
```

---

### 5.2 Sentinel Master Acceptance Criteria Matrix

The matrix below maps every domain of the Tradebook platform to its functional requirement, target behavior, automated verification command, pass criteria, and audit verification step. No absolute performance numbers appear here (D10) — performance gates are the recorded-baseline regression model owned by Task 09.

| Domain ID | Platform Domain | Functional Requirement | Target Behavior / Baseline | Automated Verification Command | Pass Criteria | Sentinel Audit Verification Step |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **SEC-01** | **Bi-Temporal Audit & Backups** | Append-only `audit_log`; nightly dump provably restorable | Restored per-table row counts match the source exactly | `./scripts/backup-restore-rehearsal.sh` | Script exits 0; any count mismatch fails | Run the §3.1 overlap query — zero overlapping `system_time` ranges |
| **API-02** | **.NET 10 Backend** | FastEndpoints REPR slices in a standard JIT container (D7); JWT on business/realtime endpoints | Container builds and starts; anonymous probes green | `dotnet build src/Backend/Tradebook.sln -c Release` | Build succeeds; `/health/ready` returns 200 | Verify only health and login are anonymous; actor comes from JWT `sub` |
| **MSG-03** | **Real-Time Push** | `OutboxDispatcher` (LISTEN `outbox_new_event`) -> `EntityChanged` fan-out; catch-up via `GET /api/v1/events?afterSequence=N` | At-least-once delivery with client dedup; catch-up pages complete and ordered | `dotnet test tests/Tradebook.IntegrationTests/Tradebook.IntegrationTests.csproj --filter "FullyQualifiedName~RealTime"` | Task 03 tests T1–T7 green | WebSocket trace shows binary MessagePack frames on `/hubs/dashboard` |
| **ANA-04** | **Semantic Layer** | JSON AST -> `SemanticQueryCompiler` -> parameterized SQL (D4) | Identifier whitelist enforced; filter values parameterized (D11) | `dotnet test tests/Tradebook.UnitTests/Tradebook.UnitTests.csproj --filter "FullyQualifiedName~SemanticQueryCompiler"` | Injection attempts rejected; valid ASTs compile | Inspect generated SQL: zero interpolated user strings |
| **UI-05** | **React 19 SPA** | Optimistic mutations with rollback; 409 conflict prompt (D5) | Latency recorded as data — no absolute render gate (D10) | `npx playwright test` (from `tests/e2e`) | Tier 1–3 specs green incl. the concurrent-edit 409 spec | Verify the conflict prompt shows server state; no silent overwrite |
| **VIZ-06** | **Custom Visualizations** | `ChartAdapter` contract + registry; ECharts + Lightweight Charts (D8) | Adapters conform to the mount/update/resize/setTheme/destroy lifecycle | `npm --prefix src/Frontend test` | Adapter contract tests green | Assert LTTB downsampling executes in a Web Worker |
| **INF-07** | **Infrastructure IaC** | Terraform Tier 1 (Azure, D14); root compose = `postgres:17` + api only (D9) | `docker compose up` boots the two-service stack healthy | `terraform -chdir=infra/terraform validate` | 0 validation errors; both containers report healthy | Inspect `docker-compose.yml`: no services beyond postgres and api |
| **AGN-08** | **Agent Governance** | Zero C#-to-TS drift, ArchUnitNET boundaries, Stryker mutation score | Break threshold 80, defined once in root `stryker-config.json` | `dotnet stryker --config-file stryker-config.json` | CI fails if mutation score < 80% | Inspect `AGENTS.md` rules and generated TypeScript DTO interfaces |
| **QA-09** | **E2E & Load Testing** | Playwright automation & k6 baseline-regression harness (Task 09) | Within 20% of the committed `tests/performance/baseline.json` | `k6 run tests/performance/k6/api-delivery-ingestion.js` + `node tests/performance/compare-baseline.mjs api-delivery-ingestion` | Zero 4xx/5xx for valid-input scenarios; no >20% regression | Verify `baseline.json` provenance: reference machine documented |
| **INT-10** | **Master Integration** | End-to-end wiring across DB, API, SignalR, UI | 100% complete integration blueprint | `./scripts/platform-verify.sh` | Script completes with zero errors; every check able to fail | Verify all 10 task specs exist in `docs/tasks/` and cross-links resolve |

---

## 6. Step-by-Step Implementation Guide & Subagent Execution Plan

### 6.1 Step-by-Step Execution Sequence

1. **Step 1: Database & Backup Verification Setup**:
   - Apply every migration in `src/Database/Migrations`, in order.
   - Run the §3.1 audit overlap query and confirm zero rows.
   - Author `scripts/backup-restore-rehearsal.sh` and run it green against a seeded database.

2. **Step 2: Backend API & Health Probes**:
   - Map `/health/live` and `/health/ready` anonymously in `src/Backend/src/Tradebook.Api/Features/Health/HealthEndpoints.cs`; readiness must execute a PostgreSQL health check.
   - Confirm SignalR MessagePack registration and the `/hubs/dashboard` route (owned by Task 03; wired in Task 02's `Program.cs`).

3. **Step 3: Concurrency Conflict Contract**:
   - Confirm the `version`-guard UPDATE pattern in every mutable-entity endpoint and the 409 problem-details response body.
   - Confirm Task 09's Tier 2 concurrent-edit spec covers the browser-visible conflict prompt (no separate client merge module exists to implement).

4. **Step 4: Verification Scripts & Execution Tooling**:
   - Author `scripts/platform-verify.sh` (and a PowerShell counterpart `scripts/platform-verify.ps1` if Windows hosts run verification).
   - Ensure execution permissions (`chmod +x scripts/platform-verify.sh scripts/backup-restore-rehearsal.sh`).

5. **Step 5: Master Verification Run**:
   - Boot the local stack (`docker compose up -d --wait` — `postgres:17` + api).
   - Execute `./scripts/platform-verify.sh` and record results in the handoff report.

---

## 7. Anti-Cheating & Integrity Guardrails

To preserve absolute engineering integrity, all subagents and implementers must strictly adhere to these non-negotiable rules:
- **No Hardcoded Test Returns**: Fake boolean returns, hardcoded `true` assertions, or dummy responses without real underlying logic are strictly prohibited.
- **No Mock Facade Services**: All integration tests must execute against real Docker containers (`Testcontainers.PostgreSql`) or authentic state engines.
- **No Error Suppression in Verification**: no verify script may wrap a check in a suppression idiom (unconditional success operators, ignored exit codes, discarded stderr used to hide failures). Every verification command must be able to fail the run.
- **No Bypassing Stryker Mutation Tests**: Lowering mutation score thresholds below 80% or disabling mutation checks is treated as an integrity violation.
- **No Baseline Tampering**: performance baselines may only be re-recorded on the documented reference machine (Task 09) — never edited to green a failing run.
- **Independent Forensic Audit**: A `teamwork_preview_auditor` agent will independently inspect code, trace logic chains, execute build commands, and verify test assertions. Any detected cheating will result in immediate rejection.

---

*Blueprint lives at `docs/tasks/task-10-platform-integration-master-blueprint.md`.*
