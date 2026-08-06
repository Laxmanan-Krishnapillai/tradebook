# Tradebook Master Task Breakdown & Feature Inventory Report

**Author**: Explorer 3 (Task Breakdown & Feature Inventory Explorer)  
**Date**: August 5, 2026  
**Target File**: `c:\Users\LaxmananKrishnapilla\tradebook\.agents\teamwork_preview_explorer_r3_3\analysis.md`  
**Status**: Comprehensive Master Task Breakdown & Requirements Synthesis  

---

## 1. Comprehensive Requirements Survey Across Iterations 1–3

This section provides a rigorous synthesis of all requirements and architectural deliverables across the three iterative design phases of the Tradebook platform.

```
+-------------------------------------------------------------------------------------------------------------------------+
|                                              TRADEBOOK REQUIREMENTS MAP                                                 |
+-------------------------------------------------------------------------------------------------------------------------+
| ITERATION 1: Research & Exploratory Design                                                                              |
|   ├── R1: Versioning & Audit Trails (Event Sourcing, Temporal Tables, Delta/Iceberg, Bi-Temporal Models)                 |
|   ├── R2: Semantic Data Modeling & Multi-System Pipelines (dbt, Cube.js, Malloy, EAV/Graph Abstractions)                |
|   ├── R3: High-Performance Snappy CRUD UI/UX (Linear/Twenty UX, Local-First, TanStack Query/DB, Virtualization)          |
|   └── R4: Plug-and-Play Custom Visualizations (ECharts, Tremor, Nivo, Dynamic Dashboard Builder)                         |
+-------------------------------------------------------------------------------------------------------------------------+
| ITERATION 2: Architectural Refinement & Simplification                                                                  |
|   ├── R1: Adversarial Tech Stack Review (Consolidation to .NET 9 + PostgreSQL 17 + TimescaleDB + NATS JetStream)        |
|   ├── R2: Real-World Industry Case Studies (Linear, Twenty, PostHog, Supabase, Retool learnings & post-mortems)          |
|   └── R3: Infrastructure Architecture & Cost Scaling (Terraform Tier 1/2/3 modules, cost curves, trade-off matrices)   |
+-------------------------------------------------------------------------------------------------------------------------+
| ITERATION 3: Master Blueprint, Agent-Readiness & Execution Planning                                                     |
|   ├── R1: Master Architecture Blueprint Consolidation (`architecture/master-architecture-blueprint.md`)               |
|   ├── R2: Agent-Readiness Framework (`research/agent-readiness-framework.md`, AGENTS.md, TypeGen, Stryker)              |
|   └── R3: Master Task Breakdown & Subagent Writedown (`tasks/README.md`, `tasks/task-01-database...md` to `task-10...`) |
+-------------------------------------------------------------------------------------------------------------------------+
```

### 1.1 Iteration 1 Requirements & Findings (R1–R4)
1. **R1. Versioning & Audit Trails Architecture**:
   - Evaluated Event Sourcing, Temporal Tables, Delta/Iceberg, and CRDTs for granular change attribution ("who changed what and when") and full revertability.
   - *Key Synthesis*: Settled on PostgreSQL native `TSTZRANGE` bi-temporal tables (tracking valid time $V_t$ and system time $S_t$) with trigger-automated transaction audit logs to eliminate complex external ledger clusters.
2. **R2. Semantic Data Modeling & Multi-System Data Pipelines**:
   - Researched ingestion, transformation, and query abstractions across heterogeneous data sources using dbt, Cube.js, Malloy, and GraphQL.
   - *Key Synthesis*: Combined a dynamic C# EAV/Graph query builder with dbt SQL transformations and TimescaleDB continuous aggregates, exposed via a unified REST/GraphQL query layer.
3. **R3. High-Performance Snappy CRUD UI/UX Tech Stack**:
   - Investigated local-first sync engines (Zero, ElectricSQL, PowerSync, Replicache) and frontend frameworks powering Linear, Twenty, Notion, and Figma.
   - *Key Synthesis*: Selected React 19 + TypeScript with TanStack Query v5, TanStack DB, IndexedDB (Dexie.js) optimistic mutation queues, virtualized TanStack Table grids, and command-palette (Kbar/cmdk) navigation.
4. **R4. Plug-and-Play Custom Visualizations Framework**:
   - Evaluated dynamic charting libraries (Tremor, Nivo, Apache ECharts, Lightweight Charts, Observable Plot).
   - *Key Synthesis*: Standardized on Apache ECharts (for heavy canvas/WebGL financial charts) and Tremor (for clean UI dashboards), dynamically bound to Semantic Layer query endpoints via React Grid Layout.

