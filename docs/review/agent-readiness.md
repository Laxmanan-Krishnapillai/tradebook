# Agent Readiness, Verification & Tooling

*Part of the [architecture review](README.md).*

### 6.12 Agent readiness, verification & human-on-the-loop development

The right frame is not "does a human review every change" (human-in-the-loop) but "does a human review after the fact, at scale, only where it matters" (human-on-the-loop) — which only works where a compiler or automated test reliably catches drift *before* merge. Layers with fast, deterministic, hard-fail feedback are safe for high AI-agent autonomy; layers with slow, external, or silent failure modes need mandatory human review regardless of how confident the agent making the change is.

**Verification-loop ranking for this stack, safest to riskiest:**

1. **FastEndpoints vertical slices** — safe, high autonomy. C# compiles, DI wiring fails loudly, unit/integration tests are deterministic and fast.
2. **Hangfire background jobs** — mostly safe, one carve-out. Same compiler/test net as #1, but changes to retry/idempotency logic need mandatory review, since failures surface hours later in production (duplicate side effects like a double charge or double email are hard to unwind after the fact).
3. **React/Zustand/XState UI logic** — safe-ish. TypeScript plus unit tests catch a lot; XState machines are good agent territory specifically because transitions are explicit and testable.
4. **TanStack Query cache reconciliation / live-query merge logic (§6.11)** — needs heavy review. Async, racy, no compiler signal. An agent can pass every test it wrote and still race in production, because the bug only exists in interleavings its own tests didn't happen to hit.
5. **SurrealQL PERMISSIONS/schema** — needs the heaviest review, mandatory gate. No compiler involvement at all, only a live integration test catches it if one exists, and failures are silent security holes, not build breaks. **The `$auth` vs. `$token` bug found in §6.1 is the canonical example**: only a live-DB test against the real JWT access type would have caught it, and an agent (or a human) could ship that mistake with every other check green.

**Type-safety and contract propagation — two disconnected contracts, not one.** The `.NET` leg can be made fully typed end-to-end via an OpenAPI-generated client (NSwag, Kiota, or a source generator), turning backend/frontend drift into a build failure — solved, agent-safe. The direct-DB leg is the opposite: hand-written SurrealQL/Surqlize queries against a schema that can drift with zero compiler involvement. Rename a field or reshape a permission clause, and the frontend query still runs syntactically fine while quietly returning wrong or missing data. **This is the single biggest agent-safety gap in the current plan.** Fix: generate frontend types from the SurrealDB schema via introspection, and treat any diff in that generated file as a required-review trigger, with the same status as an OpenAPI diff.

**Blast radius per layer, if an agent (or human) gets it subtly wrong:**

| Layer | Failure mode | Review requirement |
|---|---|---|
| FastEndpoints slice | Compile error or failing test; worst case a functional bug caught in staging | CI-gated autonomy is fine |
| SurrealQL PERMISSIONS/schema | **Silent** data leak or data loss, no compile error — may surface only when a customer notices | Mandatory human review, always, plus a mandatory live-DB integration test before merge |
| Frontend optimistic-merge logic | Silent UI bug (duplicate rows, stale data, flicker) — rarely a security issue but hard to reproduce and erosive to user trust | Recommended review; require the agent to state explicitly that it exercised the race condition, since automated coverage here is inherently weak |
| Hangfire job | Safe for pure logic changes | Mandatory review only for anything touching retries/idempotency keys |

**What's missing from the plan for a real human-on-the-loop workflow:**
- A mandatory live-SurrealDB integration test suite gating any PR touching schema or `PERMISSIONS` files, with required human diff review on those files treated as security code, not app code.
- OpenAPI client generation wired into CI so `.NET`/frontend drift fails the build outright (mirrored by schema-introspection-diff checks for the direct-DB leg, per above).
- Feature flags, so agent-shipped changes go out dark with staged rollout instead of all-or-nothing.
- A staging environment with synthetic data that exercises the live-query and optimistic-merge paths under load — the one area in this stack you cannot unit-test your way out of.
- Post-merge safety net: error-rate and anomaly monitoring (e.g. Sentry/OpenTelemetry across both `.NET` and SurrealDB) so races and permission mistakes that slip past review surface in minutes, not via a support ticket.

