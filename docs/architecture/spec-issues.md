# Specification issues

## 2026-08-07 â€” Task 05 catch-up response shape conflicts with Task 03

Task 05's catch-up blueprint treats `GET /api/v1/events` as an event array, while Task
03 returns `{ events, latestSequence }`. The frontend implements the Task 03 response
shape, processes `events` until a short page, and retains event-id deduplication.

## 2026-08-07 — Task 06 Tremor peer dependency contradicts React 19

Task 06 pins `@tremor/react@^3.18.0`, whose npm peer dependency accepts React 18 only, while the platform requires React 19. The installation therefore requires `--legacy-peer-deps`. Proposed resolution: either formally approve this verified peer override or replace Tremor with a React-19-compatible KPI component kit in a revised Task 06 specification.

## 2026-08-07 — Task 06 server-side dashboard validation has no backend contract

Task 06 requires a backend dashboard-save endpoint that validates `workspace_dashboards.layout_json`, but neither the Task 02 DTO/endpoints nor the current database model exposes a dashboard save contract. This frontend task cannot implement or test the required HTTP 400 behavior without inventing an endpoint. Proposed resolution: add the DTO, route, authorization policy, persistence mapping, and JSON-schema validation ownership to a backend task.

## 2026-08-07 - Task 09 depends on unavailable UI contracts and Task 06

Task 09 requires genuine browser assertions for a deliveries grid, delivery form,
optimistic-row marker, conflict prompt, and dashboard chart update. The committed
Task 05 frontend currently exposes only a heading and command palette; none of the
required delivery controls or test identifiers exist. The dashboard/chart assertions
also belong to Task 06, which is still marked `Specified` and is not listed as a
Task 09 prerequisite. Implementing the missing UI here would violate the one-task
scope and invent an inter-task ownership transfer. Proposed resolution: complete
Task 05's delivery UI and make Task 06 an explicit prerequisite before enabling the
Tier 1-4 browser mutation, conflict, realtime, virtualization, and chart specs.

Task 09 also has no provisioned test user, JWT, or contract fixture. Delivery writes
require a JWT and an existing `contracts.id`, and the schema has no seeded user or
contract. Its audit assertion additionally names `get_entity_state_as_of`, but Task
02 exposes no such endpoint and opaque-box browser tests cannot query PostgreSQL
directly. The baseline cannot be truthfully recorded until the reference machine and
those fixtures are supplied. Proposed resolution: provision dedicated test fixtures
and either add an authorized audit-read contract or assign the audit check to a
database integration test; then record and commit the initial baseline on that named
reference machine.

The prescribed Playwright configuration cannot start the frontend because
`src/Frontend/package.json` has no `dev` script, despite Task 09 requiring
`npm run dev --prefix ../../src/Frontend`. Proposed resolution: Task 05 should add
the Vite `dev` script and ensure the application exposes its E2E routes before Task
09 browser tests are enabled.

## 2026-08-07 - Task 06 dashboard JSON schema is internally inconsistent

`src/Frontend/src/types/dashboardSchema.json` requires widget fields `id`, `title`,
`semanticModelRef`, `queryAst`, and `visualEncodings`, but declares only `chartType`
under `properties` while setting `additionalProperties: false`. A strict Draft-07
validator therefore rejects every widget. The persisted-dashboard API uses the
complete `DashboardSpecification` shape in `visualizations.ts` as the strict
validation contract until the JSON schema declares its remaining fields.

## 2026-08-07 - Task 10 health authentication conflicts with binding repository rules

The Task 10 body and decision-log D11 say the health probes require JWT authentication,
while the binding repository instructions explicitly exempt `/health/live` and
`/health/ready` (and make `POST /api/v1/auth/login` the sole anonymous API route). The
existing integration contract also expects both health probes to be anonymous.
Proposed resolution: keep the two exact health routes anonymous, keep all business and
realtime endpoints authenticated, and make readiness perform a real PostgreSQL check.

## 2026-08-07 - Task 07 Azure backup policy reintroduces removed WORM behavior

`infra/terraform/storage.tf` provisions
`azurerm_storage_container_immutability_policy.backups` for 2,555 days. Azure immutable
blob retention is WORM behavior, which conflicts with decision-log D6's explicit removal
of WORM/object lock until compliance provides a written requirement. The storage account
also configures only 365 days of soft-delete retention, so removing the immutability
resource would leave the intended seven-year backup retention contract undefined.
Proposed resolution: remove the immutability resource and specify a non-WORM,
versioning-based seven-year lifecycle/retention mechanism before production deployment.