### 1.2 Iteration 2 Requirements & Findings (R1–R3)
1. **R1. Adversarial Tech Stack & Complexity Review**:
   - Challenged hyper-fragmented polyglot setups (SurrealDB + ScyllaDB + ClickHouse + Kafka + Go/Rust services).
   - *Key Synthesis*: Applied **90/10 Engineering** under the non-negotiable .NET 9 requirement. Consolidated database and streaming into **PostgreSQL 17 + TimescaleDB** and **NATS JetStream**, powered by a **.NET 9 Modular Monolith** (Native AOT, FastEndpoints, SignalR Core binary push), achieving a 75.8% Complexity Reduction Score (CRS).
2. **R2. Real-World Industry Case Studies & Engineering Learnings**:
   - Analyzed tech evolutions and post-mortems from Linear, Twenty CRM, PostHog, Supabase, and Retool.
   - *Key Synthesis*: Adopted key architectural patterns: Linear's local-first sync queue, Twenty's modular TypeScript UI slices, PostHog's ingestion outbox resilience, and Retool's dynamic visualization component model.
3. **R3. Infrastructure Architecture, Terraform Setups, & Cost Scaling Analysis**:
   - Formulated 3 cloud deployment tiers with HCL Terraform modules and itemized cost scaling (100 to 1M users).
   - *Key Synthesis*: Standardized Tier 1 (Lean PaaS / Serverless Container), Tier 2 (Growth AWS ECS Fargate + Managed Aurora PostgreSQL), and Tier 3 (Scale Self-Hosted EKS Kubernetes) infrastructure specifications.

### 1.3 Iteration 3 Requirements & Findings (R1–R3)
1. **R1. Master Architecture Consolidation Document**:
   - Unified all prior research into `architecture/master-architecture-blueprint.md` as the authoritative system specification.
2. **R2. Agent-Readiness Research & Engineering Framework**:
   - Designed developer and AI agent ergonomics: `AGENTS.md` context rules, `TypeGen` automated C#-to-TypeScript contract generation, ArchUnitNET boundary tests, Stryker.NET mutation testing, and hermetic Docker Compose devcontainer setup (`research/agent-readiness-framework.md`).
3. **R3. Master Task Breakdown & Execution Planning**:
   - Formulated a 10-task master breakdown index (`tasks/README.md`) and individual execution blueprints (`tasks/task-01...md` through `tasks/task-10...md`).

---

## 2. Consolidated Feature, Module, Infrastructure & Engineering Inventory

To ensure complete coverage across the platform, every required component is categorized below into 9 technical domains.

| Domain | Engineering Sub-Modules & Components | Key Requirements & Standards |
| :--- | :--- | :--- |
| **1. Database & Bi-Temporal Storage** | - PostgreSQL 17 primary relational schema<br>- TimescaleDB hypertable partitioning<br>- Bi-temporal audit logging (`TSTZRANGE` $V_t, S_t$)<br>- Transactional outbox table (`outbox_messages`) | - Sub-50ms bi-temporal query resolution<br>- Strict ACID isolation & exclusion constraints<br>- `pgBackRest` WAL archiving & PITR support |
| **2. .NET 9 Modular Monolith Backend** | - ASP.NET Core Native AOT application<br>- FastEndpoints REPR pattern API endpoints<br>- EF Core 9 + Dapper hybrid ORM repositories<br>- `HybridCache` L1 memory / L2 NATS caching tier<br>- `AddOptionsWithValidateOnStart()` strongly-typed config | - >35,000 req/sec benchmark per node<br>- Sub-45MB baseline RAM footprint<br>- Zero runtime circular namespace dependencies |
| **3. Real-Time Streaming & Event Bus** | - NATS JetStream pub/sub & KV storage engine<br>- Background outbox worker (`System.Threading.Channels`)<br>- SignalR Core Hub with binary MessagePack protocol<br>- Dynamic group subscriptions & client backpressure | - MessagePack binary serialization overhead reduction (>60% vs JSON)<br>- Automatic WebSocket reconnect with state recovery |
| **4. Dynamic Semantic Layer & dbt Engine** | - Dynamic EAV/Graph relational query builder<br>- dbt SQL transformation project models<br>- Cube.js / dbt REST & GraphQL query interface<br>- TimescaleDB continuous aggregate Materialized Views | - Dynamic metric computation with sub-100ms latency<br>- Pre-computed continuous aggregation rollups |
| **5. React 19 Snappy Local-First UI** | - React 19 + TypeScript + Vite frontend SPA<br>- TanStack Query v5 + TanStack DB sync cache<br>- IndexedDB (Dexie.js) offline mutation queue<br>- Command palette navigation (Kbar / cmdk)<br>- Virtualized data grid (TanStack Table) | - Optimistic UI rendering (<16ms frame target)<br>- Full keyboard accessibility & shortcut routing<br>- Automatic offline retry & conflict resolution |
| **6. Custom Visualizations Framework** | - Dynamic chart widget registry (ECharts + Tremor)<br>- Visual query builder & metric selector UI<br>- Drag-and-drop dashboard canvas (React Grid Layout)<br>- Chart configuration serialization & export | - Plug-and-play semantic model query binding<br>- Sub-30ms chart re-rendering under live stream |
| **7. IaC Infrastructure & Docker Containers** | - HCL Terraform modules for Tiers 1, 2, and 3<br>- Multi-stage Dockerfile (.NET AOT + Node Vite)<br>- Developer/Agent `docker-compose.yml`<br>- VS Code `.devcontainer/devcontainer.json` | - Single-command `docker compose up` local setup<br>- Deterministic IaC linting via `tflint` / `checkov` |
| **8. Agent-Readiness & Developer Ergonomics** | - Root `AGENTS.md` and module-level `AGENTS.md`<br>- `TypeGen` automated TypeScript DTO generator<br>- `ArchUnitNET` vertical slice boundary test suite<br>- `Stryker.NET` mutation testing pipeline | - Strict CI build failures on contract drift<br>- Minimum 80% mutation testing score threshold |
| **9. E2E Testing & Performance Verification** | - Playwright E2E browser automation test harness<br>- NBomber load testing & latency benchmarking<br>- Integration test suite with testcontainers-dotnet<br>- Continuous Integration pipeline (`ci.yml`) | - 100% automated assertion of snappy mutations<br>- Stress validation under 10,000 msg/sec SignalR stream |

