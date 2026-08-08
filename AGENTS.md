# Tradebook Agent Guide

## Binding rules

- Read `docs/architecture/decision-log.md` before task work; it overrides all other documents.
- The authoritative domain model is `docs/architecture/entity-model.md`. Do not invent schema or enum values.
- Every endpoint requires JWT authentication except `/health/live`, `/health/ready`, and `POST /api/v1/auth/login` (the sole anonymous API route). Derive the actor from the JWT `sub` claim only.
- Bind SQL values as parameters and whitelist every dynamic identifier.
- Do not edit `src/Frontend/src/api/generated/` by hand. Change C# DTOs and regenerate contracts.
- Integration tests use PostgreSQL 17 through Testcontainers; do not depend on a host database.
  Database-backed classes derive from `DatabaseTestBase` (API-host tests) or
  `PostgresDatabaseTestBase` (direct database tests) so Respawn clears application rows
  before every method while preserving `schema_migrations`.

## Commands

- `dotnet build src/Backend/Tradebook.sln -c Debug`
- `dotnet test tests/Tradebook.UnitTests/Tradebook.UnitTests.csproj`
- `dotnet test tests/Tradebook.IntegrationTests/Tradebook.IntegrationTests.csproj`
- `dotnet test tests/Tradebook.ArchitectureTests/Tradebook.ArchitectureTests.csproj`
- `dotnet typegen generate --project-folder .`
- `dotnet stryker --config-file stryker-config.json`
- `npm --prefix src/Frontend run lint`
- `bin/verify.sh` — runs ALL of the above gates in one shot (the definition of done).

Use `bin/agent-commit.sh <type> <scope> <summary>` for commits.

## Definition of done (every task)

Run `bin/verify.sh` and get exit 0 before declaring any task complete. It runs the same
gates as CI: `-warnaserror` build, unit + integration (Testcontainers) + architecture
tests, Stryker mutation (break=80), contract-drift check, and frontend lint/build/test.
"Done" means the gates are green **and** every acceptance-criterion ID in the task spec
maps to a passing test — end each task with that ID→test table. Never declare done on a
plan alone, and never fake, hardcode, or special-case a pass.

## Restraint (anti-slop)

Make the smallest change that satisfies the spec. Do not add a library, abstraction,
interface, config option, or layer unless a named acceptance criterion requires it; if
one does, cite it. Prefer the platform/stdlib and already-installed dependencies. No
speculative or adjacent features. Keep diffs scoped and reviewable.

## Test integrity

Changing tests to match intentionally changed behaviour is expected and allowed — when
you do, add a commit trailer `Test-Change: <what changed and why>`. Do NOT delete,
`[Skip]`, comment out, or weaken tests to get a green gate. `bin/check-test-integrity.sh`
flags unjustified test reductions and Stryker independently enforces strength.

## Reasoning effort, speed & execution plan

Default `high`. Use `ultra` (top level — max reasoning + subagent delegation) for the
genuinely decomposable tasks (14, 16, 17), `xhigh` for the deep focused one (12 Entra),
`medium` for mechanical/QA (09, 22, 10). Fast mode (`service_tier = "fast"`) is
quality-neutral (~1.5x speed, ~2.5x credits) — keep it on for interactive/daytime work,
turn it off for unattended overnight runs. `docs/codex/WAVES.md` holds the
dependency-ordered plan and per-task effort; `bin/codex-task.sh` runs one task supervised,
`bin/codex-overnight.sh` runs the sequence unattended.
