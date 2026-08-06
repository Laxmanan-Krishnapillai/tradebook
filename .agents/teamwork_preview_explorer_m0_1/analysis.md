# Detailed Codebase & Core Domain Investigation Report: Tradebook

**Agent ID**: `teamwork_preview_explorer_m0_1`  
**Working Directory**: `c:\Users\LaxmananKrishnapilla\tradebook\.agents\teamwork_preview_explorer_m0_1`  
**Date**: 2026-08-04  
**Target Architecture**: Tradebook High-Performance Hybrid Web Application  

---

## 1. Executive Summary & System Overview

Tradebook is designed as a high-performance, real-time, hybrid web application architecture for data management, workflow canvases, kanban boards, and interactive analytics. The codebase currently consists of comprehensive architectural blueprints, a 25-point independent critique, and an unconstrained alternative architecture evaluation.

### Key Architectural Baseline
- **Frontend Layer**: React 19 SPA powered by Vite, `@tanstack/react-router` for type-safe code-split routing, `Zustand` for global UI state, `XState` (`@xstate/react`) for workflow state machines, `@xyflow/react` (React Flow) for node-based canvas diagrams, `@dnd-kit/core` & `@dnd-kit/sortable` for drag-and-drop operations, and `@tanstack/react-virtual` for virtualized list/grid rendering.
- **Backend Layer**: .NET 9 Web API structured using **Vertical Slice Architecture** with `FastEndpoints` (REPR pattern: Request-Endpoint-Response), `FluentValidation`, `SurrealDb.Net` SDK for database interactions, and `Hangfire` for background job orchestration.
- **Database & Security Layer**: `SurrealDB` operating as a multi-model document/graph database with WebSocket live query capabilities (`LIVE SELECT`). Authorization is governed via Record-Level Security (`PERMISSIONS` clauses with `TYPE RECORD` access).
- **Data Access Pattern (CQRS Split)**: Direct browser WebSocket connection for read-only `SELECT` and `LIVE SELECT` queries; all mutations (`CREATE`, `UPDATE`, `DELETE`) are routed exclusively through the .NET backend API to maintain security, auditability, and side-effect consistency.

---

## 2. Comprehensive Codebase File Structure & Layout

The project repository at `c:\Users\LaxmananKrishnapilla\tradebook` is organized into three major documentation trees alongside user requirements:

```text
c:\Users\LaxmananKrishnapilla\tradebook\
├── ORIGINAL_REQUEST.md                 # 4 Core Research Pillars specification
├── README.md                           # Master architectural overview & section map
├── architecture/                       # Baseline Architectural Plan
│   ├── overview.md                     # System architecture diagram & stack matrix (§1, §2)
│   ├── folder-structure.md             # Canonical frontend and backend directory trees (§3)
│   └── testing-and-assumptions.md      # Initial assumptions and verification strategy (§4, §5)
├── review/                             # Independent Architectural Critique (25 Action Items)
│   ├── README.md                       # Review index and navigational guide
│   ├── action-items.md                 # Master 25-point action item summary matrix (§6.15)
│   ├── access-control-and-data-model.md # RLS/JWT bugs (§6.1), Direct DB risk (§6.2), Multi-tenancy (§6.3), Revocation (§6.4)
│   ├── backend-and-jobs.md             # Hangfire second datastore requirement (§6.5)
│   ├── frontend-state-and-ui.md        # TanStack Query (§6.6), Animations (§6.7), React Flow+dnd-kit (§6.8), CQRS split (§6.11)
│   ├── performance-and-scalability.md  # Perceived latency levers (§6.10)
│   ├── surrealdb-production-readiness.md# SurrealDB production maturity & BSL 1.1 license (§6.9)
│   ├── agent-readiness.md              # Autonomy risk ranking (§6.12), Tooling recommendations (§6.13)
│   └── engineering-and-product-gaps.md # Testing depth, CI/CD, RFC 7807 error contracts, a11y (§6.14)
├── alternatives/                       # Unconstrained Architectural Exploration
│   ├── README.md                       # Alternatives index
│   ├── recommendation.md               # Ranked two-part decision roadmap (§7.9)
│   ├── local-first-sync-engines.md     # PowerSync, ElectricSQL, TanStack DB, Zero, Replicache (§7.2, §7.7, §7.8)
│   ├── reactive-backend-as-database.md # Convex / InstantDB evaluation (§7.1)
│   ├── crdt-collaboration.md           # Yjs / Automerge canvas collaboration debunking (§7.3)
│   ├── surrealdb-embedded-wasm.md      # In-browser WASM SurrealDB limitations (§7.4)
│   ├── edge-compute.md                 # Cloudflare Workers / Durable Objects (§7.5)
│   └── workflow-engine-alternatives.md # Restate.dev actor-model execution engine (§7.6)
└── research/                           # Destination folder for the 4 research pillar outputs
```

---

## 3. Core Domain Entities, Data Models & Technical Stack Mapping

