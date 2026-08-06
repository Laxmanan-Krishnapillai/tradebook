# Adversarial Review of the Master Implementation Task Breakdown (Tasks 01–10)

**Reviewer**: Independent adversarial audit (10 parallel critique agents, one per task spec)
**Date**: August 5, 2026
**Scope**: `tasks/README.md` (master index) and all 10 task specification files in `tasks/`
**Verdict**: **REJECT ALL 10 TASK SPECS AS WRITTEN — the roadmap is not executable or honestly verifiable in its current form.**

---

## 1. Executive Summary

Each of the 10 task specs was reviewed independently by a dedicated adversarial agent grounded against `tasks/README.md` and `architecture/master-architecture-blueprint.md`. Every single task was rated **Unsound**. The failures are not cosmetic — they fall into four systemic categories:

1. **Cross-document contract contradictions.** No two documents agree on the domain model (`trades`/`tenants` vs BioGem `contracts`/`market_prices`), the hypertable name (`trade_ticks` vs `market_ticks`), the optimistic-concurrency column (`xmin` — which is not even a legal Postgres column name — vs `version`), the test-project paths (four different identities), or the load targets (three different TPS numbers).
2. **Specified-but-never-implemented deliverables.** Outbox enqueue triggers, the channel→hub→SignalR fan-out pipeline, the `/api/v1/mutations/batch` endpoint, ArchUnitNET, the S3/Parquet exporter, custom-plugin registries, WebGL context governance — all claimed as in-scope, none actually specified with code or tests.
3. **Unachievable or unverifiable acceptance criteria.** >35,000 req/sec under <50ms p99, <5ms AOT cold start, <30MB RAM, "exactly-once" event delivery, >60fps WebGL, <16ms DOM renders, Stryker ≥80% via a config whose paths don't exist. Many are mathematically infeasible given the spec's own design (a 100ms poller cannot deliver sub-10ms end-to-end); most are unverifiable with the commands provided.
4. **Self-defeating anti-cheating guardrails.** Several specs mandate "no dummy stubs / no hardcoded SQL" while shipping code that is a dummy stub (a `Set` bookkeeping class marketed as a WebGL context pool), or guardrails that *forbid* the very names the rest of the system uses.

**Systemic root cause**: the specs were authored in isolation and then linked, without a single enforced source of truth for schemas, DTOs, paths, or verification commands. The "Single Authoritative Master Architecture Blueprint" is contradicted by `entity-model.md`, by the tasks themselves, and even by its own DDL.

---

## 2. Verdict Summary

| Task | Verdict | Kernel of failure |
|---|---|---|
| 01 — Database & TimescaleDB | **Unsound** | Spec contradicts itself on valid-time semantics; outbox trigger claimed but absent; integration test cannot pass; schema conflicts with blueprint (`trade_ticks`/`market_ticks`, `xmin`/`version`) |
| 02 — .NET Backend Core | **Unsound** | Audit writes violate the schema's own exclusion constraint (2nd mutation fails); `xmin::uint4` is invalid SQL; all endpoints `AllowAnonymous()`; Dapper + reflection serialization are incompatible with the mandated Native AOT |
| 03 — SignalR + NATS | **Unsound** | Client-delivery pipeline never specified; "exactly-once" impossible (at-least-once, no dedup); `SubjectMap` lacks the one event the system actually emits → silent data loss; 3-replica streams on single-node dev |
| 04 — Semantic Layer / dbt | **Unsound** | RLS predicate emits unqualified `tenant_id` → every multi-table query errors; Arrow schema/array type mismatch throws at runtime; ORDER BY is a raw SQL-injection vector; dbt reads a schema no task creates |
| 05 — React 19 CRUD UI | **Unsound** | **Duplicate task files** (README links a 31-line stub); `/api/v1/mutations/batch` owned by nobody; mutation lifecycle contradicts Tasks 09/10; optimistic pipeline never wired to UI; no `online` listener |
| 06 — Visualizations | **Unsound** | Query AST/endpoint incompatible with Task 04; "plug-and-play plugins" never specified; WebGL pool governs nothing (no WebGL context ever acquired); memory governor is a facade; >60fps criterion dropped |
| 07 — Infrastructure | **Unsound** | TimescaleDB cannot run on Aurora or `postgres:17-alpine`; compose ships **no NATS service** (Tasks 03/08/README all boot one); secrets/IAM/HTTPS/WAF mandated but unimplemented; Tier 1 & 3 have no modules |
| 08 — Agent-Readiness | **Unsound** | Stryker config paths resolve to nonexistent files; ArchUnitNET assigned to it but absent; re-authors DTOs instead of consuming Task 02's; automated anti-cheat tests pass without running the real gates |
| 09 — E2E & NBomber | **Unsound** | Load-tests `/api/v1/orders` (does not exist); benchmark never asserts (4xx/5xx count as success); 35k rps lacks load math; MSW 2.0 cannot mock WebSockets; NBomber v5 is commercial-licensed |
| 10 — Platform Integration | **Unsound** | Merkle proofs incompatible between C# (binary tree) and SQL (flat concat) — mathematically cannot match; `platform-verify.sh` has broken paths and `|| true` that swallows failures; S3/Parquet verify target unimplementable |

