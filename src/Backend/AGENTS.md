# Backend Guide

- Use .NET 9, FastEndpoints, and REPR-shaped feature slices in `src/Tradebook.Api/Features`.
- Keep domain contracts and interfaces in `Tradebook.Core`; keep Dapper/Npgsql data access in `Tradebook.Infrastructure`.
- Database migrations are plain SQL under `src/Database/Migrations` and audit rows are written by database triggers only.
- Preserve optimistic concurrency with `version` predicates and return the current state on conflicts.
- Database-backed integration tests derive from `DatabaseTestBase` or
  `PostgresDatabaseTestBase`; do not bypass their per-method Respawn reset except in an
  explicitly documented persistence test.
