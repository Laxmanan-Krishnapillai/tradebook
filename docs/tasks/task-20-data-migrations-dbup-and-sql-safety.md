# Task 20: Migration Runner (DbUp), Compile-Time SQL (sqlc) & Postgres Safety Gates

> **GREENFIELD MODERNIZATION TASK (2026-08-07)** — Replace the hand-rolled Dapper/Npgsql migration runner with DbUp (journaled, forward-only, ordered embedded `.sql`), adopt `sqlc-gen-csharp` for schema-verified, compile-time-typed SQL across all data access, and add Squawk + sqlfluff CI gates over every `.sql` file. Raw SQL, PL/pgSQL bi-temporal triggers, and Dapper/Npgsql execution stay authoritative. Record the decision and its fallbacks in `docs/architecture/decision-log.md`.

- **Phase**: Data Persistence Hardening
- **Area**: Database · Infrastructure · CI
- **Complexity**: High
- **Prerequisites**: Task 13 (coordinates Task 15 — value-object overrides)
- **Status**: Specified
- **Target Files**:
  - `src/Backend/src/Tradebook.Infrastructure/Migrations/MigrationRunner.cs` — DbUp runner (replaces the deleted hand-rolled runner)
  - `src/Database/Migrations/001_*.sql … 013_*.sql` — existing migrations, **kept** and still embedded
  - `sqlc.yaml` (repo root) — sqlc configuration
  - `src/Backend/src/Tradebook.Infrastructure/Data/queries/*.sql` — authored typed queries (schema source stays `src/Database/Migrations`)
  - `src/Backend/src/Tradebook.Infrastructure/Data/generated/*.cs` — generated typed C# (never hand-edited)
  - `.github/workflows/ci.yml` — Squawk + sqlfluff + sqlc no-drift gates
  - `.sqlfluff` (dialect `postgres`) and `.squawk.toml` — linter configs

---

## 1. Context

### 1.1 Problem Statement

Migrations run through a bespoke Dapper/Npgsql loop in `src/Backend/src/Tradebook.Infrastructure/` that reimplements ordering, idempotency, and journaling by hand — untested surface area with no standard journal and no per-script transaction guarantee. Application data access is raw SQL strings executed through Dapper; nothing verifies those strings against the deployed PostgreSQL 17 schema, so a renamed column or a wrong cast surfaces only at runtime. No gate inspects migration DDL, so a change can silently take an `ACCESS EXCLUSIVE` lock, build an index non-concurrently, rewrite a table on a type change, or add a foreign key without `NOT VALID`. SQL formatting drifts file to file, which makes machine-authored diffs hard to review.

### 1.2 Required Outcomes

- DbUp (`dbup-postgresql` 7.x) is the single migration runner: journaled to `schema_journal`, forward-only, applying the ordered embedded `001..013_*.sql` scripts; the hand-rolled runner is deleted and DbUp is wired into startup and the migration CLI.
- All application data access flows through `sqlc-gen-csharp` output — raw `.sql` parsed against the migration schema into compile-time-typed C# executed via Npgsql/Dapper — behind a `sqlc generate` + `git diff --exit-code` no-drift gate that compiles under `dotnet build`.
- Task 15 Vogen value objects (for example `TradeId`, `Money`) map through sqlc type overrides.
- Squawk gates every changed migration for lock- and rewrite-safety; sqlfluff (`postgres` dialect) lints every `.sql`; PL/pgSQL audit triggers writing `audit_log`, integer `version` concurrency, and `NUMERIC(18,8)`/`NUMERIC(18,4)` money stay intact.

### 1.3 In Scope

- Delete the hand-rolled runner; author the DbUp runner and wire it into startup and CLI.
- Keep and re-embed the existing `001..013_*.sql` migrations, PL/pgSQL triggers, and `audit_log` mechanics unchanged.
- Author the sqlc query set, generated data-access layer, `sqlc.yaml`, value-object overrides, and the CI no-drift gate.
- Add Squawk and sqlfluff CI gates and their configs; enable Dapper.AOT analyzers where hand-written Dapper remains; maintain the exhaustive list of hand-written-Dapper exceptions.

### 1.4 Out of Scope

- The Task 04 semantic query compiler and its identifier whitelist — unchanged; it keeps producing its dynamic, whitelisted SQL.
- EF Core and EF migrations — explicitly excluded, not introduced anywhere.
- Entity/domain model changes and schema redesign — money precision, integer versioning, and the bi-temporal audit design stay as-is.

## 2. Current State

The schema lives as numbered raw SQL in `src/Database/Migrations/001_*.sql … 013_*.sql`, embedded as assembly resources and applied by a hand-written Dapper/Npgsql runner in `src/Backend/src/Tradebook.Infrastructure/`. Bi-temporal audit is enforced by PL/pgSQL triggers that write to `audit_log`; optimistic concurrency is an integer `version` column checked on update. Injection safety for dynamically composed queries is handled by the Task 04 semantic query compiler's identifier whitelist, untouched here. Money is `NUMERIC(18,8)` and `NUMERIC(18,4)`. All data access is raw SQL through Dapper. Task 15 introduces Vogen value objects, which this task maps at the persistence boundary through sqlc overrides rather than by hand.