---

## 3. Consolidated Findings by Task

### Task 01 — Core Database & TimescaleDB Bi-Temporal Audit
- **CRITICAL**: §4.1 prose says keep the original `valid_time` on update; §4.2 code recomputes it from `executed_at` (the column a retroactive edit mutates). Time-travel queries return NULL for periods where a state existed — the headline "Full Revertability" feature corrupts the timeline.
- **CRITICAL**: "Automated outbox enqueue triggers" and acceptance TS-06 are specified, but no trigger code exists anywhere in the spec. Task 03 only reads `outbox_events`; nobody writes it atomically.
- **CRITICAL**: The provided integration test only runs migrations 001–005, but the trigger lives in `src/Database/Functions/` — zero audit rows are created, so the test cannot pass. It also queries current time, never testing time-travel.
- **HIGH**: `DELETE FROM tenants` cascades to `portfolio_accounts`, which is still referenced by `trades` (RESTRICT) → tenant deletion is structurally impossible.
- **HIGH**: No RLS, no policies; `SECURITY DEFINER` functions without `SET search_path` (privilege-escalation vector); `audit_log` is freely writable — the "immutable audit" claim has no enforcement.
- **HIGH**: `parent_commit_hash` is never assigned → no hash chain, no tamper evidence. Commit-chain integrity is fake.
- **HIGH**: `23P01` exclusion test is untestable through the designed write path (trigger always closes the prior row first).
- **MEDIUM**: `diff_patch` generation is not valid RFC 6902 (no `remove` ops, `value:NULL` omitted); no-op updates still audit.
- **MEDIUM**: README verification references `001_initial_schema.sql` which this spec never produces; migrations exist in `Migrations/` but `Functions/` and `Triggers/` are silently skipped by the C# runner.

### Task 02 — .NET 9 Backend Core
- **CRITICAL**: Both `CreateAtomicAsync` and `UpdateAtomicAsync` insert audit rows with `[t, ∞)` system_time **and** valid_time for the same entity — overlapping ranges violate the Task-01 `EXCLUDE USING gist` constraint. The audit log can hold exactly one row per entity; the second mutation fails.
- **CRITICAL**: `xmin::text::uint4` is invalid SQL (no `uint4` type; `xmin` is `xid`), and the blueprint DDL declares a user column named `xmin`, which Postgres forbids. The prerequisite schema is un-buildable and the OCC mechanism cannot execute.
- **CRITICAL**: Every endpoint is `AllowAnonymous()`; `TenantId`/`ActorId` come from the request body — any caller can forge any tenant/actor. Direct contradiction of the blueprint's JWT claims model.
- **HIGH**: Native AOT stack cannot publish: `FastEndpoints.Swagger` referenced but not packaged; Dapper uses `Reflection.Emit` (throws under AOT); HybridCache default serializer is reflection-based; no manual endpoint registration. `<5ms` cold start and `<30MB` RAM are unsubstantiated (README uses `<50MB`).
- **HIGH**: "Bi-temporal soft deletion" is implemented as a hard `DELETE` with `'{}'` diff and no outbox event — realtime subscribers never learn of deletions; no revertability.
- **HIGH**: `DELETE /api/v1/trades/{tradeId}` has no request member bound to the route token → FastEndpoints fails at startup; `PUT` never validates route vs body.
- **HIGH**: Audit `diff_patch` hardcoded `'{}'`, `parent_commit_hash` never set, `pre_state` never populated — "what changed" is not answerable.
- **MEDIUM**: Test-project path is `src/Backend/Tradebook.Tests` in one place, `tests/Tradebook.Tests` in the command, `tests/Tradebook.UnitTests` in the README. TypeGen `OutputDir` resolves to a nonexistent path.