### 3.1 Database & Security Model (`SurrealDB`)
- **Tables & Schema Delineation**:
  - `project`: Primary workspace entity. Fields: `id`, `tenant` (Record link / UUID), `name`, `owner` (User ID), `created_at`, `updated_at`.
  - `workflow`: Workflow canvas definition containing nodes and edges.
  - `kanban_board`, `kanban_card`, `kanban_tag`: Task management entities with many-to-many tag relations via graph edges.
  - `audit_log` / `version_history`: Append-only records tracking mutation attribution and state snapshots.
- **Security Rule Corrections (`TYPE RECORD`)**:
  - *Correction from `review/access-control-and-data-model.md: line 5-17`*: Plain `TYPE JWT` access bypasses `PERMISSIONS` clauses entirely. The system must authenticate via `DEFINE ACCESS ... TYPE RECORD WITH JWT` so `$auth` populates with `$auth.tenant_id` and `$auth.id`.
  - *Read-Only Allowlist*: Tables enforce `PERMISSIONS FOR create, update, delete NONE` for browser clients. Browsers are granted `FOR select WHERE tenant = $auth.tenant_id AND (owner = $auth.id OR $auth.role = 'admin')`.

### 3.2 Backend Architecture (`.NET 9` Vertical Slice)
- **Pattern**: REPR (Request-Endpoint-Response) using `FastEndpoints`.
- **Feature Slices (`/backend/src/Features/`)**:
  - `Auth/`: `LoginEndpoint.cs`, `LoginRequest.cs`, `LoginResponse.cs`, `LoginValidator.cs`.
  - `Projects/`: `CreateProjectEndpoint.cs`, `ProcessProjectBackgroundJob.cs`.
  - `Workflows/`: `ExecuteWorkflowEndpoint.cs`.
- **Background Jobs**: `Hangfire` configured with an explicit PostgreSQL instance (`review/backend-and-jobs.md: line 5-7`).
- **Error Standard**: RFC 7807 / RFC 9457 `ProblemDetails` integrated with `FluentValidation` (`review/engineering-and-product-gaps.md: line 13`).

### 3.3 Frontend Architecture (`React 19` + `Vite`)
- **State Delineation**:
  - **Global UI State**: `Zustand` (`useAuthStore`, `useUIStore`) for session, active modal, and layout settings.
  - **Workflow Execution**: `XState` state machines for complex multi-step interactions and canvas editing flows.
  - **Entity Caching & Optimistic State**: `@tanstack/react-query` acting as the canonical client cache.
  - **Canvas & Graph Rendering**: `@xyflow/react` (React Flow) for interactive node diagrams.
  - **Drag-and-Drop**: `@dnd-kit/core` & `@dnd-kit/sortable` for kanban and list sorting.
  - **Virtualized Data Tables**: `@tanstack/react-virtual` for rendering high-density datasets.

### 3.4 Frontend CQRS Read/Write Reconciliation Pattern
- *Source: `review/frontend-state-and-ui.md: line 27-95`*
- **Read Path**: `@tanstack/react-query` seeded with initial query, updated via WebSocket subscriptions (`LIVE SELECT` streams wrapped via `surqlize`).
- **Write Path**: Client generates a deterministic ULID/UUID (`clientId`), passes it in a POST/PATCH request to `.NET` FastEndpoints. `onMutate` immediately writes the optimistic record into TanStack Query cache.
- **Reconciliation**: When `.NET` persists the record in SurrealDB, SurrealDB emits a `LIVE SELECT` push. Because the server record shares the exact `clientId`, the live query update replaces the optimistic record seamlessly without key collisions or UI flickering.

---

## 4. Synthesis of Critique & Review Findings

The independent review (`review/action-items.md`) identified 25 critical action items across system security, performance, data sync, and DX:

| Ref | Category | Observation & Defect | Critical Resolution |
|---|---|---|---|
| §6.1 | Security | `$auth` unpopulated with plain `TYPE JWT` | Enforce `TYPE RECORD WITH JWT` so RLS `PERMISSIONS` execute |
| §6.2 | Data Access | Direct client DB write risk & dual write logic | Adopt Read-Only direct queries (`SELECT`/`LIVE SELECT`) + Backend-only writes |
| §6.3 | Multi-tenancy | Shared table `tenant` column risk | Evaluate Namespace/Database-per-tenant structural isolation |
| §6.4 | Auth | JWT revocation unhandled | Implement short-lived access tokens (5-15m) + refresh tokens in .NET |
| §6.5 | Infrastructure | Hangfire requires persistent store | Provision dedicated PostgreSQL instance for Hangfire job state |
| §6.6 | Frontend | Missing REST data fetching layer | Integrate `@tanstack/react-query` for all .NET endpoints |
| §6.7 | UI Performance | Heavy Framer Motion animations in canvas | Restrict Framer Motion / Aceternity UI to marketing/onboarding pages |
| §6.8 | Interaction | React Flow zoom desync with dnd-kit `getBoundingClientRect` | Implement zoom-aware coordinate translation and per-node `DndContext` |
| §6.9 | Database | SurrealDB BSL 1.1 license & 3.x breaking changes | Establish automated binary schema migrations and backup validation |
| §6.10 | Latency | Network topology mistaken for smoothness driver | Focus on optimistic UI updates, Zustand selectors, and virtualized lists |
| §6.11 | CQRS | Disconnected read/write channels cause UI flicker | Standardize on shared client-side ULID/UUID keys in TanStack Query |
| §6.12 | AI Readiness | Permissive schema changes bypass build checks | Mandatory live DB tests + human review gate for any `schema/` diffs |
| §6.13 | Governance | Architectural boundary decay in C# | Enforce `ArchUnitNET` rules and `Stryker.NET` mutation testing |
| §6.14 | Quality Gaps | Absence of contract testing, E2E a11y, and RFC 7807 | Add OpenAPI client codegen (Orval), `@axe-core/playwright`, and Problem Details |