## 3. Technical Design

### 3.1 DbUp migration runner

Delete the bespoke runner and replace it with DbUp. Keep the `001..013_*.sql` files as embedded resources (the `.csproj` keeps its `<EmbeddedResource Include="..\..\..\Database\Migrations\*.sql" />`); DbUp applies them alphabetically, which the zero-padded numbering guarantees.

```csharp
using DbUp;
using DbUp.Engine;

namespace Tradebook.Infrastructure.Migrations;

// Journaled, forward-only application of the ordered embedded 001..013 scripts.
public static class MigrationRunner
{
    public static DatabaseUpgradeResult Run(string connectionString)
    {
        EnsureDatabase.For.PostgresqlDatabase(connectionString);
        var upgrader = DeployChanges.To.PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(MigrationRunner).Assembly,
                n => n.Contains(".Migrations.") && n.EndsWith(".sql"))
            .WithTransactionPerScript()                        // each migration commits atomically
            .JournalToPostgresqlTable("public", "schema_journal")
            .LogToConsole().Build();

        var result = upgrader.PerformUpgrade();
        if (!result.Successful) throw new MigrationException(result.Error);   // fail fast
        return result;
    }
}
```

Startup invokes `MigrationRunner.Run` before the host serves traffic; the CLI exposes the same call for out-of-band upgrades. Never edit an applied migration — add the next numbered file.

### 3.2 sqlc configuration

`sqlc.yaml` points the schema at the real migrations, so every query is typed against the deployed PG17 shape. The C# plugin is pinned by URL and digest.

```yaml
version: "2"
plugins:
  - name: csharp
    wasm:
      url: "https://downloads.sqlc.dev/plugin/sqlc-gen-csharp_0.x.wasm"
      sha256: "<pinned-digest>"                             # pin the 0.x plugin build
sql:
  - engine: "postgresql"
    schema: "src/Database/Migrations"                       # typed against the migrations = the real PG17 schema
    queries: "src/Backend/src/Tradebook.Infrastructure/Data/queries"
    codegen:
      - plugin: "csharp"
        out: "src/Backend/src/Tradebook.Infrastructure/Data/generated"
        options:
          driver: "Npgsql"                                  # execute over Npgsql/Dapper
          overrides:                                        # Task 15 Vogen value objects
            - { db_type: "uuid",    csharp_type: "Tradebook.Domain.ValueObjects.TradeId" }
            - { db_type: "numeric", csharp_type: "Tradebook.Domain.ValueObjects.Money" }
```

### 3.3 Example query and generated method

```sql
-- name: GetOpenPositionByTrade :one
SELECT position_id, trade_id, quantity, avg_price, version
FROM positions WHERE trade_id = @trade_id AND closed_at IS NULL;

-- name: UpdatePositionWithVersion :execrows
UPDATE positions
SET quantity = @quantity, avg_price = @avg_price, version = version + 1
WHERE position_id = @position_id AND version = @expected_version;   -- optimistic concurrency guard
```

sqlc generates a typed method `Task<GetOpenPositionByTradeRow?> GetOpenPositionByTrade(GetOpenPositionByTradeArgs args)` returning a record whose `TradeId` is the Vogen `TradeId` and `avg_price` is `Money`, executed over an `NpgsqlConnection`. The `:execrows` mutation returns the affected-row count, so the caller enforces optimistic concurrency directly — zero rows means a `version` mismatch. A column rename in a later migration makes `sqlc generate` fail: schema drift becomes a build error, not a production incident.

### 3.4 CI safety gates

```bash
# SQL format & lint (postgres dialect)
sqlfluff lint --dialect postgres src/Database/Migrations src/Backend/src/Tradebook.Infrastructure/Data/queries

# DDL safety gate over changed migrations only
git diff --name-only origin/main... -- 'src/Database/Migrations/*.sql' \
  | xargs --no-run-if-empty squawk --config .squawk.toml

# sqlc no-drift gate: regenerate, fail on any change, then compile
sqlc generate
git diff --exit-code -- src/Backend/src/Tradebook.Infrastructure/Data/generated
dotnet build src/Backend/Tradebook.sln -c Release
```

In `ci.yml` the DDL gate runs through `sbdchd/squawk-action` on the migration paths; sqlfluff and the sqlc no-drift step run as their own jobs. Enable Dapper.AOT analyzers on projects that keep hand-written Dapper for usage and batching correctness — Dapper.AOT's enhanced SQL-syntax analysis is SQL-Server-only, so it does **not** validate Postgres SQL; sqlfluff and Squawk own that.

### 3.5 Toolchain versions

| Component | Version (Aug 2026) | Role |
|---|---|---|
| PostgreSQL | 17 | Target database |
| DbUp (`dbup-postgresql`) | 7.x | Journaled, forward-only migration runner |
| Npgsql / Dapper | 9.x / 2.1.x | Driver + materialization for generated and exception queries |
| sqlc | 1.29.x | Query compiler (external Go toolchain) |
| sqlc-gen-csharp | 0.x | C# codegen plugin (WASM, pinned by digest) |
| Squawk / sqlfluff | 2.x / 3.x | Migration DDL-safety linter · SQL formatter (`postgres`) |
| Vogen (Task 15) | 6.x | Value objects mapped via sqlc overrides |