### Task 03 — SignalR Real-Time & NATS JetStream
- **CRITICAL**: The entire delivery pipeline (NATS consumer → channel → hub → `Clients.Group`) is **never specified**. The hub ships only subscribe/unsubscribe; the channel carries raw JSON bytes; typed client methods receive typed DTOs — no conversion code exists. The task's core tests target a path that doesn't exist.
- **CRITICAL**: "Without duplicate delivery" is an exactly-once claim the design cannot honor. The worker publishes to NATS inside an open transaction; a crash between publish and commit republishes. No dedup (`Nats-Msg-Id`), no consumer idempotency.
- **CRITICAL**: `SubjectMap` has no `Trade:TradeCreated` entry — the one event Task 02 actually emits. Unmapped events are logged and skipped, but the batch still commits → **permanent silent data loss**.
- **HIGH**: Sub-10ms end-to-end is mathematically infeasible with a 100ms batch poller.
- **HIGH**: Authorization is specified in prose (§4.2/§7.1) and absent in code — `SubscribeContract` performs no access check; cross-company leakage.
- **HIGH**: No reconnect/catch-up for loss-intolerant events; a client offline >30s permanently misses events.
- **HIGH**: Global channel capacity 5000 (not "per company"); `FullMode.Wait` with no timeout blocks the DB worker holding row locks.
- **HIGH**: Streams with `Replicas: 3` cannot exist on the single-node dev compose; no stream-bootstrap step exists at all, so the worker fails from first boot. `DeliverPolicy: All` is a consumer option placed under stream config.
- **MEDIUM**: IT-03-05 requires NATS headers the code never sets; Testcontainers referenced but not a dependency; two different integration-test csproj paths.

### Task 04 — Dynamic Semantic Layer & dbt
- **CRITICAL**: RLS predicate emits bare `tenant_id = @p0`; joined tables both have `tenant_id` → `column reference "tenant_id" is ambiguous` on every multi-table query. `candle_1m` has no `tenant_id` at all.
- **CRITICAL**: Arrow serializer declares typed schema fields but builds every column as `StringArray` → `ArrowStreamWriter` throws. "Zero-copy" is false (stringified cells); `NUMERIC(28,10)` silently cast to float64.
- **CRITICAL**: `ORDER BY` interpolates client strings raw → SQL injection the spec claims to prevent. Its own injection test only covers filter values.
- **HIGH**: TS AST allows `contains`/`notIn`; C# compiler throws on them. Empty `IN ()` and null-bind scalar operators silently return wrong results.
- **HIGH**: dbt reads `raw_ingestion.trades` — a schema no task creates; no `profiles.yml`; no model touches the continuous aggregates the objective claims to integrate. Two analytics stacks that never connect.
- **HIGH**: Join resolution is single-hop star only; the `candle` entity is unqueryable.
- **HIGH**: Guardrails #1/#2 (no hardcoded SQL) are defeated by design — pre-cooked SQL just moves into `metrics[].expression`, concatenated unparsed.
- **HIGH**: YamlDotNet/Arrow under the Native AOT no-reflection mandate → won't publish without trimming annotations never specified.
- **MEDIUM**: The `<10ms`/`<30ms` criteria have no methodology; the benchmark project belongs to Task 09 and doesn't exist yet.