---

## 3. Master Implementation Task Sequencing & Dependency Graph

### 3.1 Logical Dependency & Flow Diagram

```
[Task 01: PostgreSQL 17 + TimescaleDB Setup] ──────────────────────────┐
  │                                                                    │
  ▼                                                                    ▼
[Task 02: .NET 9 Backend Vertical Slices]              [Task 07: Infrastructure & Docker]
  │                                                                    │
  ├───────────────────────────────┬────────────────────────────────────┤
  ▼                               ▼                                    ▼
[Task 03: SignalR + NATS Bus]  [Task 04: Semantic Layer]     [Task 08: Agent-Readiness Tools]
  │                               │                                    │
  └───────────────┬───────────────┘                                    │
                  ▼                                                    │
[Task 05: React 19 Snappy UI] ◄────────────────────────────────────────┘
  │
  ▼
[Task 06: Custom Visualizations Framework]
  │
  ▼
[Task 09: E2E Testing & NBomber Performance Harness]
  │
  ▼
[Task 10: Platform Integration & Master Documentation]
```

### 3.2 Master Task Breakdown Summary Table

| Task ID | Task Title | Phase | Primary Dependencies | Estimated Effort |
| :--- | :--- | :--- | :--- | :--- |
| **Task 01** | Core Database Architecture & TimescaleDB Bi-Temporal Audit Setup | Storage Layer | None | High |
| **Task 02** | .NET 9 Modular Monolith Backend Core & Vertical Slice Framework | Backend Core | Task 01 | High |
| **Task 03** | SignalR Core Real-Time Engine & NATS JetStream Event Bus Integration | Real-Time Messaging | Task 01, Task 02 | High |
| **Task 04** | Dynamic Semantic Layer & dbt/Cube Analytical Query Pipeline | Data Pipelines | Task 01, Task 02 | High |
| **Task 05** | React 19 Keyboard-First Snappy CRUD UI & TanStack Local Sync | Frontend UI | Task 02, Task 03, Task 08 | Very High |
| **Task 06** | Plug-and-Play Custom Visualizations & Dynamic Dashboard Framework | Visualizations | Task 04, Task 05 | Medium |
| **Task 07** | Infrastructure as Code (IaC) Terraform Modules & Docker Setup | DevOps / Infra | Task 01, Task 02 | Medium |
| **Task 08** | Agent-Readiness Framework, Automated TypeGen & Tooling | Agent Ergonomics | Task 02 | Medium |
| **Task 09** | Automated End-to-End (E2E) Testing Harness & NBomber Benchmarks | QA / Performance | Task 03, Task 05, Task 07 | Medium |
| **Task 10** | Platform Integration, Master Documentation & Final Verification | Integration | Tasks 01–09 | Medium |

---

## 4. Detailed Master Task Specifications (Task 01 to Task 10)

---

### Task 01: Core Database Architecture & TimescaleDB Bi-Temporal Audit Setup

- **Phase**: Storage & Data Model Layer
- **Lead / Owner**: Database Engineering Lead
- **Complexity**: High
- **Prerequisites**: PostgreSQL 17 + TimescaleDB 2.15+ environment