---

## 5. Alternative Architecture Evaluation & Strategic Positioning

The alternative exploration (`alternatives/recommendation.md`) frames Tradebook's technical roadmap into two distinct decisions:

1. **Decision A (Low-Risk Additive Upgrade — Recommended Initial Step)**:
   - **Pilot `TanStack DB` with SurrealDB**: Leverage `ForetagInc/tanstack-db-surrealdb` adapter to wrap SurrealDB's `LIVE` WebSocket stream.
   - **Benefit**: Provides incremental differential dataflow processing (via `d2ts`) for client-side joins (e.g., kanban card tags, node edges) while keeping the current .NET + SurrealDB stack 100% intact.
   - **Scope**: Pilot first on the Kanban feature slice.

2. **Decision B (Major Architecture Shift — Contingency Path)**:
   - **Migrate to PostgreSQL + Local-First Sync Engine**: If SurrealDB encounters severe production scaling or live-query memory limits (`review/performance-and-scalability.md: line 18`), evaluate **ElectricSQL + TanStack DB** or **PowerSync**.
   - **Benefit**: Achieves true zero-network-wait reads via browser SQLite/WASM replication.

3. **CRDT Clarification (`alternatives/crdt-collaboration.md`)**:
   - Industry analysis (Figma, tldraw) demonstrates that full CRDT engines (Yjs/Automerge) are unnecessary for single-editor workflow canvases. Simple debounced server writes with last-writer-wins reconciliation are superior in simplicity and performance.

---

## 6. Mapping Existing State to the 4 Research Pillars

### Pillar 1: Versioning & Audit Trails Architecture Research
- **Tradebook Context**: Needs full revertability and granular change attribution ("who changed what and when").
- **Existing Assets**: `review/access-control-and-data-model.md` (§6.2 dual write path) highlights that direct writes cause untracked side effects. Routing writes through .NET backend enables central interceptors.
- **Key Frameworks to Investigate**: Event Sourcing vs. Temporal Tables vs. Delta/Iceberg vs. Git-style branch/merge models.
- **Technical Baseline**: Append-only event logs in SurrealDB/Postgres, server-side attribution middleware in FastEndpoints, and immutable snapshot tables.

### Pillar 2: Semantic Data Modeling & Multi-System Data Pipelines
- **Tradebook Context**: Needs flexible ingestion, transformation, and export across heterogeneous external systems with dynamic semantic layers.
- **Existing Assets**: SurrealDB multi-model graph/document capabilities (`architecture/overview.md`).
- **Key Frameworks to Investigate**: dbt-style semantic layers, Cube.js, Malloy, GraphQL, dynamic EAV/Graph models, and data pipeline orchestration.
- **Technical Baseline**: Unified schema mapping layer, SQL/SurrealQL query abstractions, and background extraction pipelines (Hangfire/Restate).

### Pillar 3: High-Performance Snappy CRUD UI/UX Tech Stack
- **Tradebook Context**: Needs Linear/Twenty-grade snappy, optimistic, keyboard-first UI experiences.
- **Existing Assets**: `review/frontend-state-and-ui.md` (§6.11 CQRS pattern), `review/performance-and-scalability.md` (§6.10 latency levers), `alternatives/local-first-sync-engines.md` (§7.8 TanStack DB).
- **Key Frameworks to Investigate**: Local-First sync engines (Zero, ElectricSQL, PowerSync, Replicache, TanStack DB), TanStack Query, optimistic UI patterns, Virtualized tables (AG Grid, TanStack Table, Canvas tables).
- **Technical Baseline**: TanStack Query + TanStack DB incremental collections, client-side ULID generation, custom keyboard shortcut routing, and virtualized data grids.

### Pillar 4: Plug-and-Play Custom Visualizations Framework
- **Tradebook Context**: User-configurable, dynamic chart and dashboard visualization integration with semantic models.
- **Existing Assets**: `architecture/folder-structure.md` (`src/features/analytics/`).
- **Key Frameworks to Investigate**: Tremor, Nivo, Apache ECharts, Lightweight Charts, Observable Plot, Embedded BI (Metabase/Lightdash).
- **Technical Baseline**: Dynamic query translation from semantic model to chart spec, responsive canvas widgets, and real-time WebSocket live-data bindings.

---

## 7. Conclusions & Handoff Readiness

The investigation establishes a complete blueprint of Tradebook's current state, risks, and research foundation. The findings have been synthesized into `handoff.md` to guide subsequent architectural research passes.