### Task 05 — React 19 CRUD UI (both files reviewed)
- **HIGH**: **Duplicate files.** README links `task-05-react-snappy-crud-ui.md`, a 31-line stub that defers to `task-05-react19-snappy-crud-ui.md` (the real 920-line spec), which is never linked. Two artifacts claim Task 05; no precedence rule.
- **CRITICAL**: `/api/v1/mutations/batch` — the frontend's only sync path, Task 09's load-tested path, and the blueprint's atomic transaction — is claimed as a Task 02 deliverable that Task 02 never defines. Nobody owns it, its request/response schema, or its status codes.
- **CRITICAL**: Mutation lifecycle contradicts downstream verifiers: Task 05 deletes synced rows; blueprint, Task 09, and Task 10 assert `PENDING → SYNCED`. Three downstream gates cannot pass against the specified code.
- **CRITICAL**: Optimistic pipeline never wired: no component calls `enqueue()`, `perform3WayMerge` has zero callers, undo can't compensate pending mutations, no local-vs-server precedence rule.
- **CRITICAL**: "Automatically flush upon network restoration" is not implemented — no `online` listener, no retry/backoff, no poison-mutation dead-letter.
- **HIGH**: `compactMutations` keys on `entityType:entityId` (no tenant); INSERT+UPDATE merge keeps stale `baseVersion`; client fabricates server-generated IDs; `baseVersion` never maps to server OCC.
- **HIGH**: `fetch` sends no Authorization header; no 401/403/409/429 handling; no batch size cap; client includes spoofable `tenantId` in DTO.
- **HIGH**: SignalR envelope mismatch — Task 03 hub exposes strongly-typed methods, Task 05 consumes a generic `EntityStreamEvent` with `sequenceNumber` that no producer emits; three different event shapes across three specs.
- **MEDIUM**: `TanStack DB` claimed in title but not a dependency; TypeGen v4 (Task 02) vs v5.3 (Task 08); `kbar@0.1.0-beta.45` stale; virtualized table has no code contract.

### Task 06 — Custom Visualizations
- **CRITICAL**: Binds to `/api/v1/semantic/query`; Task 04 implements `/api/v1/analytics/query`. AST operator/granularity vocabularies are incompatible (`not_equals` vs `notEquals`, invented `between`). The task's core purpose cannot function.
- **CRITICAL**: "Plug-and-Play Custom Visualizations" (the task title) is never specified: no plugin interface, registry, loader, sandbox, CSP, versioning, or validation. `pluginRef` declared but never resolved — the headline deliverable is vapor.
- **HIGH**: The README-mandated >60fps acceptance criterion is absent from the acceptance table. Live-tick path is unbuildable: Task 03 is not even a prerequisite and the chart wrapper accepts only static data.
- **HIGH**: `WebGLContextPoolManager` governs nothing: ECharts is initialized with `renderer: 'canvas'`, no code ever calls `getContext('webgl')`; `webglcontextlost` can never fire; no LRU/texture tracking.
- **HIGH**: Memory governor is a facade: no `registerDuckDbAllocation`/`registerTanStackDbAllocation` methods exist (counters permanently 0); `triggerCompaction` dispatches an event nobody subscribes to; V8 heap monitor can't see WASM/workers/VRAM. Violates its own guardrail #2.
- **HIGH**: `transferControlToOffscreen()` throws on React StrictMode double-invoke; unstable effect deps recreate charts every render; no main-thread fallback; no ResizeObserver; deferred widgets never retry.
- **HIGH**: Zero security content for persisted widget definitions (injection surface via `chartType`/`pluginRef`, unsanitized titles).
- **MEDIUM**: LTTB acceptance criterion (retain absolute min/max) contradicts genuine LTTB (only endpoints guaranteed) — the test and the anti-cheat rule are in direct conflict.

### Task 07 — Infrastructure
- **CRITICAL**: TimescaleDB does not run on AWS Aurora (library rejected in `shared_preload_libraries`) and is not bundled in `postgres:17-alpine` → the compose init fails and the DB never becomes healthy; subagent forced to cheat (omit timescaledb) to pass.
- **CRITICAL**: Spec's DB narrative uses the BioGem schema (`contracts`, `market_prices`) while its own prerequisites (Tasks 01/02) build `trades`/`tenants`. Two files claim authority over contradictory schemas.
- **CRITICAL**: Compose ships no `nats` service, but README, Task 03, and Task 08 all boot `nats` from this file, and Task 07 is the sole owner of `docker-compose.yml`. The platform's core broker has no local runtime.
- **HIGH**: Secrets manager mandated but unimplemented: `master_password = var.master_password` (plaintext in state), hardcoded `dev_password_123`, no `secrets` block, no rotation.
- **HIGH**: ECS task role has zero policy attachments; execution role uses broad AWS-managed policy; egress `0.0.0.0/0`; no HTTPS listener, no ACM, no WAF despite being costed ($120).
- **HIGH**: Tier 1 (App Runner/Supabase/Neon) and Tier 3 (EKS/Karpenter) have no modules; CloudWatch log group, KMS, and auto-scaling are referenced but absent from skeletons → ECS task won't start.
- **MEDIUM**: Cost model is not itemized; 10,000 TPS peak vs 100 TPS avg vs README >35k req/sec unreconciled; 3 NAT gateways ~$97/mo not in the $210 line.
- **MEDIUM**: "Fluent Migrator at startup" with Flyway `V{NNN}__` naming; DB role `tradebook_app` vs README's `tradebook`.

