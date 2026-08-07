# Tradebook Master Implementation Task Breakdown Index & Roadmap

**Author**: Worker 3 (Master Task Breakdown Index Author)  
**Date**: August 5, 2026  
**Target File**: `c:\Users\LaxmananKrishnapilla\tradebook\tasks\README.md`  
**Status**: Authoritative Master Implementation Roadmap & Subagent Execution Specification  

---

> **DESCOPE NOTICE (2026-08-06)** — [`architecture/decision-log.md`](../architecture/decision-log.md) is now authoritative over this index. Removed from the stack: NATS JetStream, TimescaleDB, DuckDB WASM, Dexie offline queue + `/api/v1/mutations/batch`, S3 WORM/Merkle, Native AOT, WebGL pool/memory governor, infra tiers 2–3, and all absolute perf gates. Each task file carries its own notice; tasks 03 and 07 require full rewrites. The audit table is named `audit_log` (not `bi_temporal_audit_log`).

## Executive Overview

This master index defines the complete, end-to-end implementation roadmap for the **Tradebook** platform. It consolidates all technical, architectural, agent-readiness, and infrastructure requirements synthesized across **Iterations 1–3** into **10 granular, independently verifiable implementation tasks**. 

Every task is designed to be executed by specialized parallel subagents or engineering leads without ambiguity, adhering strictly to the **Pragmatic .NET 9 + PostgreSQL 17 + React 19** tech stack (2026-08-06 de-scope — TimescaleDB and NATS JetStream removed, see `architecture/decision-log.md`) defined in `architecture/master-architecture-blueprint.md` and `research/agent-readiness-framework.md`.

The **authoritative domain source of truth** is `architecture/entity-model.md` (v2.0, Excel-verified against the 5 Tradebook workbooks). The master blueprint §3 DDL is generated from it, and every task MUST consume its entities, enums, and business rules as its data contract.

---

## 1. Master Implementation Task Breakdown Index Table

The table below summarizes all 10 implementation tasks, detailing their primary domain, target specification document, entity/data-model scope, logical prerequisites, complexity, and current specification status. Entity references are bound to `architecture/entity-model.md` (v2.0).