#### 1. Detailed Scope & Feature Coverage
- Initialize PostgreSQL 17 engine with TimescaleDB extension enabled.
- Design core domain DDL: `portfolios`, `orders`, `executions`, `assets`, `accounts`.
- Implement native bi-temporal audit tracking with `valid_period TSTZRANGE` ($V_t$) and `system_period TSTZRANGE` ($S_t$).
- Construct automatic PostgreSQL audit triggers to log row mutations to `bi_temporal_audit_log`.
- Define transactional outbox table (`outbox_messages`) for atomic event persistence within domain transactions.
- Set up continuous aggregate hypertables for trade execution volume and portfolio valuation rollups.
- Configure `pgBackRest` WAL archiving script templates and physical backup retention rules.

#### 2. Key Deliverables & File Targets
- `src/Database/Migrations/001_initial_schema.sql` (Core DDL, bi-temporal types, outbox table)
- `src/Database/Migrations/002_timescaledb_hypertables.sql` (Hypertables & continuous aggregates)
- `src/Database/Functions/fn_bi_temporal_audit_trigger.sql` (PL/pgSQL bi-temporal trigger)
- `src/Database/Indexes/001_bi_temporal_indexes.sql` (GIST range & B-Tree indexes)
- `scripts/db-init.sh` (Database bootstrapping script)

#### 3. Architecture & Code Contract Blueprint
```sql
-- Bi-Temporal Portfolio Table Definition
CREATE TABLE portfolios (
    portfolio_id UUID NOT NULL,
    tenant_id UUID NOT NULL,
    name VARCHAR(255) NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'USD',
    metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
    valid_period TSTZRANGE NOT NULL,
    system_period TSTZRANGE NOT NULL DEFAULT TSTZRANGE(NOW(), NULL),
    created_by VARCHAR(100) NOT NULL,
    CONSTRAINT pk_portfolios PRIMARY KEY (portfolio_id, system_period),
    EXCLUDE USING GIST (portfolio_id WITH =, valid_period WITH &&, system_period WITH &&)
);
```

#### 4. Independent Verification Criteria
- Run `psql -f src/Database/Migrations/001_initial_schema.sql` on a fresh PostgreSQL 17 instance; verify zero SQL syntax errors.
- Execute automated SQL mutation test: insert, update, and soft-delete a portfolio record. Verify that `bi_temporal_audit_log` records exact historical snapshots with non-overlapping `TSTZRANGE` bounds.
- Verify GIST index performance: execute `EXPLAIN ANALYZE SELECT * FROM portfolios WHERE valid_period @> NOW()::timestamptz AND system_period @> NOW()::timestamptz;` and confirm Index Scan usage under <5ms execution time.

---

### Task 02: .NET 9 Modular Monolith Backend Core & Vertical Slice Framework

- **Phase**: Application Core Engine
- **Lead / Owner**: Backend Lead Architect
- **Complexity**: High
- **Prerequisites**: Task 01 (Database Schema)

#### 1. Detailed Scope & Feature Coverage
- Structure .NET 9 Solution (`Tradebook.sln`) adhering to Modular Monolith architecture and FastEndpoints REPR pattern.
- Configure Native AOT compilation flags (`<PublishAot>true</PublishAot>`) in C# `.csproj` files.
- Implement EF Core 9 for command mutations and Dapper for ultra-fast query read paths.
- Setup `HybridCache` tier combining L1 in-memory caching with L2 NATS KV storage.
- Enforce strict startup configuration validation using `.AddOptionsWithValidateOnStart().ValidateDataAnnotations()`.
- Build vertical feature slices: `Features/Portfolios`, `Features/Orders`, `Features/Executions`, `Features/Analytics`.

#### 2. Key Deliverables & File Targets
- `src/Backend/Tradebook.sln` (Main solution file)
- `src/Backend/Tradebook.Api/Program.cs` (ASP.NET Core entry point, FastEndpoints & DI setup)
- `src/Backend/Tradebook.Api/Tradebook.Api.csproj` (Native AOT configuration)
- `src/Backend/Tradebook.Domain/Entities/` (Domain models: Portfolio, Order, Execution)
- `src/Backend/Tradebook.Infrastructure/Persistence/` (EF Core DbContext, Dapper query handlers)
- `src/Backend/Tradebook.Features/Portfolios/CreatePortfolioEndpoint.cs` (FastEndpoints slice example)

#### 3. Architecture & Code Contract Blueprint
```csharp
// Program.cs startup pattern with FastEndpoints & Options Validation
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<DatabaseOptions>()
    .BindConfiguration("Database")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddFastEndpoints();
builder.Services.AddHybridCache();

var app = builder.Build();
app.UseFastEndpoints();
app.Run();
```

#### 4. Independent Verification Criteria
- Execute `dotnet build src/Backend/Tradebook.sln` with zero warnings and zero compilation errors.
- Execute `dotnet publish -c Release -r linux-x64 --self-contained` to verify Native AOT compilation success.
- Run integration tests: `dotnet test tests/Tradebook.IntegrationTests` verifying FastEndpoints HTTP responses return 200 OK with correct JSON payloads within <20ms.

---

### Task 03: SignalR Core Real-Time Engine & NATS JetStream Event Bus Integration

