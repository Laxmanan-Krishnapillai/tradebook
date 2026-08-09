# Adversarial review findings — 2026-08-09

**To:** the agent implementing Task 17 (Wolverine messaging & durable outbox) in this worktree.
**From:** a parallel review session that audited main (cb08783) and this branch (codex/task-17) against `docs/tasks/*`, `docs/architecture/decision-log.md`, and the master blueprint, using six independent review agents.
**Do not commit this file.** It is a supervisor message, not repo content.

Your branch is up to date with main (merge-base = cb08783), and much of the hygiene work is verified good: `Infrastructure/Outbox/` fully deleted, no LISTEN/NOTIFY/SKIP LOCKED remnants, no EF Core/Marten in the package graph, MSG-07 boundary test present, hub wire names unchanged, migration 014's expand-phase design is sound. The findings below are what will fail your acceptance criteria or ship real bugs.

---

## Part 1 — Blocking defects on this branch

### 1. CRITICAL — Outbox flush happens BEFORE transaction commit in every converted repository (violates MSG-01 / D2 atomicity)

Every write slice currently does `EnlistAsync(tx)` → `PublishAsync(event)` → `publisher.FlushAsync()` → `tx.CommitAsync()`:

- `src/Backend/src/Tradebook.Infrastructure/Data/GooCertificateRepository.cs:142-143` (also 207-208, 264-265, 316-317)
- `MarketPriceRepository.cs:158-159, 206-207`
- `TransferRepository.cs:136-137, 201-202`
- `HedgeRepository.cs:120-121, 179-180`
- `DeliveryRepository.cs` (all three mutation paths)
- …and the remaining `Data/*Repository.cs` mutation paths (~10 files, systemic).

`FlushOutgoingMessagesAsync` releases envelopes to the durable local queue immediately. Wolverine's documented ADO.NET outbox pattern (and task-17 spec §3.2) is commit **then** flush. Consequences:

- If `CommitAsync` fails, the event is already dispatched: `EntityChangedRealtimeHandler` writes a `realtime_event_log` row and pushes SignalR for a rolled-back write — a permanent ghost event in catch-up. This is precisely what acceptance criterion **MSG-01** ("rolled-back command produces no outgoing message and no SignalR push") forbids.
- Even on success, the handler races the command commit: the browser's reconcile fetch (`useRealtimeQuerySync.ts:117-122`) can hit the API before the row is visible, get a 404, and remove a just-created entity from the cache.

Fix is a one-line swap per site (commit, then flush), but it must be applied to every converted repository.

### 2. CRITICAL — SaveDashboardEndpoint still uses the hand-rolled outbox; live dashboard push is dead in this binary

`src/Backend/src/Tradebook.Api/Features/Dashboards/SaveDashboardEndpoint.cs:35-39, 178-200` still does `INSERT INTO outbox_events ...` (`WriteOutboxAsync`) and never touches `ITransactionalEventPublisher`. This branch deletes the dispatcher, so nothing consumes `outbox_events` anymore. The row reaches `realtime_event_log` only via migration 014's compatibility trigger (`014_wolverine_realtime.sql:77-96`) — visible to catch-up, but **never pushed live** to `dashboard:{actorId}`. This regresses main's live dashboard fan-out and violates spec §1.3 ("convert every write slice") and MSG-04 / anti-cheat #1 ("no hand-rolled outbox remains anywhere"). The compat trigger makes this invisible to DB-polling tests — only a live-push assertion catches it.

### 3. MAJOR — Your MSG-01/MSG-06 tests bypass the production write path and hand-roll the *correct* ordering, masking finding 1

`tests/Tradebook.IntegrationTests/RealTime/WolverineMessagingTests.cs:606-669` (`ExecuteMarketPriceCommandAsync`) opens its own transaction and does commit (line 662) **then** flush (line 663) — the opposite order of every production repository — and inserts `market_prices` with raw SQL instead of calling any `Features/**` endpoint or repository. The rollback test at line 62 therefore passes while shipped code violates MSG-01. Rewrite these tests to drive the real endpoints/repositories; that is also what makes finding 1 regression-proof.

