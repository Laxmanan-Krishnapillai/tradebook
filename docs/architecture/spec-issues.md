# Specification Issues

## 2026-08-06 — Task 01 auxiliary schema conflicts with the entity model

**Gap:** Task 01 and the blueprint require `audit_log`, `outbox_events`,
`custom_field_definitions`, and `semantic_models`, while the authoritative entity model
states that no tables may be invented outside its listed domain entities. The first two
are additionally required by decision-log D2/D13, but the latter two are not covered by
the decision log.

**Proposed resolution:** Treat `audit_log` and `outbox_events` as cross-cutting platform
infrastructure explicitly authorized by the decision log. Add `custom_field_definitions`
and `semantic_models` to the entity model before implementing them, or explicitly
authorize them as platform tables. This task will implement only the two D2/D13-required
infrastructure tables and defer the ambiguous custom/semantic tables.

## 2026-08-06 — Task 01 workbook import inputs are absent

**Gap:** Task 01 requires a repeatable seed and Excel import pipeline for five named
workbooks, but none of those workbooks, sample rows, or mapping file exists in this
repository.

**Proposed resolution:** Supply sanitized workbook fixtures (or a complete mapping and
fixture dataset) under a versioned test-fixtures location. The schema migrations include
the required natural keys and intentionally do not seed invented business data.

## 2026-08-06 - Task 02 delivery contract and authentication dependency drift

**Gap:** Task 02's example delivery contract refers to `custom_fields`,
`price_eur_mwh`, and a `Deleted` delivery status. None exists in the authoritative
entity model or Task 01's `physical_deliveries` schema; the allowed
`report_status_enum` values do not include `Deleted`. The task also requires login
against a `users` table, but no such entity or table is authorized by the entity model
or supplied by Task 01.

**Proposed resolution:** Keep Task 02's implemented delivery surface limited to
authorized `physical_deliveries` columns. Treat a soft delete as the permitted
`Cancelled` status transition and retain a deletion reason only in the outbox event
payload (not as an invented column). Defer login until a user identity model/table and
password/role contract are explicitly added to the authoritative entity model; JWT
bearer validation and authorization policies can still be configured.

## 2026-08-06 - Task 02 additional vertical-slice contracts are incomplete

**Gap:** Task 02 requires implementations for Contracts, CapacityBookings, Transfers,
Biotickets, GoOCertificates, MarketPrices, and TaxTariffs, but provides only route
shapes. It does not define their request/response DTOs, validation requirements,
mutation fields, pagination/filter semantics, or outbox event types. The entity model
does not substitute for an HTTP contract.

**Proposed resolution:** Add a contract table equivalent to the physical-deliveries
slice for each route before implementation. This task implements the fully specified
physical-deliveries slice and foundational backend framework only.

## 2026-08-06 - Task 03 dispatcher dependency direction is undefined

**Gap:** Task 03 requires `OutboxDispatcher` to be implemented in
`Tradebook.Infrastructure`, but its mandatory constructor depends on
`IHubContext<DashboardPushHub, IDashboardPushClient>` from `Tradebook.Api`. The API
already references Infrastructure, so implementing that contract creates a circular
project reference. No Core-level fan-out port, event publisher interface, or alternate
assembly placement is specified.

**Proposed resolution:** Define a Core-owned `IOutboxEventFanout` port implemented by
the API using `IHubContext`, then inject that port into the Infrastructure dispatcher.
Alternatively, explicitly move the dispatcher into Tradebook.Api. Until one is chosen,
the dispatcher and its delivery/retry integration tests are deferred; the independent
SignalR hub, authentication, and cursor endpoint can be implemented safely.

## 2026-08-06 - Task 08 names a non-existent ArchUnitNET NuGet package

**Gap:** Task 08 specifies the package ID `ArchUnitNET.xUnit` at version `0.11.1`.
NuGet does not publish a package by that ID; restoring it fails with NU1101. The
published xUnit extension is `TngTech.ArchUnitNET.xUnit` and provides the intended
ArchUnitNET API.