- **Phase**: Real-Time Messaging & Streaming
- **Lead / Owner**: Distributed Systems Specialist
- **Complexity**: High
- **Prerequisites**: Task 01 (Outbox Table), Task 02 (.NET Backend Core)

#### 1. Detailed Scope & Feature Coverage
- Integrate NATS JetStream client (`NATS.Client.Core`) for inter-process messaging and event streaming.
- Build transactional outbox background processor (`OutboxProcessorService`) utilizing `.NET 9 System.Threading.Channels` for non-blocking stream ingestion.
- Configure SignalR Core Hub with MessagePack binary protocol (`Microsoft.AspNetCore.SignalR.Protocols.MessagePack`).
- Implement connection group routing (tenant-level and portfolio-level real-time rooms).
- Implement client backpressure and buffer management to prevent slow-consumer memory growth.

#### 2. Key Deliverables & File Targets
- `src/Backend/Tradebook.Infrastructure/Messaging/NATSJetStreamService.cs` (NATS client wrapper)
- `src/Backend/Tradebook.Infrastructure/BackgroundServices/OutboxProcessorService.cs` (Outbox poller)
- `src/Backend/Tradebook.Api/Hubs/TradeStreamHub.cs` (SignalR core hub)
- `src/Backend/Tradebook.Contracts/Messages/PortfolioUpdatedMessage.cs` (MessagePack DTOs)

#### 3. Architecture & Code Contract Blueprint
```csharp
[MessagePackObject]
public record OrderExecutedMessage(
    [Key(0)] Guid OrderId,
    [Key(1)] Guid PortfolioId,
    [Key(2)] decimal ExecutionPrice,
    [Key(3)] decimal Quantity,
    [Key(4)] DateTime ExecutedAtUtc
);
```

#### 4. Independent Verification Criteria
- Launch NATS server container and execute outbox publisher test: insert 1,000 outbox records in Postgres; verify `OutboxProcessorService` drains all 1,000 records to NATS JetStream within <250ms.
- Connect a SignalR test client using MessagePack protocol; publish an execution event to NATS; verify the client receives the binary payload in <10ms latency.
- Run backpressure test: stream 10,000 updates/sec to a throttled client connection; confirm `System.Threading.Channels` drops older non-critical ticks without exhausting host RAM.

---

### Task 04: Dynamic Semantic Layer & dbt/Cube Analytical Query Pipeline

- **Phase**: Data Pipeline & Analytics Layer
- **Lead / Owner**: Data Architect / Analytics Engineer
- **Complexity**: High
- **Prerequisites**: Task 01 (TimescaleDB), Task 02 (.NET Backend)

#### 1. Detailed Scope & Feature Coverage
- Build a dynamic C# EAV/Graph relational query builder to translate user-defined metric requests into optimized SQL queries.
- Structure dbt project (`dbt_tradebook`) for continuous metric modeling and materialization.
- Configure Cube.js / dbt dynamic semantic model schemas for portfolio performance, PnL, and execution metrics.
- Implement TimescaleDB continuous aggregate refresh policy definitions.
- Expose REST and GraphQL semantic query endpoints in .NET 9 API.