### 3.6 sqlc-gen-csharp maturity and fallback

`sqlc-gen-csharp` is a young, pre-1.0 (0.x) plugin and pulls the external Go `sqlc` toolchain plus a WASM plugin into the build. The repository adopts it repo-wide as the committed default for data access regardless of that maturity: the generated layer wraps Npgsql/Dapper, so its output is ordinary raw SQL executed through the driver already in use, and pinning the toolchain plus the plugin `sha256` behind the no-drift gate contains the 0.x surface. The fallback is narrow and stated — hand-written Dapper remains permissible **only** for constructs sqlc cannot yet express, and every such query is enumerated in §4.1. No query drops to hand-written Dapper for convenience.

**Reference docs**: `docs/architecture/decision-log.md` · DbUp <https://dbup.readthedocs.io/> · sqlc <https://docs.sqlc.dev/> · sqlc plugins <https://docs.sqlc.dev/en/latest/guides/plugins.html> · Squawk <https://squawkhq.com/> · sqlfluff <https://docs.sqlfluff.com/> · Dapper.AOT <https://github.com/DapperLib/DapperAOT>.

## 4. Coordination, Dependencies & Rollback

Task 13 provides the Infrastructure project layout and connection wiring the DbUp runner and generated layer build on. Task 15 owns the Vogen value objects; sequence the override block in `sqlc.yaml` so it lands with Task 15's merge, keep it in lockstep with the value objects Task 15 declares, and re-run the no-drift gate whenever either side changes. Migrations are forward-only, so recovery is a new numbered migration, never an edit to an applied one; reverting the PR restores the generated layer while applied migrations stay journaled in `schema_journal`.

### 4.1 Hand-written Dapper exceptions (exhaustive)

Hand-written Dapper is confined to these constructs, and this list is the whole set:

1. The Task 04 semantic query compiler's dynamically composed, identifier-whitelisted predicate SQL — out of scope and unchanged.
2. Bulk ingestion via Npgsql binary `COPY ... FROM STDIN`, which sqlc does not model.
3. Variadic-arity `IN (...)` filters whose parameter count is only known at runtime.
4. Administrative and maintenance one-offs in tooling (not application hot paths).

Adding any query outside this set requires expressing it in sqlc; extending the set updates this list in the same change.

## 5. Verification & Acceptance

### 5.1 Verification commands

```bash
dotnet test src/Backend/test/Tradebook.Infrastructure.Tests --filter Category=Migrations   # DbUp → Testcontainers PG17, journal populated
sqlc generate && git diff --exit-code -- 'src/Backend/src/Tradebook.Infrastructure/Data/generated'
dotnet build src/Backend/Tradebook.sln -c Release
squawk --config .squawk.toml src/Database/Migrations/0*.sql
sqlfluff lint --dialect postgres src/Database src/Backend
dotnet test src/Backend/test/Tradebook.Infrastructure.Tests --filter Category=Data           # exercise generated data access
```

### 5.2 Acceptance criteria

| ID | Criterion | Verified by |
|---|---|---|
| DATA-01 | DbUp applies `001..013` to a fresh Testcontainers PG17 with 0 errors and a populated `schema_journal` | Migrations test suite |
| DATA-02 | The hand-rolled runner is deleted; startup and CLI upgrade only through DbUp | Code search + startup smoke |
| DATA-03 | `sqlc generate` yields zero `git diff`; `dotnet build` compiles the generated code | CI no-drift gate |
| DATA-04 | Generated rows map Task 15 value objects (`TradeId`, `Money`) via overrides | Unit tests + build |
| DATA-05 | Squawk passes on all changed migrations; any exception justified inline | CI Squawk gate |
| DATA-06 | sqlfluff lint passes across every `.sql`; integration tests exercise generated data access | CI sqlfluff gate + data suite |
| DATA-07 | Every hand-written-Dapper query appears on the §4.1 exception list | Review checklist |

## 6. Guardrails

1. No hand-rolled migration runner remains; DbUp is the only path that applies schema.
2. Migrations stay forward-only, journaled, and ordered `001..013`; never edit an applied migration — add the next numbered file.
3. Every migration passes Squawk; any blocking finding is justified inline with a `-- squawk-ignore` comment stating the reason.
4. Generated sqlc code is never hand-edited; regenerate with `sqlc generate`, commit, and keep the `schema` pointed at `src/Database/Migrations`.
5. Raw SQL and PL/pgSQL bi-temporal triggers writing `audit_log` stay authoritative; DbUp and sqlc wrap them, they do not replace them.
6. Hand-written Dapper is allowed only for the constructs enumerated in §4.1; extending the set updates that list in the same change.
7. No EF Core and no EF migrations anywhere; the Task 04 identifier whitelist and semantic query compiler stay unchanged; money stays `NUMERIC(18,8)`/`NUMERIC(18,4)` and concurrency stays the integer `version` check.
