# Engineering & Product Gaps

*Part of the [architecture review](README.md).*

### 6.14 Additional gaps: testing depth, deployment/CI-CD, and product-level omissions

Further sweep found real, non-redundant gaps beyond what §6.1-§6.12 cover:

**Testing depth.** Vitest/RTL/Playwright + xUnit (Section 5) baseline, not full strategy. Missing: **contract testing** between FastEndpoints and SPA (no shared schema today, DTO drift breaks silently at runtime — generate OpenAPI spec from FastEndpoints, consume via `openapi-typescript` or NSwag, gated on spec diffs in CI); **visual regression testing** for canvas/kanban UI (pixel-diffing via Playwright screenshot assertions or Chromatic — only way to catch React Flow layout/zoom regressions unit tests can't see); **mutation testing** (Stryker.NET / StrykerJS, both mature) run specifically against validation and cache-reconciliation logic, to verify test suite isn't hollow; and **load/soak testing for live-query subscriptions** (many concurrent WS connections held open for hours is real memory-growth/connection-exhaustion failure mode — k6/Artillery soak test opening N concurrent live queries while watching server memory over time currently absent).

**Deployment & CI/CD — genuine hole, not a nice-to-have.** No environment story (dev/staging/prod), no containerization, no secrets management for JWT signing keys/SurrealDB connection strings/CORS origins, no health checks. Add: Docker Compose file for local dev (SurrealDB + `.NET` + SPA), real orchestrator target for prod, ASP.NET Core `HealthChecks` (liveness plus SurrealDB-reachability readiness check), and proper secrets story (Key Vault/Doppler — not `appsettings.json` in git). Without this, architecture stops at localhost.

**Error contract.** FastEndpoints natively supports RFC 7807/9457 Problem Details (`ErrorOptions.ProblemDetailsConfig`), including mapping FluentValidation failures into `errors` dictionary. Standardize on this explicitly rather than leaving implicit, map field-keyed `errors` object onto React Hook Form's `setError` (or ShadCN form field state) by property path — write this down as contract now, before every feature slice invents its own error shape.

**Folder structure — three predictable failure modes.** `Core/` becomes dumping ground for anything two features share (rule of thumb: nothing enters `Core/` without being used by 3+ features, otherwise it's a `Contracts/`-namespaced shared-kernel module instead); feature slices duplicate validation/mapping logic over time; `hooks/` at frontend root becomes ungoverned catch-all (enforce that hooks used by only one feature live in `features/*/hooks`, root `hooks/` reserved for genuinely cross-cutting ones — needs lint rule or it will rot).

**Auth/session lifecycle beyond JWT issuance.** Registration, password reset, MFA, account lockout, session revocation UI absent. Reasonable to defer, but right now reads as oversight rather than decision — flag it explicitly as Phase 2 item.

**Accessibility.** Worth flagging specifically because Radix's built-in a11y creates false confidence: covers dialogs/menus/forms, not custom canvas. React Flow's node graph and dnd-kit's drag interactions need their own keyboard-nav and screen-reader affordances Radix can't provide for free. Add `@axe-core/playwright` to E2E suite, treat canvas keyboard navigation as distinct engineering task.

**Undo/redo.** Not addressed by XState/Zustand as configured — both state containers, not history managers. Needs command-pattern history stack (snapshot-based, e.g. Zustand history middleware, or explicit stack of inverse operations) with debounced snapshotting on drag-end rather than per-pixel, plus explicit decision on how undo interacts with in-flight optimistic writes (undoing a change already round-tripped to backend is distinct problem from undoing local-only state).

**Workflow versioning — real, unaddressed gap.** Nothing describes draft-vs-published state or version history for a workflow — every edit currently destructive to last known-good version. Needs explicit append-only version table with "published" pointer, and decision on whether canvas node/edge IDs stay stable across versions (they should, for diffing).

**Bundle size.** Real gap given "ultra-fast" goal and library count. TanStack Router supports route-based code splitting out of the box — canvas and kanban feature bundles should be explicitly lazy-loaded behind dynamic `import()` so initial shell doesn't pay for React Flow + dnd-kit + framer-motion on first paint.

**Observability & reconnection.** No OpenTelemetry tracing across SPA → `.NET` → SurrealDB, no correlation IDs, no frontend error tracking (Sentry) — compounding §6.12 post-merge-safety-net gap. Separately: no reconnection/resync strategy defined for live queries after WebSocket drop — events missed during a disconnect need explicit re-sync on reconnect, not just bare resubscribe.

*Sources: [FastEndpoints ProblemDetails config](https://api-ref.fast-endpoints.com/api/FastEndpoints.ErrorOptions.ProblemDetailsConfig.html), [Stryker.NET](https://github.com/stryker-mutator/stryker-net), [StrykerJS](https://github.com/stryker-mutator/stryker-js), [Playwright + axe-core accessibility testing](https://qaskills.sh/blog/playwright-accessibility-testing-axe-complete-guide)*