#### 2. Key Deliverables & File Targets
- `src/Analytics/dbt_tradebook/dbt_project.yml` (dbt project configuration)
- `src/Analytics/dbt_tradebook/models/marts/portfolio_pnl_daily.sql` (dbt SQL transformation)
- `src/Analytics/Cube/schema/Portfolios.js` (Cube.js semantic dimension/metric schema)
- `src/Backend/Tradebook.Features/SemanticLayer/QueryBuilder.cs` (C# dynamic SQL generator)

#### 3. Architecture & Code Contract Blueprint
```csharp
public class SemanticQueryRequest
{
    public string Cube { get; set; } = default!;
    public List<string> Measures { get; set; } = new();
    public List<string> Dimensions { get; set; } = new();
    public List<SemanticFilter> Filters { get; set; } = new();
    public string TimeDimension { get; set; } = default!;
    public string Granularity { get; set; } = "day";
}
```

#### 4. Independent Verification Criteria
- Run `dbt compile` and `dbt test` inside `src/Analytics/dbt_tradebook`; verify all SQL transformation models compile and pass data validation tests.
- Execute dynamic query benchmark: post a JSON request requesting 3 measures across 2 dimensions filtered by date range; verify `QueryBuilder.cs` emits valid ANSI SQL and returns aggregated metrics in <50ms.
- Validate continuous aggregate auto-refresh: insert 5,000 raw executions; trigger `CALL refresh_continuous_aggregate('portfolio_pnl_daily', NULL, NULL);`; verify aggregated rows reflect updated sums.

---

### Task 05: React 19 Keyboard-First Snappy CRUD UI & TanStack Local Sync Architecture

- **Phase**: Frontend UI & User Experience
- **Lead / Owner**: Frontend Lead Architect
- **Complexity**: Very High
- **Prerequisites**: Task 02 (.NET API), Task 03 (SignalR Client), Task 08 (TypeGen Contracts)

#### 1. Detailed Scope & Feature Coverage
- Initialize React 19 + TypeScript + Vite frontend project with Tailwind CSS and Radix UI primitives.
- Implement local optimistic mutation queue using TanStack Query v5 + IndexedDB (Dexie.js).
- Build command palette modal (Kbar / cmdk) supporting global keyboard shortcuts (`Cmd+K`, `g p`, `c o`).
- Construct virtualized data table framework using TanStack Table v8 supporting 100,000+ client-side rows.
- Integrate SignalR binary MessagePack client for live optimistic state reconciliation.

#### 2. Key Deliverables & File Targets
- `src/Frontend/package.json` (React 19, TanStack Query, Dexie, Vite dependencies)
- `src/Frontend/src/lib/sync/mutationQueue.ts` (IndexedDB mutation persistence)
- `src/Frontend/src/components/ui/CommandPalette.tsx` (Keyboard command palette)
- `src/Frontend/src/components/grid/VirtualizedDataTable.tsx` (TanStack Table component)
- `src/Frontend/src/hooks/useSignalRStream.ts` (SignalR MessagePack hook)

#### 3. Architecture & Code Contract Blueprint
```typescript
// Optimistic Mutation Queue Pattern with Dexie.js
export async function enqueueMutation<T>(mutation: LocalMutation<T>): Promise<void> {
  await db.mutations.add({
    ...mutation,
    status: 'pending',
    createdAt: Date.now()
  });
  triggerSyncEngine();
}
```

#### 4. Independent Verification Criteria
- Execute `npm run build` inside `src/Frontend`; verify Vite bundles cleanly with zero TypeScript errors.
- Test optimistic UI: simulate a 3,000ms network latency delay; invoke "Create Order" command; verify table row renders instantly (<16ms) and sync status transitions from "pending" to "synced" upon server response.
- Execute virtualized table benchmark: load 50,000 rows into `VirtualizedDataTable.tsx`; verify 60fps smooth scrolling with zero DOM lag.

---

### Task 06: Plug-and-Play Custom Visualizations & Dynamic Dashboard Framework

- **Phase**: Visualizations & Analytics Engine
- **Lead / Owner**: Frontend Visualization Specialist
- **Complexity**: Medium
- **Prerequisites**: Task 04 (Semantic Layer), Task 05 (React UI Framework)

#### 1. Detailed Scope & Feature Coverage
- Build plug-and-play visual widget registry integrating Apache ECharts and Tremor UI components.
- Develop dynamic query builder UI allowing non-technical users to bind chart widgets to Semantic Layer metrics.
- Implement drag-and-drop customizable dashboard grid using React Grid Layout.
- Support real-time chart updating via direct subscription to SignalR streaming hooks.
- Add dashboard layout serialization to backend JSONB metadata store.

#### 2. Key Deliverables & File Targets
- `src/Frontend/src/components/visualizations/WidgetRegistry.ts` (Chart widget factory)
- `src/Frontend/src/components/visualizations/EChartsWrapper.tsx` (ECharts canvas component)
- `src/Frontend/src/components/dashboard/DashboardGrid.tsx` (React Grid Layout wrapper)
- `src/Frontend/src/components/visualizations/QueryBindingConfigurator.tsx` (Metric selector)

#### 3. Architecture & Code Contract Blueprint
```typescript
export interface ChartWidgetConfig {
  id: string;
  type: 'line' | 'candlestick' | 'bar' | 'donut';
  title: string;
  metricQuery: SemanticQueryRequest;
  refreshIntervalMs?: number;
  layout: { x: number; y: number; w: number; h: number };
}
```

#### 4. Independent Verification Criteria
- Render a live candlestick financial chart with 10,000 tick updates; verify smooth WebGL rendering without memory leakage or frame drops.
- Test dashboard persistence: modify widget positions via drag-and-drop; save layout; reload browser; verify exact widget positions restore from JSONB backend API.

---

### Task 07: Infrastructure as Code (IaC) Terraform Modules & Docker Setup

- **Phase**: Infrastructure & DevOps
- **Lead / Owner**: DevOps & Cloud Infrastructure Lead
- **Complexity**: Medium
- **Prerequisites**: Task 01 (Postgres/Timescale), Task 02 (.NET API)

#### 1. Detailed Scope & Feature Coverage
- Author HCL Terraform infrastructure modules for 3 production deployment tiers:
  - **Tier 1 (Lean/MVP)**: AWS App Runner / Container Apps + Serverless Postgres.
  - **Tier 2 (Growth)**: AWS ECS Fargate + RDS Aurora PostgreSQL + Managed NATS.
  - **Tier 3 (Scale)**: Multi-AZ EKS Kubernetes Cluster + Terraform Helm releases.
- Write multi-stage production `Dockerfile` (optimized Native AOT build + React static assets).
- Construct `docker-compose.yml` for zero-configuration local developer and AI agent environments.
- Create VS Code `.devcontainer/devcontainer.json` for reproducible containerized development.

#### 2. Key Deliverables & File Targets
- `infra/terraform/tier1_lean/main.tf` (Tier 1 Terraform specification)
- `infra/terraform/tier2_growth/main.tf` (Tier 2 Terraform specification)
- `infra/terraform/tier3_scale/main.tf` (Tier 3 Terraform specification)
- `Dockerfile` (Multi-stage container build)
- `docker-compose.yml` (Local developer container setup)
- `.devcontainer/devcontainer.json` (Devcontainer definition)

#### 3. Architecture & Code Contract Blueprint
```hcl
# Tier 2 ECS Fargate Task Definition Snippet
resource "aws_ecs_task_definition" "tradebook_api" {
  family                   = "tradebook-api"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = 512
  memory                   = 1024
  execution_role_arn       = aws_iam_role.ecs_execution.arn
  container_definitions    = jsonencode([...])
}
```

#### 4. Independent Verification Criteria
- Run `tflint` and `terraform validate` across `infra/terraform/tier1_lean`, `tier2_growth`, and `tier3_scale`; verify 0 validation errors.
- Execute `docker compose up --build -d` on a clean host; verify all containers (Postgres 17, NATS JetStream, API, Frontend) start and pass healthchecks within <30 seconds.

---

### Task 08: Agent-Readiness Framework, Automated TypeGen & Tooling

- **Phase**: Agent Ergonomics & Governance
- **Lead / Owner**: AI Developer Experience Lead
- **Complexity**: Medium
- **Prerequisites**: Task 02 (.NET Backend), Task 05 (React Frontend)

#### 1. Detailed Scope & Feature Coverage
- Write repository root `AGENTS.md` and module-level `AGENTS.md` specifying strict operational rules for AI agents.
- Setup `TypeGen` automated pipeline to generate TypeScript DTO contracts directly from C# domain models upon build.
- Implement `ArchUnitNET` unit tests in `Tests.Architecture` enforcing vertical slice isolation rules (e.g. `Features.Orders` cannot reference `Features.Portfolios`).
- Configure `Stryker.NET` mutation testing pipeline (`stryker-config.json`) with an enforced 80% mutation score threshold.
- Establish Semantic Release and Conventional Commits automated CI rules.

#### 2. Key Deliverables & File Targets
- `AGENTS.md` (Root AI agent governance file)
- `src/Backend/Tradebook.Api/AGENTS.md` (Backend slice rules)
- `typegen.json` (C# to TypeScript contract generator config)
- `tests/Tradebook.ArchitectureTests/SliceBoundaryTests.cs` (ArchUnitNET rules)
- `stryker-config.json` (Stryker mutation test configuration)

#### 3. Architecture & Code Contract Blueprint
```csharp
// ArchUnitNET Slice Isolation Boundary Rule
[Fact]
public void Slices_MustNotDependOn_OtherSlices()
{
    Types().That().ResideInNamespace("Tradebook.Features.Orders")
        .Should().NotDependOnAny(Types().That().ResideInNamespace("Tradebook.Features.Portfolios"))
        .Check(Architecture);
}
```

#### 4. Independent Verification Criteria
- Modify a C# DTO in `Tradebook.Contracts`; run `dotnet build`; verify updated TypeScript definitions auto-generate in `src/Frontend/src/types/generated/`.
- Intentionally introduce an invalid cross-slice namespace dependency; run `dotnet test tests/Tradebook.ArchitectureTests`; verify `ArchUnitNET` fails the build with an explicit violation message.
- Run `dotnet stryker` on `Tradebook.Domain`; verify mutation score report generates and meets the ≥80% threshold requirement.

---

### Task 09: Automated End-to-End (E2E) Testing Harness & NBomber Benchmarks

- **Phase**: QA & Performance Engineering
- **Lead / Owner**: Quality Assurance & Performance Lead
- **Complexity**: Medium
- **Prerequisites**: Task 03 (SignalR), Task 05 (React UI), Task 07 (Docker Environment)

#### 1. Detailed Scope & Feature Coverage
- Construct Playwright E2E browser test suite automating critical user journeys (login, portfolio creation, order execution, custom dashboard design).
- Build NBomber load and stress testing suite targeting API endpoints and SignalR WebSocket hubs.
- Integrate `Testcontainers.PostgreSql` and `Testcontainers.Nats` for hermetic C# integration testing.
- Formulate GitHub Actions continuous integration workflow (`.github/workflows/ci.yml`).

#### 2. Key Deliverables & File Targets
- `tests/e2e/playwright.config.ts` (Playwright configuration)
- `tests/e2e/specs/snappy-crud-mutations.spec.ts` (Optimistic UI test)
- `tests/performance/Tradebook.Benchmarks/LoadTestScenario.cs` (NBomber test scenario)
- `.github/workflows/ci.yml` (CI/CD pipeline workflow)

#### 3. Architecture & Code Contract Blueprint
```csharp
// NBomber Load Testing Scenario Snippet
var scenario = Scenario.Create("create_order_load_test", async context =>
{
    var response = await httpClient.PostAsJsonAsync("/api/v1/orders", newOrder);
    return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
})
.WithWarmUpDuration(TimeSpan.FromSeconds(5))
.WithLoadSimulations(Simulation.KeepConstant(copies: 50, during: TimeSpan.FromSeconds(30)));
```

#### 4. Independent Verification Criteria
- Execute `npx playwright test` inside `tests/e2e`; verify all browser user journeys pass green across Chrome, Firefox, and WebKit.
- Run NBomber performance load test: execute 35,000 req/sec benchmark; verify 99th percentile latency remains <50ms with 0% error rate.

---

### Task 10: Platform Integration, Master Documentation & Production Readiness Verification

- **Phase**: Master Integration & Documentation
- **Lead / Owner**: Principal Systems Architect
- **Complexity**: Medium
- **Prerequisites**: Tasks 01 through 09

#### 1. Detailed Scope & Feature Coverage
- Wire end-to-end communication channels between PostgreSQL 17, TimescaleDB, .NET 9 API, NATS JetStream, SignalR Core, dbt/Cube analytics layer, React 19 UI, and ECharts visualization dashboard.
- Create master task index document at `tasks/README.md`.
- Finalize system architecture blueprint at `architecture/master-architecture-blueprint.md` and repository `README.md`.
- Conduct full production readiness checklist review (security headers, secrets management, CORS, health checks).

#### 2. Key Deliverables & File Targets
- `tasks/README.md` (Master task index & execution status)
- `architecture/master-architecture-blueprint.md` (Consolidated master architecture specification)
- `README.md` (Root repository documentation & getting started guide)
- `reports/production-readiness-audit.md` (Final audit checklist)

#### 3. Architecture & Code Contract Blueprint
```markdown
# Tradebook Tasks Master Index

| Task ID | Specification File | Status | Verification Result |
| :--- | :--- | :--- | :--- |
| Task 01 | `tasks/task-01-database-and-timescaledb-setup.md` | Specified | Verified |
| Task 02 | `tasks/task-02-dotnet-backend-core.md` | Specified | Verified |
...
```

#### 4. Independent Verification Criteria
- Perform full repository audit: run `docker compose up`, execute `dotnet test`, execute `npx playwright test`; verify 100% test pass rate across all suites.
- Verify `tasks/README.md` and all 10 task specification files exist under `tasks/` with zero missing broken links.

---

## 5. Verification Matrix & Cross-Validation Protocol

To guarantee that the master task breakdown completely satisfies all system requirements from Iteration 1 through Iteration 3, the matrix below maps each requirement to its corresponding master task and verification method.

| Requirement ID | Requirement Description | Assigned Master Task | Primary Verification Method |
| :--- | :--- | :--- | :--- |
| **Iter 1 - R1** | Bi-Temporal Audit Trail & Versioning | **Task 01** | SQL trigger mutation execution & `TSTZRANGE` bounds check |
| **Iter 1 - R2** | Semantic Layer & Data Pipelines | **Task 04** | `dbt test` execution & dynamic C# query builder output test |
| **Iter 1 - R3** | High-Performance Snappy CRUD UI | **Task 05** | Playwright optimistic mutation latency test (<16ms UI render) |
| **Iter 1 - R4** | Dynamic Custom Visualizations | **Task 06** | ECharts WebGL frame rate benchmark under live stream |
| **Iter 2 - R1** | Adversarial Tech Stack Consolidation | **Task 01, Task 02, Task 03** | Architecture review & NATS + Postgres resource footprint check |
| **Iter 2 - R2** | Industry Learnings Implementation | **Task 03, Task 05** | Dexie sync queue test & SignalR outbox drain verification |
| **Iter 2 - R3** | Infrastructure Terraform & Costs | **Task 07** | `tflint` and `docker compose up` multi-container health check |
| **Iter 3 - R1** | Master Architecture Blueprint | **Task 10** | Document verification (`architecture/master-architecture-blueprint.md`) |
| **Iter 3 - R2** | Agent-Readiness & Governance | **Task 08** | ArchUnitNET boundary failure assertion & Stryker score ≥80% |
| **Iter 3 - R3** | Master Task Breakdown & Specs | **Task 10** | `tasks/README.md` and `tasks/task-01` to `task-10` completeness check |

---
*End of Task Decomposition Analysis Report.*