*Sources: [From Human-in-the-Loop to Human-on-the-Loop: Evolving AI Agent Autonomy](https://bytebridge.medium.com/from-human-in-the-loop-to-human-on-the-loop-evolving-ai-agent-autonomy-c0ae62c3bf91), [Human in the Loop vs On the Loop](https://www.braingrid.ai/blog/human-in-the-loop-vs-on-the-loop), [Human-in-the-Loop vs. Human-on-the-Loop – n8n Blog](https://blog.n8n.io/human-in-the-loop-vs-human-on-the-loop/), [Architecting for agentic AI development on AWS](https://aws.amazon.com/blogs/architecture/architecting-for-agentic-ai-development-on-aws/), [Agentic Coding Guide 2026](https://www.teamday.ai/blog/complete-guide-agentic-coding-2026)*

---

### 6.13 Agent-readiness tooling: concrete recommendations

§6.12 identified the risk ranking; this is the concrete tooling to act on it, prioritized.

**Adopt first, in this order:**

1. **ArchUnitNET for vertical-slice boundary tests.** NetArchTest is dead (no release since 2023); ArchUnitNET is the maintained option (a direct port of Java's ArchUnit). Concretely: a `Tests.Architecture` project asserting e.g. `Types().That().ResideInNamespace("Features.Orders").Should().NotDependOnAny(Types().That().ResideInNamespace("Features.Billing"))`, run on every PR — turning "an agent silently coupled two slices" into a build failure instead of something a reviewer has to spot. **Caveat**: it only catches structural coupling via namespace/assembly references, not runtime coupling through shared DB tables or SurrealDB records — it doesn't extend to the SurrealQL layer at all.
2. **Stryker.NET for mutation testing** on the C# slices (actively maintained, .NET 9 supported). Directly addresses "an agent writes a test that executes the code but asserts nothing" — a real, specific failure mode of LLM-authored tests. Run it scoped to changed projects (baseline/diff mode) rather than the whole solution so it stays fast enough to gate every PR, and gate on a per-project mutation-score threshold so one weak slice doesn't block unrelated work.
3. **Typed contracts using tools built for this exact stack, not generic ones.** For the `.NET` leg: `FastEndpoints.ClientGen.Kiota` or the NSwag-based `FastEndpoints.ClientGen` (both maintained by the FastEndpoints team itself) generating a spec fed into **Orval**, whose entire purpose is "OpenAPI in, typed TanStack Query hooks out" — this concretely closes the §6.12 drift gap for the `.NET` leg. For the SurrealDB leg: `surqlize` (§6.11) does zero-codegen type inference plus a typed query builder, **but it is explicitly labeled experimental by its own maintainers**, and even a mature version only type-checks query *shape* — it does not and cannot type-check `PERMISSIONS` logic. Typed contracts reduce shape drift on the SurrealDB leg; they do not reduce the permissions risk already flagged as the biggest one in §6.1/§6.12.
4. **`AGENTS.md` at the repo root.** This is now a genuine cross-tool standard (Claude Code, Codex CLI, Cursor, Aider, Devin, Copilot, Gemini CLI, and Windsurf all read it natively as of 2026), not a single-vendor convention. Put the actual enforceable rules in it: build/test commands, the slice-boundary rule ArchUnitNET now enforces, and explicitly — *"changes under `schema/` or anything touching `PERMISSIONS` require a live-DB integration test and mandatory human review; no autonomous merge."* Keep it under ~150 lines; longer files show measurably worse agent adherence and higher inference cost. Add a second, nested `AGENTS.md` inside the SurrealQL/schema directory itself restating just the permissions rule, since that's the one place an agent most needs to see it without having read the root file carefully.

**Adopt next, lower urgency:**
- **Verify** for approval-testing FastEndpoints response payloads and OpenAPI diffs — actively maintained, but note commercial/government users of the official binaries face a small subscription (from $10/mo) starting August 2026; source stays free as a fallback.
- **Pact** — skip for now. It protects a consumer/provider HTTP boundary, and there's currently one HTTP provider (the `.NET` backend) and no second service consuming it. Revisit only if Hangfire or another service gets split out as its own deployable.
- **Devcontainers + Docker Compose** — just do it, low-drama, no real tool choice to make. SurrealDB publishes an official `surrealdb/surrealdb:latest-dev` image and compose template; wrap Surreal + Postgres (for Hangfire, per §6.5) + the `.NET` SDK in one `devcontainer.json`. This is the precondition for an agent being able to self-verify anything at all.
- **OpenFeature + GrowthBook** for the §6.12/§6.14 feature-flag gap — OpenFeature gives a vendor-neutral flag API with both a `.NET` and a React SDK so neither codebase is locked to one vendor's shape; GrowthBook's self-hosted free tier includes full experimentation, not just flags. Unleash is the other reasonable pick if predictable per-seat self-host cost matters more than experimentation features.
- **Config validation** — no new library needed. `.NET` 9 already supports `AddOptionsWithValidateOnStart().ValidateDataAnnotations()`, which catches bad env vars at startup rather than first use. Just turn it on for every `Options` class.
- **OpenTelemetry** — SurrealDB 3.1 now honors W3C `traceparent`/`tracestate` across HTTP, RPC, and WebSocket, so a `.NET` `ActivitySource` span can carry through into the DB layer, and the OTel browser SDK's fetch/XHR auto-instrumentation carries the same header from React. A solid post-merge safety net once wired, but it only pays off after the CI gates above exist to catch problems pre-merge — hence last on this list.

*Sources: [ArchUnitNET](https://github.com/TNG/ArchUnitNET), [Stryker.NET](https://github.com/stryker-mutator/stryker-net), [FastEndpoints client generation](https://fast-endpoints.com/docs/swagger-support), [Orval](https://orval.dev/), [surqlize](https://github.com/surrealdb/surqlize), [AGENTS.md](https://agents.md/), [Verify](https://github.com/VerifyTests/Verify), [SurrealDB devcontainer image](https://docker.surrealdb.com/), [OpenFeature](https://openfeature.dev/), [GrowthBook](https://www.growthbook.io/)*