| Task ID | Task Title | Primary Domain | Target Specification File | Entities / Data Model | Logical Prerequisites | Complexity | Status |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Task 01** | Core Database Architecture, Entity Model & TimescaleDB Bi-Temporal Audit Setup | Storage & Data Model | [`tasks/task-01-database-and-timescaledb-setup.md`](task-01-database-and-timescaledb-setup.md) | All entities: `contracts`, `certificate_contracts`, `counterparties`, `companies`, `trading_points`, `physical_deliveries`, `capacity_bookings`, `transfers`, `bioticket_deliveries`, `tax_tariffs`, `hedges`, `market_prices`, `capacity_price_indexes`, `goo_certificate_transactions`, `invoice_line_items`, `external_cogs` + all 18 enums | None | High | Implemented |
| **Task 02** | .NET 9 Modular Monolith Backend Core & Vertical Slice Framework | Application Core Engine | [`tasks/task-02-dotnet-backend-core.md`](task-02-dotnet-backend-core.md) | All entities — vertical slices: Contracts, PhysicalDeliveries, CapacityBookings, Transfers, Biotickets, GoOCertificates, MarketPrices, TaxTariffs | Task 01 | High | Implemented |
| **Task 03** | In-Process Event Distribution & SignalR Real-Time Push (rewritten 2026-08-06, D2) | Real-Time Messaging | [`tasks/task-03-signalr-realtime-and-nats.md`](task-03-signalr-realtime-and-nats.md) | `contracts`, `physical_deliveries`, `market_prices` (domain delta + price index events) | Task 01, Task 02 | High | Implemented |
| **Task 04** | Dynamic Semantic Query Layer (single C# AST→SQL path; dbt/Cube removed, D4) | Data Pipeline & Analytics | [`tasks/task-04-dynamic-semantic-layer-dbt.md`](task-04-dynamic-semantic-layer-dbt.md) | `physical_deliveries`, `market_prices`, `capacity_price_indexes`, `invoice_line_items`, `goo_certificate_transactions` | Task 01, Task 02 | High | Implemented |
| **Task 05** | React 19 Keyboard-First Snappy CRUD UI & TanStack Local Sync Architecture | Frontend UI & UX | [`tasks/task-05-react19-snappy-crud-ui.md`](task-05-react19-snappy-crud-ui.md) | All entities — CRUD grids: Contracts, Deliveries, CapacityBookings, Transfers, Biotickets, Certificates, Prices | Task 02, Task 03, Task 08 | Very High | Implemented |
| **Task 06** | Plug-and-Play Custom Visualizations & Dynamic Dashboard Framework | Visualizations & Analytics | [`tasks/task-06-custom-visualizations-framework.md`](task-06-custom-visualizations-framework.md) | `market_prices`, `capacity_price_indexes`, `physical_deliveries` (dashboard series) | Task 04, Task 05 | Medium | Specified |
| **Task 07** | Infrastructure as Code (IaC) Terraform Modules & Docker Setup | Infrastructure & DevOps | [`tasks/task-07-infrastructure-terraform-docker.md`](task-07-infrastructure-terraform-docker.md) | Full schema (plain Postgres 17; Azure Tier 1 — rewritten 2026-08-06, D9/D14) | Task 01, Task 02 | Medium | Implemented |
| **Task 08** | Agent-Readiness Framework, Automated TypeGen & Tooling | Agent Governance | [`tasks/task-08-agent-readiness-framework.md`](task-08-agent-readiness-framework.md) | All entities (TypeGen DTO generation + DB-first codegen) | Task 02 | Medium | Implemented |
| **Task 09** | Automated End-to-End (E2E) Testing Harness & Load Baselines (k6) | QA & Performance | [`tasks/task-09-e2e-testing-and-nbomber-harness.md`](task-09-e2e-testing-and-nbomber-harness.md) | `contracts`, `physical_deliveries`, `market_prices` (mutation + load fixtures) | Task 03, Task 05, Task 07 | Medium | Specified |
| **Task 10** | Platform Integration, Master Documentation & Production Readiness Verification | Master Integration | [`tasks/task-10-platform-integration-master-blueprint.md`](task-10-platform-integration-master-blueprint.md) | All entities (integration + reconciliation audit) | Tasks 01–09 | Medium | Specified |

---

## 2. Architectural Alignment & Prerequisites Graph

The execution order of tasks is governed by explicit architectural dependencies. Tasks at lower layers establish schemas, contracts, and core services required by upper-layer components.

```
+-------------------------------------------------------------------------------------------------------------------------+
|                                        ARCHITECTURAL DEPENDENCY & EXECUTION GRAPH                                       |
+-------------------------------------------------------------------------------------------------------------------------+
|                                                                                                                         |
|   PHASE 1: STORAGE & DATA FOUNDATION                                                                                    |
|   +-------------------------------------------------------------------+                                                 |
|   | Task 01: Core Database Architecture & Bi-Temporal Audit (PG17)    |                                                 |
|   +-------------------------------------------------------------------+                                                 |
|                                    │                                                                                    |
|                                    ▼                                                                                    |
|   PHASE 2: APPLICATION CORE & INFRASTRUCTURE                                                                            |
|   +-------------------------------------------------------------------+       +-------------------------------------+   |
|   | Task 02: .NET 9 Modular Monolith Backend Core & Slices            |──────►| Task 07: IaC Terraform & Docker     |   |
|   +-------------------------------------------------------------------+       +-------------------------------------+   |
|            │                                   │                                                                        |
|            ├───────────────────────────────────┼────────────────────────────────────┐                                   |
|            ▼                                   ▼                                    ▼                                   |
|   PHASE 3: MESSAGING, DATA PIPELINES & GOVERNANCE                                                                       |
|   +-----------------------------------+  +-----------------------------------+  +-----------------------------------+   |
|   | Task 03: SignalR + In-Proc Outbox |  | Task 04: Dynamic Semantic Layer   |  | Task 08: Agent-Readiness &        |   |
|   | Real-Time Engine                  |  | & C# AST-to-SQL Query Compiler    |  | TypeGen Contracts                 |   |
|   +-----------------------------------+  +-----------------------------------+  +-----------------------------------+   |
|            │                                   │                                    │                                   |
|            └───────────────────┬───────────────┼────────────────────────────────────┘                                   |
|                                v               │                                                                        |
|   PHASE 4: USER EXPERIENCE & VISUALIZATIONS    │                                                                        |
|   +-----------------------------------+        │                                                                        |
|   | Task 05: React 19 Snappy CRUD UI  |◄───────┘                                                                        |
|   | & Local-First TanStack Sync       |                                                                                     |
|   +-----------------------------------+                                                                                     |
|            │                                                                                                            |
|            ▼                                                                                                            |
|   +-----------------------------------+                                                                                     |
|   | Task 06: Custom Visualizations    |                                                                                     |
|   | & Dynamic Dashboard Framework     |                                                                                     |
|   +-----------------------------------+                                                                                     |
|            │                                                                                                            |
|            ▼                                                                                                            |
|   PHASE 5: QA, PERFORMANCE & INTEGRATION VERIFICATION                                                                   |
|   +-------------------------------------------------------------------+                                                 |
|   | Task 09: Automated E2E Playwright Harness & k6 Load Baselines     |                                                 |
|   +-------------------------------------------------------------------+                                                 |
|                                    │                                                                                    |
|                                    ▼                                                                                    |
|   +-------------------------------------------------------------------+                                                 |
|   | Task 10: Platform Integration, Master Specs & Final Verification  |                                                 |
|   +-------------------------------------------------------------------+                                                 |
|                                                                                                                         |
+-------------------------------------------------------------------------------------------------------------------------+
```

**Entity Model Source of Truth**: `architecture/entity-model.md` (v2.0, Excel-verified) is the mandatory domain input consumed by Task 01 (DDL §3) and reflected in every task's **Entities / Data Model** column above. No task may invent tables, columns, or enum values outside it.

---

## 3. Feature Inventory & Requirements Traceability Matrix

The traceability matrix below establishes explicit mapping between every requirement across **Iteration 1, Iteration 2, and Iteration 3** and its corresponding implementation task, key components, and verification method.

| Requirement ID | Source Phase & Requirement Description | Assigned Master Task(s) | Key Architectural Components & Contracts | Primary Verification Method |
| :--- | :--- | :--- | :--- | :--- |
| **Iter 1 - R1** | Bi-Temporal Audit Trails & Full Revertability | **Task 01** | `TSTZRANGE` valid/system time, PL/pgSQL triggers, `audit_log` | SQL automated trigger mutation test & range exclusion check |
| **Iter 1 - R2** | Semantic Data Modeling & Heterogeneous Data Pipelines | **Task 04** | Single C# AST→SQL semantic compiler with identifier whitelist (dbt & Timescale aggregates removed, D3/D4) | Dynamic SQL generation unit tests incl. injection suite |
| **Iter 1 - R3** | High-Performance Snappy CRUD UI/UX & Local Sync | **Task 05** | React 19, TanStack Query v5 optimistic mutations + version OCC (D5), TanStack Table v8, cmdk palette | Playwright optimistic UI mutation test (<16ms frame target) |
| **Iter 1 - R4** | Plug-and-Play Custom Visualizations Framework | **Task 06** | Apache ECharts canvas/WebGL, Tremor UI, React Grid Layout, metric query binding | Live tick WebGL frame rate benchmark (>60fps) |
| **Iter 2 - R1** | Adversarial Tech Stack Review & 90/10 Simplification | **Task 01, Task 02, Task 03** | Single PostgreSQL 17 primary storage, .NET 9 JIT monolith (D7), in-proc outbox dispatcher (D2) | Build verification & measured startup/memory baseline (no absolute gate, D10) |
| **Iter 2 - R2** | Real-World Industry Case Studies & Engineering Post-Mortems | **Task 03, Task 05** | Linear-style local sync queue, PostHog outbox worker resilience, Retool widget registry | Network latency disconnect/reconnect sync test |
| **Iter 2 - R3** | Infrastructure IaC Terraform Modules & Monthly Cost Scaling | **Task 07** | Terraform modules for Tier 1 (Lean), Tier 2 (Growth), Tier 3 (Scale); docker-compose dev setup | `tflint` validation & `docker compose up` multi-container health check |
| **Iter 3 - R1** | Master Architecture Blueprint Consolidation | **Task 10** | Single authoritative architecture document (`architecture/master-architecture-blueprint.md`) | Document completeness audit & cross-link validation |
| **Iter 3 - R2** | Agent-Readiness & Governance Framework | **Task 08** | Root `AGENTS.md`, `TypeGen` DTO generator, `ArchUnitNET` slice tests, Stryker mutation testing | ArchUnitNET boundary failure assertion & Stryker score ≥80% |
| **Iter 3 - R3** | Master Implementation Task Breakdown & Specifications | **Task 01 to Task 10** | Master task breakdown (`tasks/README.md`) and 10 detailed task specifications | Audit of all 10 task specification files in `tasks/` |
| **Iter 3 - R3.5** | Domain & Entity Model Alignment (Excel-Verified) | **Task 01** | Authoritative `architecture/entity-model.md` (v2.0), entity-aligned PostgreSQL 17 DDL in blueprint §3, contract naming convention (`BFEX45.BT.2301.CO2E-9-2023`) | DDL cross-check vs `entity-model.md` and source Excel workbooks (5 files) |

---

## 4. Task Execution Strategy & Parallel Subagent Guidelines

To maximize execution velocity while avoiding merge conflicts and state corruption, subagents must follow a strict **6-Wave Staged Execution Strategy**.

```
+-------------------------------------------------------------------------------------------------------------------------+
|                                              STAGED EXECUTION WAVE PLAN                                                 |
+-------------------------------------------------------------------------------------------------------------------------+
| WAVE 1 (Foundational Storage):                                                                                          |
|   └── Subagent Alpha: Task 01 (PostgreSQL 17 DDL, Bi-Temporal Triggers, Outbox NOTIFY + seq id)                         |
+-------------------------------------------------------------------------------------------------------------------------+
| WAVE 2 (Core Monolith & Infrastructure - Parallel):                                                                     |
|   ├── Subagent Beta:  Task 02 (.NET 9 Modular Monolith Solution, FastEndpoints, Native AOT)                             |
|   └── Subagent Gamma: Task 07 (Terraform HCL Modules for Tiers 1-3, Dockerfile, docker-compose)                         |
+-------------------------------------------------------------------------------------------------------------------------+
| WAVE 3 (Messaging, Analytics & Agent Governance - Parallel):                                                           |
|   ├── Subagent Delta: Task 03 (In-Proc Outbox Dispatcher, SignalR Typed Hub)                                            |
|   ├── Subagent Epsilon: Task 04 (Semantic YAML Models, C# AST Compiler, Injection Tests)                                |
|   └── Subagent Zeta:  Task 08 (AGENTS.md Rules, Automated TypeGen, ArchUnitNET, Stryker Setup)                          |
+-------------------------------------------------------------------------------------------------------------------------+
| WAVE 4 (Frontend UI & Visualizations):                                                                                  |
|   ├── Subagent Eta:   Task 05 (React 19 SPA, TanStack Query Optimistic OCC, Command Palette   )                        |
|   └── Subagent Theta: Task 06 (ECharts / Tremor Widget Registry, Dynamic Metric Builder)                                 |
+-------------------------------------------------------------------------------------------------------------------------+
| WAVE 5 (QA & Performance Engineering):                                                                                  |
|   └── Subagent Iota:  Task 09 (Playwright E2E Test Suite, k6 Baseline Harness, CI/CD Pipeline)                         |
+-------------------------------------------------------------------------------------------------------------------------+
| WAVE 6 (Master Platform Integration & Audit):                                                                           |
|   └── Subagent Kappa: Task 10 (End-to-End Integration, Master Documentation, Final Verification)                         |
+-------------------------------------------------------------------------------------------------------------------------+
```

### 4.1 Subagent Isolation & Operating Rules
1. **Directory Ownership**: Subagents must write exclusively within their designated task directory and target files. No subagent may modify another task's code or specification file without explicit coordination.
2. **Contract-First Development**: Subagents working on backend endpoints (Task 02) and frontend contracts (Task 05/08) must agree on DTO specifications prior to implementation. Generated TypeScript DTOs from `TypeGen` (Task 08) serve as the single source of truth.
3. **Zero-Hardcoding Mandate**: Subagents are strictly forbidden from hardcoding mock responses, fake test passes, or shortcut assertions. Every implementation must maintain real state and execute genuine logic.
4. **Hermetic State Verification**: Before declaring a wave complete, the subagent must execute the task's automated build and test commands, documenting exact terminal outputs in its execution handoff report.

---

## 5. Standard Structure for Task Specification Files

All 10 detailed task specification markdown files (`tasks/task-01-database-and-timescaledb-setup.md` through `tasks/task-10-platform-integration-master-blueprint.md`) follow a strict, standardized 6-part structure:

```markdown
# Task [XX]: [Task Title]

- **Phase**: [Phase Name]
- **Lead / Owner**: [Specialist Role]
- **Complexity**: [Low | Medium | High | Very High]
- **Prerequisites**: [Prerequisite Task IDs]
- **Target Files**: [List of primary files created/modified]

---

## 1. Detailed Scope & Feature Coverage
[Comprehensive bulleted breakdown of all functional requirements, features, and edge cases]

## 2. Key Deliverables & File Layout
[Exact file paths, directory structures, and code module organization]

## 3. Architecture & Code Contract Blueprints
[Verbatim code blocks, DDL schemas, C# models, TypeScript types, or HCL code snippets]

## 4. Subagent Implementation Step-by-Step Workflow
[Sequential step-by-step instructions for executing the task cleanly]

## 5. Independent Verification & Acceptance Workflow
[Exact terminal commands (build, test, lint, format) and quantitative acceptance criteria]

## 6. Anti-Cheating & Integrity Guardrails
[Explicit instructions detailing prohibited shortcuts, facade classes, and hardcoded test patterns]
```

---

## 6. Independent Verification & Acceptance Workflow

To guarantee system integrity, an independent **Teamwork Auditor** agent will verify every task upon completion. Acceptance requires passing a strict 4-stage verification workflow.

```
+-------------------------------------------------------------------------------------------------------------------------+
|                                          INDEPENDENT VERIFICATION WORKFLOW                                              |
+-------------------------------------------------------------------------------------------------------------------------+
|                                                                                                                         |
|  STAGE 1: Code Base Inspection & File Layout Audit                                                                      |
|  - Confirm all target files exist in their specified paths.                                                             |
|  - Verify absence of forbidden `.agents/` source code pollution.                                                        |
|                                                                                                                         |
|  STAGE 2: Genuine Implementation & Forensic Static Analysis                                                             |
|  - Inspect code for hardcoded string returns, dummy facade handlers, or ignored exceptions.                             |
|  - Run `ArchUnitNET` rules (Task 08) to assert zero vertical slice boundary violations.                                 |
|                                                                                                                         |
|  STAGE 3: Compilation & Automated Test Verification                                                                     |
|  - Backend: Run `dotnet build` and `dotnet test` with zero warnings/failures.                                           |
|  - Frontend: Run `npm run build` and `npm run test` with zero TypeScript errors.                                        |
|  - Database: Execute SQL migration scripts on fresh Postgres 17 container; verify zero syntax errors.                  |
|  - Docker: Execute `docker compose up --build -d` and verify healthy container status.                                  |
|                                                                                                                         |
|  STAGE 4: SLA & Benchmark Assertion                                                                                     |
|  - Execute k6 baseline run (Task 09): record throughput/p99 on the reference machine; fail on >20% regression (D10).                               |
|  - Execute Playwright E2E tests: assert 100% green UI mutation flows.                                                   |
|                                                                                                                         |
+-------------------------------------------------------------------------------------------------------------------------+
```

### Verification Command Matrix

Subagents and auditors must use the standard commands below to verify task implementation:

```bash
# 1. Core Database & Migrations (Task 01)
docker compose up -d postgres
psql -h localhost -U tradebook -d tradebook_dev -f src/Database/Migrations/001_initial_schema.sql

# 2. .NET 9 Backend Compilation & Slices (Task 02)
dotnet build src/Backend/Tradebook.sln -c Release
dotnet test tests/Tradebook.UnitTests/Tradebook.UnitTests.csproj

# 3. Messaging & Real-Time Hubs (Task 03)
dotnet test tests/Tradebook.IntegrationTests --filter Category=RealTime

# 4. Semantic Query Layer (Task 04)
dotnet test tests/Tradebook.UnitTests --filter Category=SemanticCompiler

# 5. React 19 Frontend & Local Sync (Task 05)
cd src/Frontend && npm run build && npm run test

# 6. Infrastructure Terraform (Task 07)
terraform -chdir=infra/terraform validate

# 7. Agent-Readiness & ArchUnitNET (Task 08)
dotnet test tests/Tradebook.ArchitectureTests/Tradebook.ArchitectureTests.csproj
dotnet stryker --config-file stryker-config.json

# 8. E2E Playwright & k6 Load Baseline (Task 09)
npx playwright test --config tests/e2e/playwright.config.ts
k6 run tests/performance/load-baseline.js --out json=tests/performance/baseline-run.json
```

---
*End of Master Task Breakdown Index & Roadmap Specification.*
