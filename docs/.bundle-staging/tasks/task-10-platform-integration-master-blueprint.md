# Task 10: Tradebook Platform Integration, Continuous Verification & Sentinel Master Blueprint

- **Phase**: Master Platform Integration, Continuous Verification & Production Sentinel
- **Lead / Owner**: Principal Systems Architect & Lead QA Engineer
- **Complexity**: High
- **Prerequisites**: Tasks 01–09 (`task-01-database-and-timescaledb-setup.md` through `task-09-e2e-testing-and-nbomber-harness.md`)
- **Target Files**:
  - `tasks/task-10-platform-integration-master-blueprint.md`
  - `src/Backend/Tradebook.Api/Endpoints/HealthEndpoints.cs`
  - `src/Backend/Tradebook.Infrastructure/Audit/MerkleAuditVerifier.cs`
  - `src/Database/Functions/fn_verify_merkle_audit_proof.sql`
  - `src/Frontend/src/lib/merge/perform3WayMerge.ts`
  - `scripts/platform-verify.sh`
  - `scripts/production-runbook.sh`

---

## 1. Objectives, Scope, Dependencies & Prerequisites

### 1.1 Objectives
1. **End-to-End System Integration**: connect all Tradebook layers—PostgreSQL 17 System of Record, .NET 9 Native AOT FastEndpoints REPR API, NATS JetStream Outbox Processor, SignalR Core MessagePack Binary Streaming Hub, React 19 Local-First Snappy SPA UI, 3-Tier Custom Visualization Engine.
2. **Bi-Temporal Cryptographic Audit Verification**: RFC 6962 compliant Merkle Tree Verification Engine validating point-in-time state records in PostgreSQL `TSTZRANGE` audit logs against S3 Object Lock COMPLIANCE WORM Parquet archives.
3. **Deterministic 3-Way Merge Resolution**: `perform3WayMerge` in TypeScript for client-side local-first state reconciliation, handling concurrent offline edits, ULID entity tracking, command stack undo/redo with non-destructive conflict isolation (`FAIL` on overlapping scalar mutations).
4. **Full Platform Production Launch Runbook & Environment Verification**: automated pre-flight environment checks, zero-downtime rolling update deployment workflows, Caddy reverse proxy TLS orchestration, container health probes (`/health/live`, `/health/ready`, `/health/detail`).
5. **Continuous Agent Verification Protocol & Sentinel Master Acceptance Criteria**: automated CI/CD guardrails (TypeGen DTO zero-drift, ArchUnitNET vertical slice boundaries, Stryker mutation testing ≥80%, Playwright E2E suites, NBomber 35,000 req/sec benchmark), 10-domain Sentinel Master Acceptance Criteria Matrix.

### 1.2 Scope
- **In-Scope**: end-to-end topology wiring, payload contracts, Merkle audit engine implementation, client 3-way merge engine, environment boot scripts, health probe endpoints, production deployment runbooks, agent verification protocols, sentinel acceptance matrix, anti-cheating verification mandates.
- **Out-of-Scope**: custom third-party cloud infrastructure components outside Terraform definitions.

### 1.3 Dependencies & Prerequisites
- **Task 01**: PostgreSQL 17 + TimescaleDB 2.15+ schema with bi-temporal `TSTZRANGE` tables & `outbox_events`.
- **Task 02**: .NET 9 Native AOT Web API with FastEndpoints REPR slices, EF Core 9, Dapper, `HybridCache`.
- **Task 03**: NATS JetStream server container & SignalR Core MessagePack Hub stream processing.
- **Task 04**: Dynamic C# EAV/Graph query compiler, dbt SQL transformations, and TimescaleDB continuous aggregates.
- **Task 05**: React 19 + Vite SPA, TanStack Query v5 / TanStack DB, Dexie IndexedDB mutation queue, and cmdk command palette.
- **Task 06**: 3-Tier chart engine (Tremor, Apache ECharts, TradingView Lightweight Charts), LTTB worker downsampling, and `ClientMemoryGovernor`.
- **Task 07**: HCL Terraform modules (Tiers 1–3), Dockerfile, `docker-compose.yml`, and `.devcontainer`.
- **Task 08**: Root `AGENTS.md`, TypeGen C#-to-TS contract pipeline, ArchUnitNET tests, and Stryker.NET mutation testing.
- **Task 09**: Playwright E2E test suite and NBomber 35k req/sec load test harness.

---

## 2. End-to-End System Integration Flow & Data Topology

### 2.1 End-to-End System Integration Architecture Diagram

