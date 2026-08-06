# Action Items - Summary Table

*Part of the [architecture review](README.md). Master checklist: every finding across this review, one row each.*

### 6.15 Summary of recommended changes

| # | Area | Current plan | Recommendation |
|---|------|--------------|-----------------|
| 1 | Auth/permissions | `$auth.*` with plain JWT access | Must use `TYPE RECORD` access — plain `TYPE JWT` bypasses `PERMISSIONS` entirely, `$auth.*` never populates from it |
| 2 | Data access | Browser connects directly to SurrealDB for reads + writes | **Resolved**: direct access kept for `SELECT`/`LIVE SELECT` only (default `PERMISSIONS NONE`, explicit `select` allowlist); all writes go through .NET. Full relay remains the fallback if this residual risk is unacceptable — costs ~low single-digit ms, not a performance blocker either way |
| 3 | Multi-tenancy | Shared table + `tenant` column | Evaluate Namespace/Database-per-tenant isolation |
| 4 | Session revocation | Implicit (relies on JWT expiry) | Short-lived access token + refresh token pair, explicit latency window |
| 5 | Background jobs | Hangfire, storage unspecified | Explicitly provision Postgres (or Redis) as a second datastore |
| 6 | Frontend data layer | No REST caching library | Add `@tanstack/react-query`; use its normalized cache + optimistic mutations as the primary "smoothness" lever, not network topology |
| 7 | Animation libraries | Used app-wide | Confine to marketing/onboarding, not canvas/editor |
| 8 | Canvas + DnD | "Integration rule" only | Plan for zoom-aware coordinate translation + scoped `DndContext` |
| 9 | SurrealDB adoption | Treated as settled | Explicit go/no-go after a backup/restore and patch-cadence spike |
| 10 | Live-query scaling | Unverified at any concurrency | Load-test real concurrent `LIVE SELECT` counts against expected subscription patterns before committing to a topology — known buffer/backpressure and aggregate-query-starvation issues exist regardless of direct vs. relayed |
| 11 | Optimistic writes | Not mentioned in the plan | Add explicit optimistic-update handling for user-initiated writes — the single biggest lever for perceived smoothness, independent of every other decision here |
| 12 | Schema/permission review gate | Not mentioned in the plan | Mandatory live-SurrealDB integration tests + required human diff review on any PR touching `PERMISSIONS`/schema files, treated as security code |
| 13 | Frontend/.NET type contract | Not mentioned in the plan | Generate an OpenAPI client for the `.NET` leg so drift fails the build; generate frontend types from SurrealDB schema introspection for the direct-DB leg |
| 14 | Rollout safety | No feature flags or staged rollout mentioned | Add feature flags so changes (agent- or human-authored) ship dark with staged rollout, not all-or-nothing |
| 15 | Post-merge safety net | No observability/APM plan | Add error-rate/anomaly monitoring (e.g. Sentry/OpenTelemetry) across `.NET` and SurrealDB as the catch for races/permission mistakes that pass review |
| 16 | Testing depth | Unit/integration/E2E only | Add contract testing (OpenAPI diff-gated), visual regression for canvas/kanban, mutation testing on validation/reconciliation logic, and soak-testing for concurrent live queries |
| 17 | Deployment/CI-CD | Not addressed | Add Docker Compose for local dev, health checks, and real secrets management (Key Vault/Doppler) — currently stops at localhost |
| 18 | Error contract | Implicit | Standardize on RFC 7807/9457 Problem Details (native to FastEndpoints) and define the frontend mapping to form field errors up front |
| 19 | Folder structure governance | No stated rules | Add explicit rules for `Core/` inclusion (3+ features) and `hooks/` placement before they rot |
| 20 | Auth/session lifecycle | Absent | Explicitly flag registration/password reset/MFA/session revocation as a scoped Phase 2 item, not a silent omission |
| 21 | Accessibility | Assumed covered by Radix | Add `@axe-core/playwright`; treat canvas keyboard-nav/screen-reader support as its own task, not inherited from ShadCN |
| 22 | Undo/redo | Not addressed | Add a command-pattern history stack; explicitly decide how undo interacts with already-round-tripped optimistic writes |
| 23 | Workflow versioning | Not addressed | Add an append-only version table with a published pointer; keep node/edge IDs stable across versions |
| 24 | Bundle size | Not addressed | Lazy-load canvas/kanban feature bundles via TanStack Router code-splitting |
| 25 | Live-query reconnection | Not addressed | Define an explicit re-sync strategy on WebSocket reconnect, not a bare resubscribe |
