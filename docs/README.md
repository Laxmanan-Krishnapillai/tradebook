# Tradebook — Master Architecture Plan & System Specification

Welcome to **Tradebook**, a high-performance, real-time B2B financial operations, portfolio analytics, and workflow automation platform.

---

## 🚀 Iteration 3 Master Architecture Synthesis

Through three major design iterations, Tradebook evolved from a complex, hyper-sharded polyglot CQRS prototype (Iteration 1) to a streamlined, production-grade **Pragmatic .NET 9 + PostgreSQL 17 + React 19 SPA** platform (Iteration 3). 

Under our **Complexity Reduction Scoring (CRS) Model**, this consolidated stack achieves a **70.29% reduction in total operational complexity** while satisfying 100% of Tradebook's non-negotiable functional, latency, security, and financial auditability requirements.

The **authoritative domain source of truth** is the Excel-verified entity model:
📌 **[`architecture/entity-model.md`](architecture/entity-model.md)**

The single authoritative specification for the platform is documented at:
📌 **[`architecture/master-architecture-blueprint.md`](architecture/master-architecture-blueprint.md)**

The **2026-08-06 de-scope decisions** — authoritative over both documents above on any conflict — are recorded in:
📌 **[`architecture/decision-log.md`](architecture/decision-log.md)** (NATS, TimescaleDB, DuckDB WASM, offline queue, WORM/Merkle, Native AOT and absolute perf gates removed; monorepo strategy; contract ownership matrix)

---

## 🛠️ Core Technology Stack At A Glance

| Architectural Layer | Technology Selection | Key Architecture Highlights |
| :--- | :--- | :--- |
| **Backend Web API** | ASP.NET Core Web API (.NET 9, JIT) | FastEndpoints (REPR pattern), FluentValidation, long-running container (Native AOT deferred, D7). |
| **Primary Database** | PostgreSQL 17 | Relational core entities (`contracts`, `physical_deliveries`, `capacity_bookings`, `goo_certificate_transactions`), plain `market_prices` table (TimescaleDB deferred, D3), bi-temporal audit log (`TSTZRANGE` GIST exclusion constraints), transactional outbox (`outbox_events` + NOTIFY trigger), `version` column optimistic concurrency. |
| **Event Distribution** | Transactional Outbox + In-Proc Dispatcher | `BackgroundService` with `LISTEN/NOTIFY` wake-up, at-least-once delivery with client dedup (broker deferred until a second consumer exists, D2). |
| **Real-Time Push** | SignalR Core + MessagePack | Typed hub (`EntityChanged`), entity-type groups, reconnect catch-up via `GET /api/v1/events` cursor. |
| **Frontend Application**| React 19 SPA (Vite) | `@tanstack/react-router`, TanStack Query optimistic mutations + rollback, HTTP 409 conflict prompts (offline queue removed, D5). |
| **Analytics Query Path** | C# `SemanticQueryCompiler` | JSON AST → identifier whitelist → parameterized SQL on PostgreSQL 17 (DuckDB WASM deferred, D4). |
| **Custom Visualizations**| `ChartAdapter` contract | Apache ECharts (default) + TradingView Lightweight Charts behind one adapter interface; Tremor as KPI component kit; Web Worker LTTB downsampling (WebGL pool & memory governor removed, D8). |
| **Cold Audit Storage** | Versioned object storage | Nightly `pg_dump` to versioned Azure Blob storage, ≥7-year retention (WORM/Merkle deferred, D6/D14). |

---

## 📁 Repository Structure Map

```
c:\Users\LaxmananKrishnapilla\tradebook\
├── architecture/
│   ├── entity-model.md                   # 🌟 Authoritative Domain Entity Model (v2.0, Excel-verified source of truth)
│   ├── master-architecture-blueprint.md  # 🌟 Definitive Single Authoritative Architecture Blueprint
│   ├── overview.md                         # Legacy Iteration 1 system overview
│   ├── folder-structure.md                 # Project workspace & package organization
│   └── testing-and-assumptions.md          # Verification strategies & core assumptions
├── research/
│   ├── agent-readiness-framework.md        # AI Agent developer ergonomics, mutation testing & CI guardrails
│   ├── adversarial-tech-stack-review.md    # Adversarial complexity review & 70.29% CRS analysis
│   ├── industry-case-studies-and-learnings.md # Case studies (Linear, Twenty, PostHog, Supabase)
│   ├── infrastructure-terraform-and-cost-analysis.md # Terraform modules & multi-stage cost scaling
│   ├── versioning-and-audit-trails.md      # Bi-temporal audit trails & RFC 6962 Merkle trees
│   ├── semantic-modeling-and-data-sources.md# Dynamic YAML semantic models & DuckDB WASM acceleration
│   ├── snappy-crud-ui-ux.md               # Local-first UI, Dexie IndexedDB queue & dnd-kit scale translator
│   └── custom-visualizations.md           # 3-tier chart strategy & WebGL context pool management
├── review/
│   ├── README.md                           # Iteration 1 critique index
│   ├── access-control-and-data-model.md    # Security & RLS audit
│   ├── backend-and-jobs.md                 # Background jobs evaluation
│   ├── frontend-state-and-ui.md            # React state & canvas evaluation
│   ├── performance-and-scalability.md     # Performance benchmarks
│   ├── surrealdb-production-readiness.md  # SurrealDB operational risk report
│   └── action-items.md                     # Consolidated action items
├── alternatives/
│   ├── recommendation.md                   # Ranked architectural recommendations
│   └── local-first-sync-engines.md         # Local-first sync engine comparison
└── README.md                               # Project documentation index
```

---

## ⚡ Quick Start for Developers & Autonomous Agents

1. **Read Domain Model**: Start at [`architecture/entity-model.md`](architecture/entity-model.md), then the master spec at [`architecture/master-architecture-blueprint.md`](architecture/master-architecture-blueprint.md).
2. **Review Agent Guidelines**: Read [`research/agent-readiness-framework.md`](research/agent-readiness-framework.md) for Conventional Commits, Stryker mutation testing thresholds, and TypeGen contract generation.
3. **Database Setup**: Execute DDL in `architecture/master-architecture-blueprint.md §3` against PostgreSQL 17 with the `btree_gist` extension enabled.
4. **Backend Build**: Run `dotnet build` and `dotnet test` within the backend solution.
5. **Frontend Build**: Run `npm install` and `npm run build` within the React SPA application directory.