### Task 08 — Agent-Readiness & Governance
- **CRITICAL**: Root-level `stryker-config.json` paths resolve to nonexistent files (`Tradebook.Api.csproj`, `../../../tests/...` escaping above the repo). The ≥80% mutation gate is unexecutable.
- **CRITICAL**: The threshold is three different numbers (80% / per-module 85/80/75/65 / AGENTS.md 80%) with no enforcement mechanism.
- **CRITICAL**: ArchUnitNET is assigned to Task 08 by the README **four times**, but the spec contains zero occurrences and no deliverable. Task 02 ships its own ArchUnit test under a different path.
- **CRITICAL**: The hermetic DB fixture creates a 4-column `trades` table incompatible with the code it tests (which inserts `audit_log`, `outbox_events`, `custom_fields`, `xmin`...). Every integration test fails at runtime.
- **HIGH**: "Single source of truth" is self-contradictory (C# is the source per §6.2; generated TS is the source per README) and three incompatible DTO shapes already exist across Tasks 02/05/08.
- **HIGH**: The spec's own generated-contract example ships the drift it exists to prevent (`side: TradeSide.Buy` serializes to `"Buy"`; Task 02's validator accepts only `"BUY"`).
- **HIGH**: Anti-cheat tests can pass without the real gates running: `File.Exists` passes on stale output (`clearOutputDirectory: false`); Stryker test only parses the config file.
- **MEDIUM**: Context maps assert facts about other tasks' deliverables that are false (compose boots NATS, EF Core 9 used — neither true).

