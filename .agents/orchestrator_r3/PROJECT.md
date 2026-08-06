# Project: Tradebook Architectural Design & Engineering Iteration 3

## Architecture
- Stack: .NET 9 Web API + PostgreSQL 17 (with TimescaleDB / bi-temporal audit) + React 19 (TanStack DB/Query, AG Grid/Canvas, SignalR client) + Docker / Terraform
- Modules: Architecture Blueprint, Agent-Readiness Framework, Master Task Breakdown, Detailed Implementation Task Specs.

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | Master Architecture Blueprint | Definitive master architecture specification (`architecture/master-architecture-blueprint.md`) and updated `README.md` | M1 | ORIGINAL_REQUEST §R1 |
| 2 | Agent-Readiness Framework | Engineering framework for autonomous AI coding agents (`research/agent-readiness-framework.md`) | M2 | ORIGINAL_REQUEST §R2 |
| 3 | Master Task Breakdown | Overarching task breakdown index (`tasks/README.md`) | M3 | ORIGINAL_REQUEST §R3 |
| 4 | Detailed Task Implementation Specs | Dedicated task specification files (`tasks/task-*.md`) | M4 | ORIGINAL_REQUEST §R3 |
| 5 | Review & Audit Verification | Verification by Reviewer & Forensic Auditor | M5 | Integrity & Quality |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Master Architecture Blueprint & README | Synthesize prior research into master architecture blueprint and update root README.md | none | IN_PROGRESS |
| M2 | Agent-Readiness Research & Engineering Framework | Research & author agent-readiness framework document | M1 | PLANNED |
| M3 | Master Task Breakdown Index | Design and author tasks/README.md master task list | M1, M2 | PLANNED |
| M4 | Detailed Task Implementation Specifications | Parallel authoring of tasks/task-*.md for all tasks | M3 | PLANNED |
| M5 | Review & Forensic Audit Verification | Review and audit all generated deliverables | M1, M2, M3, M4 | PLANNED |

## Interface Contracts
- Document formats: Markdown (.md) with explicit section headers, diagrams (Mermaid/ASCII), DDL schemas, C#/TypeScript code blocks, CLI commands, test strategies.
- Document cross-references: All task specs link back to `architecture/master-architecture-blueprint.md`, `research/agent-readiness-framework.md`, and `tasks/README.md`.

## Code Layout
- `architecture/master-architecture-blueprint.md`
- `research/agent-readiness-framework.md`
- `tasks/README.md`
- `tasks/task-01-*.md`, `tasks/task-02-*.md`, etc.
- `README.md`