### 4. MAJOR — Messages do not carry Vogen value objects

`src/Backend/src/Tradebook.Core/Messaging/EntityChangedDomainEvent.cs:7-13` is `Guid EventId, string AggregateType, string AggregateId, ... string PayloadJson` — the old outbox row re-labeled as a domain event, with pre-serialized JSON built inside `Create` (lines 51-69). The spec requires typed per-aggregate events using Task-15 value objects (spec header "messages carry Vogen value objects"; §2 "domain-event records … using value objects"; §3.2 `DeliveryRecorded(delivery.Id, ...)`; §3.3 `DeliveryRealtimeHandler`). `EventId`, `DeliveryId`, `CompanyId` VOs exist in Core and are unused in messaging.

### 5. MAJOR — VogenMessagePackResolver silently dropped from SignalR MessagePack configuration

`src/Backend/src/Tradebook.Api/RealTime/SignalRRegistration.cs` on this branch registers bare `AddSignalR().AddMessagePackProtocol()`; main configures `CompositeResolver.Create(VogenMessagePackResolver.Instance, StandardResolver.Instance)`. `VogenMessagePackResolver.cs` is left as dead code. This reverts task-15/16 serializer wiring the spec says stays unchanged — and finding 4's required direction (Vogen types in hub messages) fails serialization without it.

### 6. MAJOR — Error policy dead-letters realtime events after ~400ms of DB trouble; non-Npgsql failures DLQ immediately

Branch `Program.cs`: only `NpgsqlException` gets `RetryWithCooldown(50ms, 100ms, 250ms)`; any other exception (SignalR send failure, `InvalidWorkspacePayload` thrown at `EntityChangedRealtimeHandler.cs:103` *before* the log insert) goes straight to the DLQ. A dead-lettered event never reaches `realtime_event_log`, so it is invisible to live push AND catch-up until manual DLQ replay — breaking D2's at-least-once guarantee that main preserved with unbounded retry. A DB failover longer than three short retries loses events from the realtime stream. Recommended: substantially longer/bounded backoff for transient classes, and ensure the `realtime_event_log` insert happens (or is retried) independently of the SignalR push so catch-up never loses an event that the push path failed on. (Main's opposite failure mode — infinite head-of-line retry on a poison row — is also wrong; land the middle ground: bounded retries + DLQ + event still recorded for catch-up.)

### 7. MINOR — §5.1 acceptance commands cannot run as written

Branch `Program.cs` ends with `await app.RunAsync()`, not the JasperFx command runner (`RunJasperFxCommandsAsync(args)`), so `dotnet run … -- resources check` boots the server and ignores the args. Also the §5.1 `rg "BackgroundService|…"` audit will false-positive on the pre-existing DbUp `MigrationHostedService.cs:16`.

### 8. MINOR — Debris / deviations to tidy before done

- Dead `"Outbox"` section survives in `src/Backend/src/Tradebook.Api/appsettings.json:4` after `OutboxOptions` was deleted.
- `IRealtimeEventReader` registered twice (branch `Program.cs` and `SignalRRegistration.AddDashboardPush`).
- `realtime_event_log` keeps a global bigserial + redundant `UNIQUE (group_name, sequence_id)` instead of spec §3.4's per-group sequence — wire-compatible, but document the deviation.
- `GetEventsSinceRequest` changed record→class and dropped `required` on `AfterSequence` — verify the contract-drift gate still passes (see also Part 3: the TypeSpec for this endpoint is already wrong on main).

---

## Part 2 — Pre-existing realtime defects on main you are inheriting (fix here if cheap, otherwise flag explicitly)