### Task 09 — E2E & NBomber
- **CRITICAL**: Benchmark posts to `/api/v1/orders` — Task 02 defines `/api/v1/trades` only — and omits required validator fields (`AssetClass`, `Currency`, `ExecutedAt`). Every request 404s/400s while the acceptance table demands "0.00% failed".
- **CRITICAL**: The harness never asserts: `Http.Send` doesn't fail on 4xx/5xx, and no `WithAssertions`. The >35k rps / <50ms p99 rows are passable even while the server returns errors — contradicted by the spec's own guardrail.
- **CRITICAL**: 35k req/sec at 500 copies requires 14.3ms mean latency per multi-statement Postgres write transaction on a CI runner — no load math or hardware spec; also 30s vs "30 minutes" internally contradictory.
- **CRITICAL**: MSW pinned to v2.0 cannot intercept WebSockets (added in v2.6.0); no `signalr.ts` blueprint, no negotiate-handshake or MessagePack frame mocking.
- **CRITICAL**: Tier 3 "full pipeline" is logically impossible under MSW isolation (if MSW intercepts REST+WS, nothing reaches FastEndpoints/NATS/Postgres; if the DB is real, it's not hermetic).
- **HIGH**: Offline-replay test is defeated by MSW (app never experiences network failure); <16.6ms criterion asserts 50ms `toBeVisible`; frame timing measured Node-side, not in-page.
- **HIGH**: 100k-row virtualization test has no data source (default `limit = 50`, no fixture generator).
- **HIGH**: CI workflow is a one-line description; no YAML, no service provisioning, no seed step for the hardcoded tenant FK.
- **HIGH**: NBomber v5+ is closed-source/commercial; `BiTemporalQueryScenario` is required but never built or registered; SignalR scenario has no JWT path.

### Task 10 — Platform Integration
- **CRITICAL**: Merkle proof is algorithmically incompatible: C# computes a binary tree (0x00/0x01 domain-separated), SQL computes `SHA256(hex(leaf0)||hex(leaf1)...)` flat concatenation. SEC-01 ("C# root matches SQL root") is mathematically impossible.
- **CRITICAL**: `platform-verify.sh` has broken paths (`backend/Tradebook.sln` vs `src/Backend/`), a nonexistent `generate-contracts` script, wrong SignalR hub path, and `grep -q ... || true` wrappers that make checks never fail.
- **CRITICAL**: `/api/v1/mutations/batch` has no owning task; SEC-04 S3/Parquet verification target is unimplementable (no exporter exists in Tasks 01–09); topology resurrects SurrealDB the blueprint explicitly eliminated.
- **HIGH**: INT-10 "all cross-links resolve" is false: six filename mismatches exist, including this task's own filename.
- **HIGH**: Three incompatible `perform3WayMerge` contracts (Task 10, Task 05, blueprint) with non-deterministic patch ordering and missing RFC 6902 escape decoding.
- **HIGH**: Runbook cites migrations (`001_initial_schema.sql`) that don't exist; health-check drift (`/healthz` vs `/health/live`+`/health/ready`); double audit write (trigger + repository) with `entity_name` casing mismatch.
- **MEDIUM**: Guardrails bypassable via `|| true`; no tamper/negative tests; `/health/detail` undefined; TypeGen v4 vs v5.3.

---

## 4. Cross-Cutting Systemic Issues (found independently by 5+ agents)

1. **Domain-name war.** `trades`/`tenants`/`portfolio_accounts` (blueprint, Tasks 01/02/04) vs BioGem `contracts`/`market_prices`/`goo_certificate_transactions` (entity-model.md, Tasks 03/07). Both claim authority. Task 03's guardrail 5 *forbids* the names the rest of the system uses.
2. **Orphaned contracts.** `/api/v1/mutations/batch` (Tasks 02/05/09/10 all assume it; nobody owns it), outbox enqueue (Task 01 claims, Task 03 assumes, Task 02 undefined), NATS stream bootstrap (nobody creates streams), BioGem DDL (nobody owns it), Dockerfile (README assigns to Task 07; absent from its target files).
3. **Path/identity fragmentation.** 3–4 different test-project paths; README verification commands reference files that cannot exist per the task specs (`001_initial_schema.sql`); 6 filename mismatches between README and disk; two Task 05 files.
4. **Performance targets divorced from reality.** 35k rps / 14.3ms-transaction writes, <5ms AOT cold start, <30MB RAM, exactly-once delivery, >60fps WebGL, sub-10ms end-to-end behind a 100ms poller — none backed by measurement, hardware, or load model; several mathematically impossible given the spec's own design.
5. **Anti-cheating that rewards cheating.** Stryker config pointing at nothing; benchmark with no assertions; `|| true` verification; guardrails forbidding the real entity names; tests passing on stale/mocked output; a "WebGL context pool" that never acquires a context.
6. **Version/tool drift.** TypeGen 4.0.0 vs 5.3.0; NBomber 5.5.0 (commercial) vs `NBomber.Http 5.0.1`; MSW 2.0 (no WS) vs required v2.6+; Dapper under AOT; unpinned Docker images.

---

## 5. Implications for the Bootstrap Pivot

The decision to scale scope down to a **single entity (biotickets)** with a rapid-deployment, agent-ready, human-on-the-loop tasklist directly addresses the root causes. The new roadmap must carry forward these hard rules:

1. **One source of truth per contract.** Exactly one canonical schema, one DTO set (TypeGen, single pinned version), one test-project layout, one set of verification commands. No duplicate files; every README link must resolve.
2. **Specify-or-delete.** Every deliverable in scope must have code, a target file, and an automated test. No "claimed but unspecified" artifacts (outbox trigger, batch endpoint, hub fan-out, plugin registry).
3. **Honest SLAs.** Only measurable criteria with a defined measurement protocol and reference environment. Default to "must be green + recorded" over invented throughput numbers.
4. **Security from day one.** JWT auth with tenant/actor from claims (no `AllowAnonymous`), RLS, no `SECURITY DEFINER` without `search_path`, no hardcoded credentials, HTTPS + secrets manager.
5. **Realtime with at-least-once + dedup**, explicit stream bootstrap, and per-group authorization — not exactly-once claims.
6. **Executable verification.** Assertions that fail the build when wrong; no `|| true`; every gate runs the real tool (Stryker, terraform plan, Playwright) against the real artifact.
7. **Human-on-the-loop.** Deploys gated by human approval; PR review as the primary safety layer; clear Definition of Done per task; single-command verify for the whole stack.

---

## 6. Recommendation

Do not execute any of Tasks 01–10. Use the findings above as the acceptance bar for the new bootstrap tasklist: every task in the rewrite must fix at least one systemic issue above and be rated **Sound** under the same adversarial method before a subagent is dispatched.