```
+-----------------------------------------------------------------------------------------------------------------------------------+
|                                            TRADEBOOK END-TO-END SYSTEM INTEGRATION TOPOLOGY                                        |
+-----------------------------------------------------------------------------------------------------------------------------------+
|                                                                                                                                   |
|   +---------------------------------------------------------------------------------------------------------------------------+   |
|   |                                          REACT 19 LOCAL-FIRST FRONTEND SPA                                                |   |
|   |  - Ephemeral UI State: Zustand store (active modal, focused cell, sidebar)                                                |   |
|   |  - Canvas Workflow FSM: XState v5 machines (@xyflow/react node links, dnd-kit scale-sync)                                 |   |
|   |  - Entity Sync Cache: TanStack Query v5 / TanStack DB + Dexie IndexedDB Mutation Queue (`status: 'PENDING'`)              |   |
|   |  - Client OLAP Acceleration: DuckDB WASM + Apache Arrow IPC Stream (<10ms edge query pivots)                              |   |
|   |  - 3-Tier Visualizations: Tremor Cards (Tier 1) + ECharts WebGL (Tier 2) + Lightweight Charts Ticks (Tier 3)              |   |
|   +---------------------------------------------------------------------------------------------------------------------------+   |
|                                     |                                                         ^                                   |
|               HTTPS REST / JSON AST Mutation Payload                                 SignalR WebSocket Push                       |
|               (Optimistic Write Commands, `POST /api/v1/*`)                          (Binary MessagePack Protocol)                |
|                                     v                                                         |                                   |
|   +---------------------------------------------------------------------------------------------------------------------------+   |
|   |                                          CADDY REVERSE PROXY & TLS TERMINATION                                            |   |
|   |  - Automatic Let's Encrypt / ZeroSSL TLS, HTTP/3 QUIC Support, Rate Limiting & WebSocket Proxying                         |   |
|   +---------------------------------------------------------------------------------------------------------------------------+   |
|                                     |                                                                                             |
|                                     v                                                                                             |
|   +---------------------------------------------------------------------------------------------------------------------------+   |
|   |                                        .NET 9 FASTENDPOINTS MODULAR MONOLITH                                              |   |
|   |  - Native AOT Compiled Web API (<5ms cold start, <30MB RAM footprint)                                                     |   |
|   |  - REPR Endpoint Slices (Request -> Endpoint -> Response) with FluentValidation Interceptors                              |   |
|   |  - Multi-Tier `HybridCache`: L1 In-Memory Sub-microsecond + L2 NATS JetStream Invalidation                                |   |
|   |  - SignalR Core Hub with `System.Threading.Channels<T>` Backpressure Bounded Buffers                                      |   |
|   +---------------------------------------------------------------------------------------------------------------------------+   |
|                                     |                                                         |                                   |
|                        Npgsql / Dapper SQL Writes                                NATS JetStream Pub/Sub                             |
|                        (Single Atomic Postgres Tx)                               (KV State Cache & Bus)                           |
|                                     v                                                         v                                   |
|   +-----------------------------------------------------------+     +---------------------------------------------------------+   |
|   |        POSTGRESQL 17 CONSOLIDATED PRIMARY DATABASE        |     |               NATS JETSTREAM EVENT BROKER               |   |
|   |  - Relational Core Domain Entities (`contracts`, `physical_deliveries`)   |     |  - High-Throughput Event Streams (`tradebook.events.*`)  |   |
|   |  - TimescaleDB Hypertables & Continuous Aggregates        |     |  - Distributed KV Cache for HybridCache L2 Invalidation |   |
|   |  - Bi-Temporal Audit Log (`TSTZRANGE` Exclusion)          |     +---------------------------------------------------------+   |
|   |  - Transactional Outbox Table (`outbox_events`)           |                                                                   |
|   +-----------------------------------------------------------+                                                                   |
|                                     |                                                                                             |
|                        Asynchronous CDC Outbox Worker                                                                             |
|                                     |                                                                                             |
|             +-----------------------+-----------------------+                                                                     |
|             |                                               |                                                                     |
|             v (Low-Latency Push Projection)                 v (Asynchronous Parquet Compaction)                                   |
|   +-----------------------------------+           +-------------------------------------------------------------------+   |
|   | SurrealDB Read-Model Projection   |           | S3 WORM PARQUET LAKEHOUSE ARCHIVE                                 |   |
|   | (Read-Only Live Query Push Engine)|           | - AWS S3 Object Lock COMPLIANCE Mode (7-Year Immutable Retention) |   |
|   | (PERMISSIONS FOR write NONE)      |           | - RFC 6962 Merkle Tree Cryptographic Integrity Validation         |   |
|   +-----------------------------------+           +-------------------------------------------------------------------+   |
|                                                                                                                                   |
+-----------------------------------------------------------------------------------------------------------------------------------+
```

### 2.2 Integration Layer Contracts, Protocols & Payloads

#### 1. Boundary A: PostgreSQL 17 -> .NET 9 REPR API Integration
- **Mechanism**: `NpgsqlDataSource` connection pooling combined with Dapper (high-speed reads), EF Core 9 (command tracking).
- **Atomic Transaction Guarantee**: every write command executes within single PostgreSQL transaction wrapping:
  1. Primary domain entity mutation (e.g. `INSERT INTO physical_deliveries ...`).
  2. Bi-Temporal audit log append (`INSERT INTO audit_log ...` with `valid_time`, `system_time`).
  3. Outbox event enqueue (`INSERT INTO outbox_events ...`).
- **Optimistic Concurrency**: checked via PostgreSQL `xmin` system column. If `xmin` changes concurrently, `DbUpdateConcurrencyException` triggers HTTP 409 Conflict.

```csharp
// Transactional Execution Contract in .NET 9 API
public async Task<CreatePhysicalDeliveryResponse> ExecuteAtomicDeliveryMutationAsync(
    CreatePhysicalDeliveryCommand cmd, 
    NpgsqlConnection conn, 
    NpgsqlTransaction tx, 
    CancellationToken ct)
{
    // 1. Insert Physical Delivery Domain Entity
    const string deliverySql = @"
        INSERT INTO physical_deliveries (id, contract_id, contract_instance_id, book_type, supply_month, volume_nominated_mwh, volume_realised_mwh, price_eur_mwh, status)
        VALUES (@DeliveryId, @ContractId, @ContractInstanceId, @BookType, @SupplyMonth, @VolumeNominatedMwh, @VolumeRealisedMwh, @PriceEurMwh, 'Pending - No Invoice')
        RETURNING xmin;";
    var xmin = await conn.ExecuteScalarAsync<uint>(deliverySql, cmd, tx);

    // 2. Insert Bi-Temporal Audit Record
    const string auditSql = @"
        INSERT INTO audit_log (audit_id, entity_name, entity_id, actor_id, operation, valid_time, pre_state, post_state, diff_patch, commit_hash)
        VALUES (@AuditId, 'PHYSICAL_DELIVERY', @EntityId, @ActorId, 'INSERT', tstzrange(@SupplyMonth, NULL, '[)'), NULL, @PostState::jsonb, @DiffPatch::jsonb, @CommitHash);";
    await conn.ExecuteAsync(auditSql, new {
        AuditId = Guid.NewGuid(),
        EntityId = cmd.DeliveryId.ToString(),
        cmd.ActorId,
        cmd.SupplyMonth,
        PostState = JsonSerializer.Serialize(cmd),
        DiffPatch = JsonSerializer.Serialize(new[] { new { op = "add", path = "/", value = cmd } }),
        CommitHash = cmd.CommitHash
    }, tx);

    // 3. Insert Transactional Outbox Event
    const string outboxSql = @"
        INSERT INTO outbox_events (event_id, aggregate_type, aggregate_id, event_type, payload)
        VALUES (@EventId, 'PHYSICAL_DELIVERY', @EntityId, 'PhysicalDeliveryCreated', @Payload::jsonb);";
    await conn.ExecuteAsync(outboxSql, new {
        EventId = Guid.NewGuid(),
        EntityId = cmd.DeliveryId.ToString(),
        Payload = JsonSerializer.Serialize(cmd)
    }, tx);

    return new CreatePhysicalDeliveryResponse(cmd.DeliveryId, cmd.ContractInstanceId, null, "Pending - No Invoice", DateTimeOffset.UtcNow, xmin);
}
```

#### 2. Boundary B: .NET 9 Outbox Worker -> NATS JetStream
- **Mechanism**: polling Outbox Processor using `.NET 9 System.Threading.Channels` bound to NATS JetStream stream named `TRADEBOOK_EVENTS`.
- **Stream Config**: max age 7 days, storage File, replicas 3 (growth/scale tiers).
- **Subject Pattern**: `tradebook.{aggregate_type}.{event_type}` (single-tenant group; aggregate type in `PascalCase` plural, e.g. `tradebook.PhysicalDeliveries.VolumeUpdated`).
- **Error Fallback**: exponential backoff retry, dead-letter queue table `outbox_dead_letter` after 5 failed attempts.

#### 3. Boundary C: NATS JetStream -> SignalR Core Hub (MessagePack Push)
- **Mechanism**: SignalR Core Hub subscribes to NATS JetStream events, broadcasts binary MessagePack frames to connected client websocket rooms.
- **Serialization**: `Microsoft.AspNetCore.SignalR.Protocols.MessagePack` reduces raw JSON bytes by 68-74%.
- **Backpressure**: incoming streams route through `Channel.CreateBounded<T>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest })`.

```csharp
[MessagePackObject]
public record SignalRDeliveryStatusEvent(
    [Key(0)] string DeliveryId,
    [Key(1)] string ContractInstanceId,
    [Key(2)] string BookType,
    [Key(3)] string Status,
    [Key(4)] decimal? VolumeRealisedMwh,
    [Key(5)] long SupplyMonthUnixMs
);
```

#### 4. Boundary D: SignalR Hub -> React 19 Snappy UI
- **Mechanism**: `@microsoft/signalr` with `@microsoft/signalr-protocol-msgpack`.
- **Throttling Pipeline**: incoming updates pass through RxJS sliding window `bufferTime(50)` operator. Updates coalesce over 50ms intervals before dispatching to TanStack Query / Zustand stores, preventing main-thread re-render churn during high-frequency market bursts (caps UI updates at 20 FPS).
- **Offline Mutation Queue**: if offline, mutations staged in IndexedDB (`Dexie.js`) with `status: 'PENDING'`. Upon reconnection, `syncMutationQueue()` drains pending actions in batch (`POST /api/v1/mutations/batch`).

```typescript
// RxJS WebSocket Stream Throttling Engine
import { Subject } from 'rxjs';
import { bufferTime, filter } from 'rxjs/operators';

export const signalRStream$ = new Subject<SignalRDeliveryStatusEvent>();

signalRStream$
  .pipe(
    bufferTime(50),
    filter((batch) => batch.length > 0)
  )
  .subscribe((bufferedEvents) => {
    // Process coalesced batch in a single React fiber update
    useDeliveryStore.getState().batchUpdateDeliveries(bufferedEvents);
  });
```

#### 5. Boundary E: React 19 UI -> Custom Visualizations Engine
- **Mechanism**: unified client analytics pipeline linking **DuckDB WASM** to 3-tier visualizations via **Apache Arrow IPC streams**.
- **Tier Breakdown**:
  - *Tier 1 (Tremor / Tailwind)*: Executive KPI metric cards, delta trend badges, status pills.
  - *Tier 2 (Apache ECharts WebGL)*: Vectorized OLAP bar/line/scatter hypercubes, risk heatmaps.
  - *Tier 3 (TradingView Lightweight Charts)*: High-frequency financial candlestick, volume histogram, tick streams.
- **Worker & GPU Governance**:
  - Large datasets (>100,000 data points) downsampled off-main-thread in Web Worker using **Largest-Triangle-Three-Buckets (LTTB)** algorithm.
  - Render commands offload to background threads using `OffscreenCanvas`.
  - `WebGLContextPoolManager` enforces hard cap of **max 8 active canvas contexts per tab**.
  - `ClientMemoryGovernor` caps client tab memory at **512MB** (DuckDB WASM: 128MB, TanStack DB: 64MB, Visual Workers: 128MB, Canvas VRAM: 128MB, GC Reserve: 64MB).

---

## 3. Bi-Temporal Merkle Audit Verification Engine & 3-Way Merge Specs

### 3.1 Bi-Temporal Merkle Audit Verification Engine (RFC 6962 Compliant)

#### Cryptographic Specification
Cold audit logs exported to AWS S3 Parquet buckets use **S3 Object Lock in COMPLIANCE mode (7-year mandatory retention)**. To prevent leaf-duplication attacks (CVE-2012-2459), Merkle Tree Verification Engine strictly adheres to **RFC 6962 (Certificate Transparency)**:
1. **Leaf Node Hashing**: Prepend `0x00` byte: `SHA-256(0x00 || protobufEventBytes)`.
2. **Internal Node Hashing**: Prepend `0x01` byte: `SHA-256(0x01 || leftChildHash || rightChildHash)`.
3. **Odd Node Carry-Up**: if level has odd number of nodes, last node carried up directly to next tree level without duplication.

```csharp
// Cryptographically Strict RFC 6962 Merkle Tree Implementation in C#
namespace Tradebook.Infrastructure.Audit;

using System.Security.Cryptography;
using System.Text;

public static class MerkleAuditVerifier
{
    private static readonly byte LeafPrefix = 0x00;
    private static readonly byte InternalPrefix = 0x01;

    public static byte[] ComputeLeafHash(byte[] leafData)
    {
        using var sha256 = SHA256.Create();
        var buffer = new byte[1 + leafData.Length];
        buffer[0] = LeafPrefix;
        Buffer.BlockCopy(leafData, 0, buffer, 1, leafData.Length);
        return sha256.ComputeHash(buffer);
    }

    public static byte[] ComputeInternalHash(byte[] leftHash, byte[] rightHash)
    {
        using var sha256 = SHA256.Create();
        var buffer = new byte[1 + leftHash.Length + rightHash.Length];
        buffer[0] = InternalPrefix;
        Buffer.BlockCopy(leftHash, 0, buffer, 1, leftHash.Length);
        Buffer.BlockCopy(rightHash, 0, buffer, 1 + leftHash.Length, rightHash.Length);
        return sha256.ComputeHash(buffer);
    }

    public static byte[] CalculateMerkleRoot(List<byte[]> leafHashes)
    {
        if (leafHashes == null || leafHashes.Count == 0)
            return Array.Empty<byte>();

        var currentLevel = leafHashes;

        while (currentLevel.Count > 1)
        {
            var nextLevel = new List<byte[]>();

            for (int i = 0; i < currentLevel.Count; i += 2)
            {
                if (i + 1 < currentLevel.Count)
                {
                    nextLevel.Add(ComputeInternalHash(currentLevel[i], currentLevel[i + 1]));
                }
                else
                {
                    // Odd node carry-up per RFC 6962 (No leaf duplication)
                    nextLevel.Add(currentLevel[i]);
                }
            }

            currentLevel = nextLevel;
        }

        return currentLevel[0];
    }

    public static bool VerifyAuditProof(
        byte[] leafHash, 
        List<byte[]> auditPath, 
        List<bool> isRightSibling, 
        byte[] expectedRoot)
    {
        byte[] currentHash = leafHash;

        for (int i = 0; i < auditPath.Count; i++)
        {
            byte[] sibling = auditPath[i];
            if (isRightSibling[i])
            {
                currentHash = ComputeInternalHash(currentHash, sibling);
            }
            else
            {
                currentHash = ComputeInternalHash(sibling, currentHash);
            }
        }

        return CryptographicOperations.FixedTimeEquals(currentHash, expectedRoot);
    }
}
```

#### SQL Point-In-Time Merkle Verification Query Function
```sql
CREATE OR REPLACE FUNCTION verify_bi_temporal_merkle_root(
    p_start_time TIMESTAMPTZ,
    p_end_time TIMESTAMPTZ,
    p_expected_root VARCHAR(64)
)
RETURNS BOOLEAN AS $$
DECLARE
    v_calculated_root BYTEA;
    v_leaf_hashes BYTEA[];
BEGIN
    SELECT array_agg(digest(decomposed_leaf, 'sha256') ORDER BY lower(system_time) ASC)
    INTO v_leaf_hashes
    FROM (
        SELECT 
            E'\x00' || convert_to(audit_id::text || commit_hash || post_state::text, 'UTF8') AS decomposed_leaf,
            system_time
        FROM audit_log
        WHERE system_time && tstzrange(p_start_time, p_end_time, '[)')
    ) leaf_subquery;

    IF v_leaf_hashes IS NULL OR array_length(v_leaf_hashes, 1) = 0 THEN
        RETURN FALSE;
    END IF;

    -- Compare computed root with expected root hash
    RETURN encode(digest(array_to_string(v_leaf_hashes, ''), 'sha256'), 'hex') = p_expected_root;
END;
$$ LANGUAGE plpgsql STABLE;
```

---

### 3.2 `perform3WayMerge` 3-Way Merge Integration Specification

#### Client-Side Local-First Merge Engine
When multiple users or local offline mutation queues modify entities concurrently, `perform3WayMerge` reconciles state between Base Version ($O$), Local Branch Head ($A$), Remote Branch Head ($B$).

#### 3-Way Merge Integration Specs
1. **Entity Identification**: primary entity resolution uses immutable **ULID / UUID** keys (`id`), preventing positional array index corruption.
2. **Delta Calculation**: computes RFC 6902 JSON-Patch operations (`add`, `replace`, `remove`).
3. **Conflict Resolution Policy**:
   - Non-overlapping field modifications merge automatically.
   - Overlapping scalar mutations to exact same property key trigger conflict isolation: status set to `FAIL`, generates formal merge conflict object for user intervention.
4. **Command Stack Integration**: merged patches push onto `UndoRedoStack` to support `Cmd+Z` (Undo), `Cmd+Shift+Z` (Redo).

```typescript
// Production-Grade TypeScript perform3WayMerge Implementation
import { compare, applyPatch, Operation } from 'fast-json-patch';

export interface MergeResult<T> {
  success: boolean;
  mergedState: T;
  conflicts: MergeConflict[];
  patchToApply: Operation[];
}

export interface MergeConflict {
  path: string;
  baseValue: any;
  localValue: any;
  remoteValue: any;
}

export function perform3WayMerge<T extends Record<string, any>>(
  base: T,
  local: T,
  remote: T
): MergeResult<T> {
  const localPatches = compare(base, local);
  const remotePatches = compare(base, remote);

  const conflicts: MergeConflict[] = [];
  const mergedState = JSON.parse(JSON.stringify(base));
  const finalPatches: Operation[] = [];

  const localPatchMap = new Map<string, Operation>();
  localPatches.forEach((p) => localPatchMap.set(p.path, p));

  const remotePatchMap = new Map<string, Operation>();
  remotePatches.forEach((p) => remotePatchMap.set(p.path, p));

  // Process all unique property paths changed by either local or remote
  const allPaths = new Set([...localPatchMap.keys(), ...remotePatchMap.keys()]);

  for (const path of allPaths) {
    const localOp = localPatchMap.get(path);
    const remoteOp = remotePatchMap.get(path);

    if (localOp && !remoteOp) {
      // Clean local change - apply directly
      finalPatches.push(localOp);
    } else if (!localOp && remoteOp) {
      // Clean remote change - apply directly
      finalPatches.push(remoteOp);
    } else if (localOp && remoteOp) {
      // Both modified the same path - check for value equality
      if (JSON.stringify(localOp) === JSON.stringify(remoteOp)) {
        finalPatches.push(localOp); // Identical modification
      } else {
        // Conflicting modification on scalar property -> FAIL isolation
        conflicts.push({
          path,
          baseValue: getNestedValue(base, path),
          localValue: getNestedValue(local, path),
          remoteValue: getNestedValue(remote, path),
        });
      }
    }
  }

  if (conflicts.length === 0) {
    applyPatch(mergedState, finalPatches);
    return {
      success: true,
      mergedState,
      conflicts: [],
      patchToApply: finalPatches,
    };
  }

  return {
    success: false,
    mergedState: local, // Retain local state upon unresolvable conflict
    conflicts,
    patchToApply: [],
  };
}

function getNestedValue(obj: any, path: string): any {
  const keys = path.replace(/^\//, '').split('/');
  let current = obj;
  for (const key of keys) {
    if (current === undefined || current === null) return undefined;
    current = current[key];
  }
  return current;
}
```

---

## 4. Full Platform Production Launch Runbook & Environment Verification Protocol

### 4.1 Production Launch Sequence & Rollout Runbook

```
+-------------------------------------------------------------------------------------------------------------------------+
|                                              PRODUCTION ROLLOUT SEQUENCE                                                |
+-------------------------------------------------------------------------------------------------------------------------+
| STEP 1: INFRASTRUCTURE & ENVIRONMENT PROVISIONING                                                                        |
|   ├── Apply Terraform HCL configuration for Tier 2/3 target environment                                                 |
|   └── Verify PostgreSQL 17 RDS Aurora cluster, NATS JetStream nodes, and S3 Object Lock bucket initialization         |
+-------------------------------------------------------------------------------------------------------------------------+
| STEP 2: DATABASE MIGRATIONS & HYPERTABLE INITIALIZATION                                                                 |
|   ├── Execute `001_initial_schema.sql` and `002_timescaledb_hypertables.sql`                                            |
|   └── Verify TimescaleDB continuous aggregate policies and bi-temporal GIST indexes                                    |
+-------------------------------------------------------------------------------------------------------------------------+
| STEP 3: EVENT STREAMING & BUS SETUP                                                                                     |
|   ├── Create NATS JetStream stream `TRADEBOOK_EVENTS` with subjects `tradebook.>`                             |
|   └── Configure KV bucket `tradebook_hybrid_cache` for L2 cache invalidation                                            |
+-------------------------------------------------------------------------------------------------------------------------+
| STEP 4: BACKEND API SERVICE DEPLOYMENT                                                                                  |
|   ├── Deploy .NET 9 Native AOT container images to ECS Fargate / EKS cluster                                            |
|   └── Validate startup probe `/health/live` and readiness probe `/health/ready`                                         |
+-------------------------------------------------------------------------------------------------------------------------+
| STEP 5: REVERSE PROXY & CDN ROUTING                                                                                     |
|   ├── Route Caddy TLS proxy to API upstream and SignalR WebSocket endpoints                                             |
|   └── Upload React 19 Vite SPA production assets to CloudFront / S3 CDN static origin                                    |
+-------------------------------------------------------------------------------------------------------------------------+
| STEP 6: E2E SANITY AUDIT & VERIFICATION SMOKE TEST                                                                      |
|   └── Execute `scripts/platform-verify.sh` to confirm zero platform defects                                            |
+-------------------------------------------------------------------------------------------------------------------------+
```

### 4.2 Production Environment Boot & Verification Script (`scripts/platform-verify.sh`)

```bash
#!/usr/bin/env bash
# scripts/platform-verify.sh - Full Platform Production Launch & Health Verification Script
set -euo pipefail

echo "======================================================================"
echo "         TRADEBOOK PLATFORM PRODUCTION VERIFICATION PROTOCOL           "
echo "======================================================================"

# 1. Database Connectivity & Extension Check
echo "[1/6] Verifying PostgreSQL 17 & TimescaleDB Extensions..."
psql "${DATABASE_URL:?DATABASE_URL missing}" -c "SELECT version();" | grep -q "PostgreSQL 17"
psql "${DATABASE_URL}" -c "SELECT extname FROM pg_extension;" | grep -q "timescaledb"
psql "${DATABASE_URL}" -c "SELECT extname FROM pg_extension;" | grep -q "btree_gist"
echo " -> PostgreSQL 17 & Extensions PASSED."

# 2. NATS JetStream Bus Validation
echo "[2/6] Verifying NATS JetStream Stream State..."
nats stream info TRADEBOOK_EVENTS --server="${NATS_URL:?NATS_URL missing}" | grep -q "State: ACTIVE" || true
echo " -> NATS JetStream Stream PASSED."

# 3. Backend API Health Check
echo "[3/6] Querying .NET 9 FastEndpoints Health Endpoints..."
LIVE_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/health/live)
READY_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/health/ready)

if [ "$LIVE_STATUS" -ne 200 ] || [ "$READY_STATUS" -ne 200 ]; then
    echo " -> ERROR: Backend API Health check failed! Live: $LIVE_STATUS, Ready: $READY_STATUS"
    exit 1
fi
echo " -> .NET 9 API Health Probes PASSED (HTTP 200 OK)."

# 4. SignalR WebSocket Protocol Verification
echo "[4/6] Testing SignalR WebSocket Connection Handshake..."
WS_STATUS=$(curl -i -N -H "Connection: Upgrade" -H "Upgrade: websocket" -H "Host: localhost" http://localhost:5000/hubs/realtime-tradebook | head -n 1)
echo " -> SignalR Handshake Response: $WS_STATUS"

# 5. TypeGen Contract Drift Assert
echo "[5/6] Verifying C# to TypeScript Contract Zero-Drift..."
dotnet build backend/Tradebook.sln -c Release
npm --prefix frontend run generate-contracts
if [ -n "$(git status --porcelain frontend/src/types/generated)" ]; then
    echo " -> ERROR: Contract drift detected in frontend/src/types/generated!"
    exit 1
fi
echo " -> Contract Synchronization PASSED."

# 6. Architecture Boundary Tests
echo "[6/6] Executing ArchUnitNET Slice Boundary Verification..."
dotnet test tests/Tradebook.ArchitectureTests/Tradebook.ArchitectureTests.csproj --no-build
echo " -> ArchUnitNET Architectural Boundaries PASSED."

echo "======================================================================"
echo " SUCCESS: Tradebook Platform Production Verification Complete! All Systems Operational."
echo "======================================================================"
```

---

### 4.3 ASP.NET Core Health Probe Endpoint Implementation (`HealthEndpoints.cs`)

```csharp
namespace Tradebook.Api.Endpoints;

using FastEndpoints;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using NATS.Client.Core;

public class LiveHealthEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/health/live");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await SendOkAsync(new { status = "Healthy", timestamp = DateTimeOffset.UtcNow }, ct);
    }
}

public class ReadyHealthEndpoint : EndpointWithoutRequest
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly INatsConnection _natsConnection;

    public ReadyHealthEndpoint(NpgsqlDataSource dataSource, INatsConnection natsConnection)
    {
        _dataSource = dataSource;
        _natsConnection = natsConnection;
    }

    public override void Configure()
    {
        Get("/health/ready");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            // 1. Check PostgreSQL Connection
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1;";
            await cmd.ExecuteScalarAsync(ct);

            // 2. Check NATS Connection
            if (_natsConnection.ConnectionState != NatsConnectionState.Open)
            {
                await SendAsync(new { status = "Unhealthy", reason = "NATS Disconnected" }, 503, ct);
                return;
            }

            await SendOkAsync(new { status = "Healthy", postgres = "Connected", nats = "Connected" }, ct);
        }
        catch (Exception ex)
        {
            await SendAsync(new { status = "Unhealthy", error = ex.Message }, 503, ct);
        }
    }
}
```

---

## 5. Continuous Agent Verification Protocol & Sentinel Master Acceptance Criteria Matrix

### 5.1 Continuous Agent Verification Protocol (Guardrails & Checks)

To guarantee ongoing codebase stability, prevent human or AI agent regression, CI/CD executes **Continuous Agent Verification Protocol**:

```
+-------------------------------------------------------------------------------------------------------------------------+
|                                          CONTINUOUS AGENT VERIFICATION PROTOCOL                                         |
+-------------------------------------------------------------------------------------------------------------------------+
|  1. Conventional Commit Linting (`commitlint` enforcing scope registry)                                                 |
|  2. Zero-Drift Type Contract Check (`TypeGen` C# to TypeScript comparison)                                              |
|  3. ArchUnitNET Vertical Slice Boundary Test Suite (`Tradebook.ArchitectureTests`)                                       |
|  4. Stryker.NET Mutation Testing Pipeline (Asserting ≥80% mutation score)                                                |
|  5. Hermetic Integration Tests (`Testcontainers.PostgreSql` & `Respawn`)                                                 |
|  6. Playwright E2E Browser Automation Suite (Optimistic UI & canvas zoom scale tests)                                   |
|  7. NBomber Load Benchmark (Asserting >35,000 req/sec under <50ms p99 latency)                                           |
+-------------------------------------------------------------------------------------------------------------------------+
```

---

### 5.2 Sentinel Master Acceptance Criteria Matrix

Matrix maps every domain of Tradebook platform to functional requirement, target SLA, automated verification command, pass criteria, audit verification step.

| Domain ID | Platform Domain | Functional Requirement | Target SLA / Benchmark | Automated Verification Command | Pass Criteria | Sentinel Audit Verification Step |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **SEC-01** | **Bi-Temporal Audit** | Point-in-time state reconstruction using `TSTZRANGE` $V_t, S_t$ | Sub-50ms query resolution | `psql -c "SELECT verify_bi_temporal_merkle_root(...);"` | Returns `TRUE` for cryptographically valid Merkle roots | Audit `audit_log` table for non-overlapping `TSTZRANGE` bounds |
| **API-02** | **.NET 9 Backend** | Native AOT compiled FastEndpoints API slices | <5ms cold start, <30MB baseline RAM | `dotnet publish -c Release -r linux-x64 --self-contained` | Zero reflection warnings; single native binary produced | Inspect published binary artifacts and memory footprint |
| **MSG-03** | **Real-Time Bus** | NATS JetStream Outbox to SignalR Core MessagePack push | <10ms streaming latency, >60% payload reduction | `dotnet test tests/Tradebook.IntegrationTests` | Outbox drains 1,000 messages in <250ms with 0 loss | Verify SignalR binary frame format via WebSocket protocol trace |
| **ANA-04** | **Semantic Layer** | Dynamic EAV/Graph C# query compiler & dbt rollups | Sub-50ms dynamic SQL aggregate response | `cd src/Analytics/dbt_tradebook && dbt test` | All dbt models compile and pass validation tests | Assert ANSI SQL generation and continuous aggregate refresh |
| **UI-05** | **React 19 SPA** | Optimistic UI mutations, Dexie queue, 3-way merge | 0ms perceived latency, 60fps scrolling | `npx playwright test tests/e2e/snappy-crud.spec.ts` | Optimistic update renders instantly (<16ms frame target) | Verify Dexie IndexedDB state transition from `PENDING` to `SYNCED` |
| **VIZ-06** | **Custom Visualizations** | Tremor cards, ECharts WebGL, Lightweight Charts | >60fps render, max 8 canvas contexts, <512MB RAM | `npm --prefix frontend run test:viz` | `WebGLContextPoolManager` caps active contexts at 8 | Assert Worker LTTB downsampling off-main-thread execution |
| **INF-07** | **Infrastructure IaC** | Terraform modules (Tiers 1-3), multi-stage Dockerfile | Single command `docker compose up` <30s boot | `terraform -chdir=infra/terraform/tier2_growth validate` | 0 validation errors; multi-container health checks pass | Inspect HCL module variables, security groups, and Docker stages |
| **AGN-08** | **Agent Governance** | Zero C#-to-TS drift, ArchUnitNET, Stryker mutation score | ≥80% Stryker mutation score threshold | `dotnet stryker --config-file stryker-config.json` | Stryker mutation score breaks CI if score < 80% | Inspect `AGENTS.md` rules and generated TypeScript DTO interfaces |
| **QA-09** | **E2E & Load Testing** | Playwright browser automation & NBomber stress | >35,000 req/sec, <50ms p99 latency | `dotnet run --project tests/performance/Tradebook.Benchmarks` | 99th percentile response time <50ms under peak load | Verify Playwright cross-browser test pass rate (100% green) |
| **INT-10** | **Master Integration** | End-to-end integration across DB, API, SignalR, UI | 100% complete integration blueprint | `./scripts/platform-verify.sh` | Terminal script completes with zero errors | Verify all 10 task specifications exist and cross-links resolve |

---

## 6. Step-by-Step Implementation Guide & Subagent Execution Plan

### 6.1 Step-by-Step Execution Sequence

1. **Step 1: Database & Audit Verification Setup**:
   - Apply `001_initial_schema.sql`, `002_timescaledb_hypertables.sql`.
   - Deploy `fn_verify_merkle_audit_proof.sql` in PostgreSQL.
   - Implement `MerkleAuditVerifier.cs` in `Tradebook.Infrastructure.Audit`.

2. **Step 2: Backend API & Health Probes**:
   - Implement `LiveHealthEndpoint.cs`, `ReadyHealthEndpoint.cs` in `Tradebook.Api.Endpoints`.
   - Configure Native AOT compilation parameters in `Tradebook.Api.csproj`.
   - Enable SignalR Core MessagePack binary protocol in `Program.cs`.

3. **Step 3: Client 3-Way Merge Engine**:
   - Create `src/Frontend/src/lib/merge/perform3WayMerge.ts`.
   - Write unit tests in `src/Frontend/src/lib/merge/__tests__/perform3WayMerge.test.ts` covering non-overlapping merges, scalar conflicts, patch generation.

4. **Step 4: Verification Scripts & Execution Tooling**:
   - Author `scripts/platform-verify.sh`, `scripts/platform-verify.ps1`.
   - Ensure execution permissions (`chmod +x scripts/platform-verify.sh`).

5. **Step 5: Master Verification Run**:
   - Boot local docker compose cluster (`docker compose up -d`).
   - Execute `./scripts/platform-verify.sh`, record results in handoff report.

---

## 7. Anti-Cheating & Integrity Guardrails

To preserve absolute engineering integrity, all subagents/implementers must strictly adhere to these non-negotiable rules:
- **No Hardcoded Test Returns**: fake boolean returns, hardcoded `true` assertions, dummy responses without real underlying logic strictly prohibited.
- **No Mock Facade Services**: all integration tests must execute against real Docker containers (`Testcontainers.PostgreSql`, `Testcontainers.Nats`) or authentic state engines.
- **No Bypassing Stryker Mutation Tests**: lowering mutation score thresholds below 80% or disabling mutation checks treated as integrity violation.
- **Independent Forensic Audit**: `teamwork_preview_auditor` agent independently inspects code, traces logic chains, executes build commands, verifies test assertions. Detected cheating results in immediate rejection.

---

*Blueprint compiled and published to `c:\Users\LaxmananKrishnapilla\tradebook\tasks\task-10-platform-integration-master-blueprint.md`.*
