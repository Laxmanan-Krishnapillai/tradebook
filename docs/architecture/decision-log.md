# Tradebook Architecture Decision Log

**Status**: Authoritative. Where this log conflicts with older statements in `architecture/master-architecture-blueprint.md`, `tasks/*.md`, `review/*.md`, or `research/*.md`, **this log wins**.
**Date**: 2026-08-06 (post-adversarial-review de-scope)

---

## D0 — Process context

- **Aug 5**: `review/adversarial-tasklist-review.md` rejected all 10 task specs and recommended a single-entity bootstrap pivot (§5).
- **Aug 6 (morning)**: a remediation pass fixed parts of tasks 01/02/04/05/08/09/10, the blueprint, and the entity model; tasks 03/06/07 were left untouched.
- **Aug 6 (this log)**: a second adversarial review confirmed which defects survived and produced the de-scope below. Each affected task file now carries a **DESCOPE NOTICE** at the top; specs must be revised per those notices before any implementation agent is dispatched.

## D1 — Continue the simplified 10-task plan

Supersedes the Aug 5 bootstrap-pivot recommendation (`review/adversarial-tasklist-review.md` §5). The 10-task roadmap continues, but on the reduced stack defined here. The Aug 5 review's findings remain the acceptance bar for revised specs.

## D2 — Cut NATS JetStream

Transactional **outbox stays**. The dispatcher becomes an in-process `BackgroundService` using `System.Threading.Channels`, woken by Postgres `LISTEN/NOTIFY` (fallback poll: 1s). Correctness rules:
- Claim outbox rows with `SELECT ... FOR UPDATE SKIP LOCKED` **inside an open transaction** (outside one, the lock is a no-op).
- Dispatch to the SignalR fan-out, then mark `processed_at` and commit in the same transaction. Delivery is at-least-once; consumers are idempotent by event id.

**Re-entry trigger**: a second consumer *process* (e.g. Salesforce sync worker) or multi-node API scale-out (which also forces a SignalR backplane decision). The outbox schema does not change when a broker is added — only the dispatcher does.

## D3 — Cut TimescaleDB

All tables are plain PostgreSQL 17. `market_prices` is ~1 row/day (wide EOD row); thirty years is ~11k rows — no hypertable, no continuous aggregates. TimescaleDB's real cost was operational: `shared_preload_libraries` hosting constraint (no Aurora/RDS; Azure ships only the Apache edition, which lacks continuous aggregates and compression), image/upgrade coupling, and backup/restore ceremony.

**First scaling step if tick data ever arrives**: native declarative partitioning (in-core, works on every managed provider). **Re-entry trigger for TimescaleDB**: sustained ingestion beyond ~1M rows/day or a hard requirement for managed compression/retention policies.

This cut unlocks managed Postgres (Aurora, Azure Flexible Server, Neon) as valid hosting options.

## D4 — Cut DuckDB WASM + Arrow IPC

Single analytics query path: JSON AST → C# `SemanticQueryCompiler` → parameterized SQL → JSON result set. Server round-trip on LAN (~30–80ms) is imperceptible for human-driven pivoting at this data size.

**Re-entry trigger**: interactive pivot workloads over >10M-row result sets.

## D5 — Cut offline-first (Dexie queue, compaction, `/api/v1/mutations/batch`, `perform3WayMerge`)

**Offline editing is explicitly out of scope.** No requirement for it was ever stated, and the specced conflict machinery was incoherent (merge engine had zero call sites; client-wins over audit data).

Replacement, which fully preserves the "Linear-grade snappy" goal:
- Optimistic updates via TanStack Query per-mutation, with rollback on error.
- Optimistic concurrency via a **`version BIGINT` column** on mutable entities: `UPDATE ... SET ..., version = version + 1 WHERE id = $1 AND version = $2`; zero rows → HTTP 409 → client refetches and shows a conflict prompt. **No silent client-wins, ever.**
- Undo/redo: in-memory command stack, session-scoped.

## D6 — Cut S3 Object Lock COMPLIANCE + RFC 6962 Merkle engine

Audit story: append-only `audit_log` (trigger-maintained, bi-temporal) + nightly `pg_dump` to a **versioned S3 bucket**. GOVERNANCE-mode object lock is a later config change if desired.

