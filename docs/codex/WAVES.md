# Codex execution plan — Tasks 09–24 (dependency-ordered waves)

Derived from the **Logical Prerequisites** column in `docs/tasks/README.md`, with
Tasks 01–08 treated as already implemented. Parallelize *within* a wave; never start
a wave until the prior one is merged, so every downstream task branches from real,
updated code. For the **unattended overnight driver** (`bin/codex-overnight.sh`) the
DAG is linearized into a single order (see bottom) and run on an integration branch.

## Wave schedule

| Wave | Tasks (parallel within wave) | Unblocked once merged | Route | Effort | Notes |
|---|---|---|---|---|---|
| 0 | **13** Platform currency & Central Package Management (SOLO) | — | local | high | Touches every `.csproj`, `Directory.Packages.props`, `global.json` — highest conflict surface. Merge alone and first. |
| 0 | **11** Frontend routing/state hardening | 03,05,06 (done) | local/cloud | high | Frontend-only; disjoint from 13, safe in parallel. Unblocks 09, 12, 19. |
| 1 | **14** Backend analyzers & compile-time safety | 13 | local/cloud | ultra | Analyzers-as-errors, `[OptionsValidator]`, Mapperly. |
| 1 | **15** Strongly-typed primitives (Vogen) | 13 (coord 16) | local/cloud | high | Land before 16/20/22 consume the value objects. |
| 1 | **17** Wolverine messaging & durable outbox | 13 (**supersedes 03 outbox**) | local | ultra | Replaces existing outbox → regression risk; lean on integration tests. |
| 1 | **18** Frontend platform (React Compiler, Vite 8, Tailwind v4, ESLint 10) | 13 (start after 11) | local/cloud | high | Reworks frontend build/lint. Unblocks 19, 23. |
| 2 | **16** Contract-first API — TypeSpec/OpenAPI/TS client | 13 (**supersedes 08 TypeGen**; rel 15,17) | local/cloud | **ultra** | Very High. Money-as-string; SignalR payload validation. |
| 2 | **20** DbUp + sqlc + Squawk SQL safety | 13 (coord 15) | local/cloud | high | Migration-safety gates. |
| 2 | **12** Microsoft Entra ID auth | 02,03,07,**11** | local | **xhigh** | Very High. Fake JWT / test auth handler in tests — never real tenant secrets. |
| 2 | **09** E2E (Playwright) + k6 load harness | 03,05,07,**11** | local | medium | Needs Docker + running app + browsers. |
| 3 | **19** UI primitives on Base UI + schema-validated forms | **16,18** | local/cloud | high | Tremor removal; RHF + Zod boundary validation. |
| 3 | **21** .NET Aspire orchestration & observability | 13,**17** | local | high | Aspire AppHost needs a container runtime → local only. |
| 3 | **22** Test platform — xUnit v3 / MTP + CsCheck | 13 (coord 04,15,21) | local/cloud | medium | Run after 21 for coordination. |
| 4 | **23** Design system & motion foundation | **18,19** | local/cloud | high | OKLCH tokens, typography-for-data, motion vocabulary. |
| 5 | **24** Agent UI guardrails & visual QA | 08,18,19,**23** | local | high | Token-lock lint (cloud-safe) + Storybook/Argos visual regression + axe (needs browsers → local). |
| 6 | **10** Platform integration, master docs & production-readiness verification | 01–09,11 | local | medium | Formally unblocked after Wave 2, but **run last** as the whole-program verification gate. |

**Coordination clusters** (watch even across waves): type+contract `{15 → 16 → 17}`
(land 15 first; 16 consumes its types); frontend design `{18 → 19 → 23}` (strictly
sequential); SQL/type `{20 ↔ 15}`; test-platform `{22 ↔ 04,15,21}`.

**Cloud caveat (only if you ever delegate to Codex cloud):** the `codex-universal`
image has **no .NET SDK and no Docker**. Install .NET 10 in a setup script and use a
**native PostgreSQL** for tests; route Testcontainers/Aspire/Playwright tasks (09, 21,
parts of 24, integration tests) to the **local CLI**. Running locally overnight avoids
all of this — your own Docker/dotnet are already present.

## Speed & effort

Effort levels: `medium` (mechanical/QA — 09, 22, 10) · `high` (default) · `xhigh`
(deep focused — 12 Entra) · `ultra` (max reasoning + subagent delegation, ~6–12× tokens
— 14, 16, 17, the genuinely decomposable work). **Fast mode** (`service_tier = "fast"`)
is a separate axis: same intelligence, ~1.5× faster inference, ~2.5× credits — keep it on
for interactive/daytime runs (you're waiting), turn it off for unattended overnight runs
(you're not). Ultra + Fast on *every* task would drain your usage window fast, which is
why Ultra is scoped to three tasks, not all sixteen.

## Linearized order (used by bin/codex-overnight.sh)

```
13  11  14  15  17  18  16  20  12  09  19  21  22  23  24  10
```

Single-threaded and gated: each task runs on its own branch off the night's
integration branch, must pass `bin/verify.sh` + `bin/check-test-integrity.sh`, then
merges into the integration branch before the next task starts. On a red gate the
driver stops (default) and leaves the branch for review.