9. **Catch-up cursor can permanently skip events (main):** `PostgresOutboxEventReader.cs:33` filters `sequence_id > @afterSequence`, but bigserial allocation order ≠ commit order: if tx A takes seq 10, tx B takes seq 11 and commits first, a client that saw 11 and reconnects later never receives 10. Your `Sequential()` local queue incidentally serializes allocation and commit for `realtime_event_log`, fixing this for new events — worth stating explicitly in the PR, and worth a test.
10. **First page load replays the entire event history:** `signalRClient.ts:93-107` starts at `catchUp(0)` and pages ALL events ever written (500/page), including a per-event `GET /api/v1/deliveries/{id}` for stale delivery events. No retention job exists anywhere, and migration 014 copies full history into `realtime_event_log`, inheriting the problem. The cursor is memory-only (task-03 §4 says persist highest seen `sequenceId`). Unbounded cost growth.
11. **Live event failing Zod validation is silently swallowed:** `signalRClient.ts:87` — `zEntityChangedEventDto.parse` throws inside the SignalR `on` callback; @microsoft/signalr logs and drops it. No onError/invalidation fallback.

---

## Part 3 — Repo-wide findings outside Task 17's scope (for the supervisor; do NOT expand your diff to fix these)

Recorded here so they are not lost; each was verified with file:line evidence by a dedicated review agent.

**Contract-first (Task 16 / D20) — the two load-bearing promises are unimplemented:**
- The drift gate never compares the server-emitted OpenAPI document against TypeSpec. `scripts/check-contract-drift.sh` runs `tsp compile` + `scripts/compare-contract-dtos.py`, a regex comparison of property NAMES only (blind to routes, verbs, params, types, nullability, enums). Proof it leaks: `main.tsp:100` declares `after` for `/api/v1/events` while the server binds `AfterSequence` + `Limit` — shipped drift, CI green. The frontend only works because it hand-writes the true params.
- Nothing in `src/Frontend/src` imports the generated SDK/hooks/client; every fetch goes through hand-written `lib/api/client.ts` ending in `response.json() as Promise<T>` — the generated Zod validators are dead code, and runtime response validation exists at exactly one boundary (events catch-up).
- `main.tsp:23-26` still declares anonymous `POST /api/v1/auth/login`; no backend endpoint exists (404), contradicting Entra auth. Dead login stack (DTOs, `LoginMapper`, `users` table + `UserRepository` in DI) survives on the backend.
- Dashboards: TypeSpec binds routes to inline `DashboardPayload { version: int64 }` while `domain.tsp` declares the real DTOs with `version: int32` bound to no route; server uses `long`.
- Enums (`BookType` etc.) are declared in TypeSpec but referenced by no model — all enum fields are plain `string`; generated Zod enum validators are orphaned.
- Money-as-string holds on the wire today (empirically probed) but only via converter layering (`MoneyJsonConverter` at options level + Vogen delegation); zero test pins any money field's wire text, and the drift gate can't see types. One converter-registration line away from silently flipping to JSON numbers. Needs the CONTRACT-07 HTTP round-trip test.
- Related read-path bug: `Price`/`Amount` reject scale > 4 while columns are `NUMERIC(12,6)` — a 5-6 decimal stored value (writable by the Python importer or direct SQL) throws inside `MoneyTypeHandler.Parse` on read → HTTP 500.

**Data/migrations (Tasks 01/20):**
- sqlc adoption is a facade: exactly one 4-line probe query; all 10 repositories are hand-written Dapper outside the four allowed exceptions. `sqlc.yaml` types against migrations 001–003 only, so "schema drift becomes a build error" is false for most of the schema.
- `.squawk.toml` permanently excludes ~16 rules (all `ban-drop-*`, `adding-field-with-default`, …) with zero inline `-- squawk-ignore` justifications — the DDL-safety gate is neutered for all future migrations.
- Migrations 008 and 010 are stubs pointing at `docs/architecture/spec-issues.md` entries that do not exist; `custom_field_definitions` (Task 01 §3.9) and the TS-08 seed path were silently dropped.
- Startup crash-loop: on a reachable-but-unmigrated DB, `SemanticSchemaMismatchException` escapes `Program.cs:50-65`'s catch filter (which covers only `NpgsqlException`/`TimeoutException`) and kills the process before `MigrationHostedService` runs; migrations also run concurrently with traffic instead of before serving (task-20 §3.1).