**Re-entry trigger**: a written compliance requirement that names WORM retention — ask compliance and get the answer in writing. (Note: the previous spec's Merkle verification could never pass — the C# binary-tree and SQL flat-concat roots were mathematically incompatible.)

## D7 — Cut Native AOT

Standard JIT container (`PublishReadyToRun` optional). Cold start is irrelevant for a long-running container; JIT with dynamic PGO matches or beats AOT steady-state. This removes the SignalR-server/Dapper/FluentValidation AOT incompatibilities outright.

**Re-entry trigger**: a serverless / scale-to-zero deployment target.

## D8 — Charts: adapter contract + two engines

The future-proofing deliverable is a **`ChartAdapter` contract** (owned by Task 06): series/data spec (raw or LTTB-downsampled), lifecycle (`mount/update/resize/setTheme/destroy`), theming tokens, and a registry keyed by chart type.

- **Apache ECharts** — default engine (OLAP, KPI sparklines, general charts).
- **TradingView Lightweight Charts** — price/candlestick views (45KB, self-contained).
- **Tremor** — a React KPI component kit, not an engine; wrap ad hoc, no up-front design needed.
- **Deleted**: `WebGLContextPoolManager` and the 512MB client memory governor — neither engine uses WebGL as configured; reintroduce only when a real WebGL renderer (e.g. `echarts-gl`, regl) enters.

## D9 — Infrastructure: Tier 1 only

One container host (API + static frontend behind Caddy) + managed PostgreSQL. Tiers 2/3 (ECS clusters, EKS/Karpenter, ScyllaDB, Redis) are deleted, not deferred-as-specced — they will be respecified against the shipped stack when a growth signal appears. Local dev: `docker compose` runs **postgres (plain `postgres:17`) only**.

## D10 — Verification honesty

- No absolute performance gates. Removed: >35,000 req/sec, <50ms p99, <5ms cold start, <30/50MB RAM, 5,000 msg/sec, 60fps assertions, <10ms query asserts.
- Gates are: functional tests green + **recorded measured baselines** (k6/Playwright runs record numbers on a documented reference machine; a later run failing >20% below its recorded baseline fails).
- Every "Verified" status in any document is stripped until code exists and the verification actually ran.

## D11 — Security baseline

- JWT auth on **every** endpoint — no `AllowAnonymous`. Actor and company identity come from token claims only, never from request bodies.
- Semantic compiler: every identifier (dimensions, measures, sort members, granularities) is validated against the compiled model whitelist; filter **values** are parameterized; user-supplied strings are **never** interpolated into SQL (this includes `sorts.member` and granularity aliases).
- Semantic model YAML is trusted-admin content, versioned in the repo — not runtime-user-editable until a sandboxing design exists.

## D12 — Repository strategy: monorepo

Single repository: `src/Backend`, `src/Frontend`, `src/Database`, `infra/`, `tests/`, plus `architecture/`+`tasks/` docs. Rationale: contract-first TypeGen (C#→TS) requires atomic cross-stack commits; one verification pipeline covers the 4-stage gates; agents get one checkout and one AGENTS.md; the task specs already assume this exact layout. CI uses path filters per area. Split a component out only when it gains an independent release cadence or team.

## D13 — Naming and ownership fixes

- Audit table is **`audit_log`** everywhere (`bi_temporal_audit_log` in `tasks/README.md` was drift).
- ArchUnitNET boundary tests: single owner = **Task 08**.

---

## Contract ownership matrix (gap fill)

| Contract | Owner | Consumers |
| :--- | :--- | :--- |
| REST API endpoints + DTOs (TypeGen source, one pinned version) | Task 02 | Tasks 05, 06, 09 |
| DB schema, `audit_log` triggers, outbox table | Task 01 | Tasks 02, 03 |
| Outbox dispatcher + SignalR hub/groups + client event envelope | Task 03 | Task 05 |
| Semantic AST + `POST /api/v1/analytics/query` | Task 04 | Task 06 (its divergent `SemanticQueryAST`/`/api/v1/semantic/query` is void) |
| `ChartAdapter` contract + registry | Task 06 | dashboards |
| docker-compose + Terraform Tier 1 | Task 07 | Tasks 09, 10 |
| **Removed contracts — delete on sight** | — | `/api/v1/mutations/batch`, Dexie mutation schema, `perform3WayMerge`, NATS subjects/streams, Merkle proof formats |

## Task impact summary

| Task | Impact | Required revision |
| :--- | :--- | :--- |
| 01 | Minor | Drop TimescaleDB (extension, hypertable, continuous aggregate); plain `postgres:17`; keep btree_gist bi-temporal audit; add `version BIGINT` to mutable entities |
| 02 | Moderate | Drop AOT; JWT policies replace `AllowAnonymous`; drop `/api/v1/mutations/batch`; OCC via `version` column |
| 03 | **Rewrite** | NATS removed entirely → in-proc outbox dispatcher + LISTEN/NOTIFY + SignalR fan-out (incl. the delivery pipeline the old spec never specified) |
| 04 | Moderate | Drop DuckDB WASM/Arrow; fix identifier-whitelist injection holes; one query path; wire or delete dbt marts |
| 05 | Major | Drop Dexie/offline/batch/3-way merge; TanStack optimistic + 409 flow; in-memory undo |
| 06 | Major | ChartAdapter contract is the deliverable; ECharts + Lightweight Charts; delete WebGL pool + memory governor; adopt Task 04's AST/endpoint |
| 07 | **Rewrite** | Tier 1 only; plain `postgres:17`; no NATS/Redis/ScyllaDB; versioned backup bucket instead of WORM |
| 08 | Minor | Remove AOT references; formally own ArchUnitNET |
| 09 | Moderate | Baseline-regression gates replace absolute numbers; drop offline-replay scenarios; NBomber replaced by k6 (commercial-license issue) |
| 10 | Moderate | Drop Merkle/WORM, NATS, and batch-endpoint verification; remove `|| true` from verify scripts |

---

## D14 — Target cloud: Azure (2026-08-06)

**Decision**: Tier-1 production infrastructure targets **Azure** (Container Apps + PostgreSQL Flexible Server 17 + Blob storage with versioning + Key Vault), replacing the previous spec's AWS target.

**Why**: The AWS choice was never grounded in organizational reality — the organization's identity tenant and existing production PostgreSQL run on Azure. The previous AWS spec was also internally impossible (Aurora + TimescaleDB). With TimescaleDB cut (D3), managed PostgreSQL Flexible Server becomes viable. The D6 "versioned S3 bucket" requirement is fulfilled by an Azure Storage account with blob versioning + retention policy; blueprint references to "S3" should be read as "versioned object storage".

**Status**: Applied in the rewritten `tasks/task-07-infrastructure-terraform-docker.md`. Flagged for explicit user confirmation — if AWS (or another provider) is preferred, only task-07 §5–6 and this entry change; nothing else in the architecture depends on the provider.

## D15 — Adopt .NET 10 LTS and NuGet Central Package Management (2026-08-08)

All .NET projects target `net10.0`. The repository pins SDK `10.0.103` in `global.json`
with `rollForward: latestFeature`, allowing newer .NET 10 feature bands while preventing
an unintended roll to .NET 11. This supersedes the previous .NET 9 backend baseline.

NuGet Central Package Management is the sole package-version source. Root
`Directory.Packages.props` enables `ManagePackageVersionsCentrally` and
`CentralPackageTransitivePinningEnabled`; project files keep versionless
`PackageReference` entries. Transitive pinning makes security-response upgrades explicit
and reviewable in one manifest instead of relying on whichever transitive version restore
selects. `GlobalPackageReference` is deliberately documented but unused here; Task 14 owns
the repo-wide analyzer entries.

## D16 — Frontend compiler and quality platform (2026-08-09)

The frontend is compiled with Vite 8's default Rolldown pipeline and React Compiler
1.0 for every React source file. `@vitejs/plugin-react` 6 moved compiler integration
from its removed `babel` option to the official `reactCompilerPreset` together with
`@rolldown/plugin-babel`; this is the supported Vite 8 equivalent of the originally
specified plugin-react 5 configuration. React Hooks 7 exposes compiler diagnostics as
individual rules rather than a rule named `react-compiler`, so the flat configuration
enables the supported `config` rule alongside the Rules of Hooks.

Tailwind 4 is CSS-first through `@tailwindcss/vite`; design tokens live in
`src/Frontend/src/styles.css`, with no JavaScript Tailwind configuration. ESLint 10 uses
only flat configuration and type-aware typescript-eslint rules. Boundaries 7 is used
instead of the task draft's 5.x pin because 5.x calls an ESLint context API removed by
ESLint 10. Knip is a required frontend CI gate. JSON APIs are made `unknown` by
`@total-typescript/ts-reset` so validation remains mandatory at external boundaries.

The exact adopted platform matrix is: React/React DOM 19.2.8, TypeScript 5.9.3,
Vite 8.2.1, `@vitejs/plugin-react` 6.0.5, React Compiler 1.0.0,
Tailwind/`@tailwindcss/vite` 4.3.3, ESLint 10.8.1, typescript-eslint 8.66.0,
React Hooks 7.1.1, jsx-a11y 6.10.2, Testing Library ESLint 7.16.2,
Vitest ESLint 1.6.26, boundaries 7.1.0, and Knip 6.32.0. Node support is restricted
to the Vite-supported 20.19+, 22.13+, and 24+ release lines.

selects. D15 reserves `GlobalPackageReference` for Task 14; D17 activates it for the
repo-wide analyzer entries.

## D17 — Backend compile-time safety toolchain (2026-08-09)

Analyzer findings are build failures on every .NET project. The repository enables the
SDK analyzers plus Meziantou.Analyzer 3.0.139, SonarAnalyzer.CSharp 10.30.0.144632,
Microsoft.CodeAnalysis.BannedApiAnalyzers 5.6.0, and
Microsoft.VisualStudio.Threading.Analyzers 18.7.23 as `GlobalPackageReference` entries
with `PrivateAssets="all"`. `BannedSymbols.txt` makes direct wall-clock reads, lossy
decimal/double money conversions, and culture-implicit numeric parsing compile-time
errors. Test projects additionally reference xunit.analyzers 1.27.0.

CSharpier 1.3.0 is the sole backend formatter. The matching `CSharpier.MsBuild` global
package checks formatting during builds, while the pinned local-tool manifest drives the
same whole-repository `csharpier check` command in local verification and CI. Keeping the
CLI and MSBuild package at one version makes formatting deterministic across editors,
developer machines, and CI.

DTO/domain mapping uses Riok.Mapperly 4.3.1 source-generated partial mappers. Options use
Microsoft.Extensions.Options 10.0.10 source-generated `[OptionsValidator]` validators and
startup `.ValidateOnStart()` checks. These choices keep mapping and configuration
validation compile-time generated, with no reflection-based mapping or validation path.
