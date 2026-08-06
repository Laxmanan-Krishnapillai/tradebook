# Tradebook — Implementation Agent Kickoff Prompt

Copy everything below the line into a fresh agent session. Fill in `<TASK NN>`.

---

You are implementing the Tradebook platform — a .NET 9 + PostgreSQL 17 + React 19 monorepo for B2B gas/GoO/bioticket trading operations. The repository contains complete, reviewed specifications; no application code exists yet. Your job is to implement **one task at a time**, exactly as specified.

## Read these files first, in this order

1. `docs/architecture/decision-log.md` — 13+1 binding decisions (D1–D14). **This file wins every conflict** with any other document.
2. `docs/architecture/entity-model.md` — the authoritative domain model. Never invent tables, columns, or enum values that are not in it.
3. `docs/architecture/master-architecture-blueprint.md` — system design, DDL, and code contracts.
4. `docs/tasks/README.md` — task index, dependency order, and verification pipeline.
5. `docs/tasks/<your assigned task file>` — read it fully, including its DESCOPE NOTICE banner, before writing any code.

## Precedence when documents disagree

decision-log.md > a task file's DESCOPE NOTICE banner > that task file's body > blueprint > everything else. Files marked LEGACY (`docs/architecture/overview.md`, `folder-structure.md`, `testing-and-assumptions.md`) and the `docs/{research,review,alternatives,.bundle-staging}` trees are historical context — never implement from them.

Removed technology you must NOT introduce even if a stray mention survives somewhere: NATS/JetStream, TimescaleDB, DuckDB WASM / Arrow IPC, Dexie / offline mutation queue, `/api/v1/mutations/batch`, `perform3WayMerge`, Merkle trees / S3 WORM, Native AOT (`PublishAot`), Redis, ScyllaDB, NBomber. If you find yourself adding any of these, stop and re-read decision-log.md.

## Task order

01 → 02 → 03 and 08 (parallel) → 04 and 05 (parallel) → 06 and 07 (parallel) → 09 → 10. Implement only your assigned task. Its prerequisite tasks must already be merged; verify that before starting, and stop if they are not.

Your assigned task: **<TASK NN>**

## Repository layout (create as needed, D12)

`src/Backend/` (Tradebook.sln: Tradebook.Api, Tradebook.Core, Tradebook.Infrastructure), `src/Frontend/` (Vite + React 19), `src/Database/Migrations/` (numbered SQL), `infra/terraform/`, `infra/compose/`, `tests/` (Tradebook.UnitTests, Tradebook.IntegrationTests, Tradebook.ArchitectureTests, e2e, performance), root `docker-compose.yml` (postgres:17 + api only). If `.gitattributes` does not exist yet, add `* text=auto eol=lf` as your first commit before anything else.

## Non-negotiable invariants (from decision-log)

- **Auth (D11)**: JWT bearer on every endpoint; only `/health/live` and `/health/ready` are anonymous. Actor identity comes from the `sub` claim only — never from a request body.
- **SQL safety**: all values parameter-bound; every dynamic identifier (semantic-layer members, sort columns, granularity) validated against a compile-time whitelist; unknown members → HTTP 400, never silently dropped.
- **Concurrency (D5)**: every mutable entity has `version BIGINT`; updates run `WHERE id = $id AND version = $expected`; zero rows → HTTP 409 returning current server state.
- **Audit**: written by database triggers only, inside the same transaction; `outbox_events` insert in the same transaction with PascalCase `aggregate_type` from the whitelist in task-03.
- **Contracts (D13)**: C# DTOs are the single source of truth; TypeGen generates TS into `src/Frontend/src/api/generated/`; never hand-edit generated files.
- **Performance (D10)**: no absolute perf gates. Record baselines (k6, Playwright timings) into committed files; regression >20% fails.

## Working rules

- Run the task's verification commands **for real** and paste actual terminal output into your report. Never claim a check passed without executing it. No `|| true`, no swallowed errors, no skipped assertions.
- Tests run against real PostgreSQL 17 via Testcontainers or the root compose file — no mocked databases in integration tests.
- Commit in small, conventional-commit units (`feat(task-01): ...`). Do not amend or force-push.
- If the spec is ambiguous, contradicts itself, or a required contract is missing: **stop coding that part**, append a dated entry to `docs/architecture/spec-issues.md` describing the gap and your proposed resolution, implement the rest of the task, and flag the issue in your final report. Do not silently improvise contracts.
- When your task is complete and verified: update your task's Status cell in `docs/tasks/README.md` from `Specified` to `Implemented`, commit, and summarize what was built, what was verified (with output), and any spec issues filed.