**Auth (Task 12):**
- SignalR `?access_token=` values will be written to logs — the HUB-01 redaction requirement is implemented nowhere; no `Logging` config in production appsettings and OTLP `IncludeFormattedMessage = true`.
- `TestAuthenticationHandler` (no signature validation) is compiled into the production assembly, gated only by `ASPNETCORE_ENVIRONMENT=Testing`, with no AUTHN-03 test asserting it is inert elsewhere.
- The deployed container cannot serve the SPA: `FallbackPolicy` (authenticated + scope) applies to `MapFallbackToFile`, so anonymous requests for `/`, `index.html`, `/assets/*` get 401 — the hosted Microsoft login flow can never start, and the mandated `entra-authentication.spec.ts` does not exist.
- `AGENTS.md:9` still instructs "POST /api/v1/auth/login (the sole anonymous API route)" and "actor from `sub`" (it is now `oid`) — the binding agent rules contradict the shipped auth model.

**Verification honesty (D10) — three gates are theater:**
- `scripts/platform-verify.sh` has never been able to pass: step [7/8] requires compose services exactly `postgres` + `api`, but compose has no `api` service (and D9 says postgres only); `infra/validation/verify-tier1.ps1` enforces the opposite. Task 10 is marked "Implemented" on a gate that cannot go green.
- `tests/performance/baseline.json` is 100% `RECORD_ON_REFERENCE_MACHINE` placeholders and `ci-e2e-performance.yml:64-72` echo-skips the comparison — the D10 baseline-regression gate has never run.
- `visual-qa.yml` runs Playwright with no postgres/API/frontend booted and `webServer` disabled under CI — every spec fails on connection refused; it is not in the required-check list either. AGUI-07/08 unmet.

**Frontend (Tasks 19/23/24) — hollow acceptance pattern:**
- Base UI primitives (`ui/dialog`, `ui/select`, `ui/table`, `ui/skeleton`, `NumericCell`, …) have zero production imports — production dialogs/selects are hand-rolled without focus traps; `tabular-nums` never reaches a real numeric surface; density toggle affects only the unused `ui/table`; gates pass because the components' own files/tests contain the probed strings.
- The ESLint guardrail banning raw Base UI imports targets `src/features/**`, a directory that does not exist (real features are `src/components/*` typed `feature-*`); `tooling/ui-guardrails.mjs` certifies the rule against a temp fixture in the only path it covers.
- `xstate` was removed despite task-11 STATE-03 / task-12 STATE-01 forbidding exactly that, with no decision-log entry.
- Storybook/Argos covers only the 3 registry components; no product UI has a story.

**Doc integrity:**
- `docs/architecture/decision-log.md` has two `## D18` headings; task-12 cites "D15" which is actually the .NET 10/CPM decision — no Entra ADR exists in the authoritative log at all.
- `docs/tasks/README.md` statuses are wrong in both directions: merged tasks 12–24 still "Specified" while 09/10 — the two with demonstrably unmet gates — say "Implemented". It also still references the D10-banned ">60fps" gate, Tier 2/3 Terraform, and Native AOT.
- `docs/.bundle-staging/` (git-tracked) retains pre-descope "**Verified**" claims for AOT/WebGL/TimescaleDB features that never existed — D10 ordered these stripped.

**Verified clean (no action):** OCC/version-409 flow backend+frontend (uniform, genuinely tested end-to-end), semantic-compiler injection surface (whitelists + property tests), anonymous surface limited to the two health endpoints, actor identity from validated claims only, CPM/.NET 10/analyzers-as-errors/CSharpier, xUnit v3 on MTP with real CsCheck property tests, Terraform Tier-1/Azure with no cut-stack remnants, `bin/verify.sh` itself has no soft-fail paths, cut features (Dexie/NATS/Timescale/DuckDB/Merkle/WebGL pool) fully absent.
