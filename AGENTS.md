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

Use `bin/agent-commit.sh <type> <scope> <summary>` for commits.