## 2026-08-06 - Task 05 SignalR catch-up response shape conflicts with Task 03

**Gap:** Task 05's `DashboardStreamClient` blueprint treats `GET /api/v1/events` as an
array of events. Task 03 implements the authenticated endpoint as
`GetEventsSinceResponse { events, latestSequence }`.

**Proposed resolution:** Treat Task 03's implemented, typed response as the contract.
The frontend reads `events` for page processing and uses `latestSequence` only as
cursor metadata; it still pages until a short `events` page and deduplicates by
`eventId`.

**Proposed resolution:** Use `TngTech.ArchUnitNET.xUnit` version `0.11.1` in the
architecture-test project. Keep the prescribed three boundary rules and Task 08's
exclusive ownership unchanged.

## 2026-08-07 - Resolutions applied (Task 02 login, Task 03 dispatcher, Task 08 Stryker)

**Task 03 dispatcher dependency direction — resolved as proposed.** `IOutboxEventFanout`
is now a Core-owned port (`Tradebook.Core.Interfaces`), implemented by the API as
`DashboardPushFanout` over `IHubContext<DashboardPushHub, IDashboardPushClient>`, and
injected into the Infrastructure `OutboxDispatcher` hosted service. Dispatcher follows
the normative loop (LISTEN `outbox_new_event`, batch ≤ `Outbox:BatchSize` with
`FOR UPDATE SKIP LOCKED`, same-transaction `processed_at` mark, error backoff).
Integration tests T1–T5 and T7 exist in `RealTime/OutboxDispatchTests.cs` (T6 already
covered by `CatchUpEndpointTests`).

**Task 02 login deferral — superseded.** Task 02 §3.8 fully specifies the login
contract, so migration `011_users.sql` adds the `users` table as authentication
infrastructure (not a trading entity: no audit trigger, outbox, or version column).
`POST /api/v1/auth/login` verifies PBKDF2-SHA256 hashes (210,000 iterations, 16-byte
per-user salt, constant-time compare) and issues JWTs with `sub` = user id and role
claims. It is the sole anonymous API route besides the health probes.

**Task 08 Stryker config drift — fixed.** `stryker-config.json` mutate globs are
project-relative (`Features/**/*.cs`, …) per the canonical §5.5 config; repo-root
relative globs silently excluded every mutant. `thresholds.low` restored to the
specified 75. An endpoint/validator unit-test suite was added to kill the mutants the
config now exposes. Note: `GetEventsSinceEndpoint`'s SQL execution path (~11 mutants)
cannot be reached by the UnitTests-only Stryker run, capping the attainable score near
88% — still above the mandatory 80% break threshold.

## 2026-08-06 - Task 04 market-price semantic entity is unreachable

**Gap:** Task 04's mandated YAML includes `market_price` while its required
`physical_delivery` target has no declared join to `market_prices`. The same task
requires the loader to fail when any non-target entity is unreachable, so the supplied
model cannot pass its own startup validation.

**Proposed resolution:** Omit `market_price` from the initial delivery P&L model. Add
it back only with an entity-model-authorized structural join and matching dimensions or
measures; no join is invented here.

## 2026-08-06 - Task 04 startup schema validation conflicts with API-auth test setup

**Gap:** Task 04 requires the semantic loader to cross-check `information_schema` and
fail application startup on drift. The existing Task 02 authentication integration test
creates `WebApplicationFactory<Program>` without a PostgreSQL fixture, so an eager
database connection makes its unrelated health/auth checks fail before any request.

**Proposed resolution:** Keep `ValidateDatabaseSchemaAsync` available on the loader
and invoke it through a dedicated startup-verification host once every API test uses the
PostgreSQL fixture (or supply an explicit health-test configuration that disables only
the database cross-check). The loader still fail-fast validates every repo YAML rule at
construction; no database query is silently substituted.
