# Task 24: Agent UI Guardrails & Visual QA

> **GREENFIELD MODERNIZATION TASK (2026-08-07)** — Encode design taste as deterministic constraints and feedback loops so that UI written by AI agents stays on-system without human hand-holding: token-lock lint, a curated component registry, a canonical `DESIGN.md`, visual-regression and accessibility CI gates, and a design-review agent. This task extends the Task 08 agent-readiness framework and is part of the Greenfield Modernization Program. See `docs/architecture/decision-log.md` (ADR-0023) for the governing decisions.

- **Phase**: Greenfield Modernization — Frontend Agent Enablement
- **Lead / Owner**: Frontend Platform / Design Systems
- **Complexity**: High
- **Prerequisites**: Task 08, Task 18, Task 19, Task 23 (relates Task 09)
- **Status**: Specified
- **Target Files**:
  - `src/Frontend/eslint.config.js` — token-lock + boundaries (extends Task 18)
  - `src/Frontend/registry.json`, `src/Frontend/components.json` — private shadcn registry + auth
  - `docs/design/DESIGN.md` — extend Task 23 with usage rules + hard rules
  - `docs/design/design-principles.md` — Linear-grade critique rubric
  - `src/Frontend/.storybook/main.ts`, `src/Frontend/.storybook/preview.ts`
  - `.github/workflows/visual-qa.yml` — Argos + `@axe-core/playwright` gate
  - `tests/e2e/visual.spec.ts`, `tests/e2e/a11y.spec.ts` (Task 09 harness)
  - `.claude/agents/design-review.md`, `.claude/commands/design-review.md`
  - `AGENTS.md`, `src/Frontend/AGENTS.md`, `GEMINI.md` — link DESIGN.md

---

## 1. Detailed Scope & Feature Coverage

### 1.1 Problem Statement

AI agents author the majority of `src/Frontend`. Tasks 18, 18, and 22 gave the repository a typed lint pipeline, a Base UI component library, and an OKLCH `@theme` token system with a canonical `DESIGN.md`. Those assets *describe* the design system, but description is not enforcement: an agent can read a doc sentence and still emit `p-[7px]`, a hand-rolled `role="button"`, or an off-token hex color. The finding driving this task is blunt — constraints beat instructions. An agent will ignore prose but cannot merge past a failing lint rule or a red screenshot. The last 5% of design drift (one-off arbitrary values, duplicated primitives, silent a11y regressions) compounds across hundreds of agent commits until the UI no longer reads as one system. Task 24 converts design taste into deterministic gates and tight feedback loops so on-system output is the path of least resistance and off-system output cannot merge.

### 1.2 Required Outcomes

- Every off-token value (arbitrary utilities, hardcoded colors) is a hard `eslint` error repo-wide, not a warning.
- Feature code cannot import a raw Base UI primitive; it must compose the registry component, enforced by `eslint-plugin-boundaries`.
- Agents discover and scaffold approved components through the shadcn MCP server backed by a private `@tradebook` registry.
- `docs/design/DESIGN.md` is loaded context — linked from root `AGENTS.md`, `src/Frontend/AGENTS.md`, and `GEMINI.md` — with token tables that carry usage rules and trading-specific hard rules.
- Visual regression (Argos) and accessibility (`@axe-core/playwright`) are REQUIRED, blocking GitHub checks on every pull request.
- An advisory `/design-review` agent screenshots the running UI across viewports and critiques it against `docs/design/design-principles.md`, upstream of the blocking gates.

### 1.3 In Scope

Adopt all five workstreams repo-wide, ordered by determinism:

1. **Token-lock the styling layer (hard block).** With Tailwind v4 `@theme` tokens as the only legal values, adopt **`eslint-plugin-tailwindcss` (v4)** as the primary — `no-arbitrary-value` and a `no-custom-classname` whitelist at `error` — chosen over `oxlint-tailwindcss` (documented fallback if lint wall-clock regresses) because Task 18 already runs ESLint 10 flat + typescript-eslint type-checked + `eslint-plugin-boundaries`: one pass, one config, one gate, co-located with boundaries. Force styling through **CVA / tailwind-variants** by linting `callees`. Forbid `@base-ui-components/*` imports in feature code outside `@/components/ui/*`, so hand-rolling an existing component is a lint error. Provide one escape hatch: a named `@utility`.
2. **A curated component registry the agent MUST compose.** Build a private shadcn registry (`registry.json`, `registry:base` items, `@tradebook` namespace, bearer auth) exposed via the shadcn MCP server; agents scaffold approved parts through `registryDependencies`. Ship well-named parts: `DataGrid`, `Combobox`, `Toolbar`.
3. **DESIGN.md as loaded context.** Extend `docs/design/DESIGN.md` with token tables carrying usage rules ("accent reserved for the single primary action"), motion/spacing/density scales, do/don't code pairs, and trading hard rules (row heights, `tabular-nums`, P/L semantics + colorblind pairing, no layout shift on live updates, keyboard-first `focus-visible`). Link it from root + `src/Frontend/AGENTS.md` + `GEMINI.md`.
4. **Visual-regression + a11y as a hard CI gate.** Stand up Storybook 10.5 with `@storybook/addon-a11y` + `storybook-addon-pseudo-states`; wire Argos (`@argos-ci/playwright` + `@argos-ci/storybook`) as a REQUIRED check the agent self-corrects against, plus `@axe-core/playwright` assertions in the Task 09 run. Mask dynamic regions and pin fonts to keep diffs deterministic. Do NOT use Lost Pixel (archived April 2026).
5. **A design-review agent loop (advisory).** Adopt the OneRedOak `design-review` subagent + `/design-review` command driven by Playwright MCP that screenshots across viewports and critiques against `docs/design/design-principles.md` (Linear-grade), with the Hallmark anti-slop `audit` as a secondary pass. Advisory only; workstreams 1 and 4 remain the merge blockers.

### 1.4 Out of Scope

- Authoring the tokens themselves or the component implementations — owned by Task 23 (tokens + DESIGN.md source of truth) and Task 19 (Base UI / shadcn library). Task 24 locks and gates them; it does not define them.
- Runtime LLM-generated UI. Explicitly rejected: output must remain committed, human-reviewable React. No generate-on-render, no server-side model calls that emit markup.
- New product features or screens (enablement infrastructure only), and replacing the Task 09 Playwright harness — the gates ride on it.

## 2. Key Deliverables & File Layout

```text
tradebook/
├── AGENTS.md · GEMINI.md · src/Frontend/AGENTS.md   # + link docs/design/DESIGN.md (Task 08)
├── playwright.config.ts                             # Task 09 — Argos reporter + font pinning
├── .github/workflows/visual-qa.yml                  # NEW — Storybook→Argos + axe (required checks)
├── docs/architecture/decision-log.md                # + ADR-0023
├── docs/design/DESIGN.md                            # EXTEND (Task 23) — usage/do-don't/hard rules
├── docs/design/design-principles.md                 # NEW — Linear-grade critique rubric
├── tests/e2e/{a11y.spec.ts,visual.spec.ts}          # NEW — @axe-core (0 violations) + @argos masks
├── .claude/agents/design-review.md                  # NEW — OneRedOak design-review subagent
├── .claude/commands/design-review.md                # NEW — /design-review (Playwright MCP)
└── src/Frontend/
    ├── eslint.config.js                             # EXTEND (Task 18) — token-lock + boundaries
    ├── components.json · registry.json              # NEW — private shadcn registry (@tradebook) + auth
    ├── .storybook/{main.ts,preview.ts}              # NEW — Storybook 10.5 + a11y + pseudo-states
    └── registry/tradebook/{data-grid,combobox,toolbar}/   # NEW — sources + *.stories.tsx
```

## 3. Architecture & Code Contract Blueprints

**Token-lock + boundaries** — `src/Frontend/eslint.config.js` (flat, extends the Task 18 type-checked pipeline):

```js
import tailwind from "eslint-plugin-tailwindcss";
import boundaries from "eslint-plugin-boundaries";

export default [
  // ...typescript-eslint type-checked config from Task 18 precedes this block...
  {
    files: ["src/**/*.{ts,tsx}"],
    plugins: { tailwindcss: tailwind, boundaries },
    settings: {
      tailwindcss: { config: "./src/app.css", callees: ["cva", "cx", "cn", "tv"] },
      "boundaries/elements": [
        { type: "ui-kit", pattern: "src/components/ui/*" },
        { type: "feature", pattern: "src/features/*" },
      ],
    },
    rules: {
      // Token-lock: Tailwind v4 @theme tokens are the only legal values.
      "tailwindcss/no-arbitrary-value": "error",   // bans p-[7px], bg-[#ff0000]
      "tailwindcss/no-custom-classname": ["error", { whitelist: ["u-density-override"] }],
      // Boundaries: raw Base UI primitives illegal outside the UI kit.
      "boundaries/external": ["error", {
        default: "allow",
        rules: [{ from: ["feature"], disallow: ["@base-ui-components/*"],
          message: "Compose @/components/ui/* — do not hand-roll a Base UI primitive the registry ships." }],
      }],
    },
  },
];
```

**Component registry** — a `src/Frontend/registry.json` item (namespaced, `registry:base` + `registryDependencies`):

```json
{
  "$schema": "https://ui.shadcn.com/schema/registry.json",
  "name": "@tradebook",
  "homepage": "https://ui.tradebook.internal/r",
  "items": [
    { "name": "toolbar", "type": "registry:base", "title": "Toolbar",
      "description": "Action bar over Base UI Toolbar; roving tabindex, focus-visible rings.",
      "dependencies": ["@base-ui-components/react"],
      "files": [{ "path": "registry/tradebook/toolbar/toolbar.tsx", "type": "registry:component" }] },
    { "name": "data-grid", "type": "registry:component", "title": "DataGrid",
      "description": "Virtualized grid: 28px rows, tabular-nums, P/L semantics w/ colorblind pairing, no layout shift on live updates.",
      "registryDependencies": ["@tradebook/toolbar", "@tradebook/combobox"],
      "dependencies": ["@tanstack/react-virtual"],
      "files": [
        { "path": "registry/tradebook/data-grid/data-grid.tsx", "type": "registry:component" },
        { "path": "registry/tradebook/data-grid/data-grid.stories.tsx", "type": "registry:file" }
      ] }
  ]
}
```

Serve the registry behind a bearer token; agents reach it through the shadcn MCP server in `.mcp.json`, with `components.json` `registries[]` supplying `{ "@tradebook": { "url": "...", "headers": { "Authorization": "Bearer ${REGISTRY_TOKEN}" } } }`.

**CI gate** — `.github/workflows/visual-qa.yml` (Argos + axe as required checks):

```yaml
name: Visual QA
on: { pull_request: { branches: [main] } }
jobs:
  storybook-argos:                                 # REQUIRED check
    runs-on: ubuntu-latest
    defaults: { run: { working-directory: src/Frontend } }
    steps:
      - uses: actions/checkout@v4
        with: { fetch-depth: 0 }                   # Argos needs history for baselines
      - uses: actions/setup-node@v4
        with: { node-version: 22, cache: npm }
      - run: npm ci
      - run: npm run build-storybook               # storybook-static/ (fonts pinned in preview.ts)
      - run: npx @argos-ci/cli upload storybook-static
        env: { ARGOS_TOKEN: ${{ secrets.ARGOS_TOKEN }} }
  e2e-a11y:                                        # Task 09: axe → 0 violations, @argos masks live data
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: 22, cache: npm }
      - run: npm ci && npx playwright install --with-deps chromium
      - run: npx playwright test --config playwright.config.ts
        env: { ARGOS_TOKEN: ${{ secrets.ARGOS_TOKEN }} }
```

Both jobs, plus the Task 18 lint job, are REQUIRED status checks in branch protection for `main`.

**DESIGN.md excerpt** — `docs/design/DESIGN.md` (tokens with usage rules + do/don't):

```md
## Color Tokens (OKLCH @theme)

| Token            | Value                  | Usage rule                                        |
| ---------------- | ---------------------- | ------------------------------------------------- |
| `--color-accent` | `oklch(0.62 0.19 27)`  | Reserved for the single primary action per view.  |
| `--color-profit` | `oklch(0.72 0.15 150)` | Positive P/L. Always paired with `▲` (colorblind).|
| `--color-loss`   | `oklch(0.63 0.20 27)`  | Negative P/L. Always paired with `▼`.             |

### Do / Don't (agent-facing)
❌ `<div className="p-[7px]">`            ✅ `<div className="p-2">`
❌ `<span style={{ color: "#e5484d" }}>`  ✅ `<span className="text-loss tabular-nums">-1,240.50 ▼</span>`
❌ `<div role="button" onClick={buy}>Buy</div>`   ✅ `<Button intent="primary" onClick={buy}>Buy</Button>`

### Hard Rules
- Default grid row height `--row-md` (28px); numeric columns use `tabular-nums`.
- Live updates reserve width and must not shift layout; P/L color is never the sole signal.
- Every interactive element shows a `focus-visible` ring; the UI is keyboard-first.
```

**Determinism ranking** (adopt in this order — most enforceable first):

| Rank | Mechanism                                    | Determinism   | Enforcement point        | Merge past it?   |
| ---- | -------------------------------------------- | ------------- | ------------------------ | ---------------- |
| 1    | Token-lock lint (arbitrary values, colors)   | Deterministic | pre-commit + CI (eslint) | No — red lint    |
| 2    | Boundaries rule (no raw Base UI in features) | Deterministic | pre-commit + CI (eslint) | No               |
| 3    | Visual regression (Argos required check)     | Deterministic | CI GitHub check          | No               |
| 4    | a11y assertions (`@axe-core/playwright`)     | Deterministic | CI (Playwright)          | No               |
| 5    | Component registry (shadcn MCP)              | Guiding       | authoring time           | Backstopped by 2 |
| 6    | Design-review agent (`/design-review`)       | Advisory      | pre-PR, human-in-loop    | Yes — advisory   |

**Reference docs**: `docs/architecture/decision-log.md` (ADR-0023) · `docs/design/DESIGN.md` · `docs/design/design-principles.md` · shadcn registry & MCP (`ui.shadcn.com/docs/registry`, `/docs/mcp`) · Argos (`argos-ci.com/docs/quickstart/playwright`, `/storybook`) · `@axe-core/playwright` (dequelabs/axe-core-npm) · `eslint-plugin-tailwindcss`, `eslint-plugin-boundaries` · Storybook `addon-a11y`, `storybook-addon-pseudo-states` · OneRedOak `design-review` · Base UI (`base-ui.com`).

## 4. Subagent Implementation Step-by-Step Workflow

1. Branch from `main` per Task 08 conventions; route every commit through `bin/agent-commit.sh` (conventional commits). No direct pushes to `main`.
2. Add `eslint-plugin-tailwindcss` to `src/Frontend/eslint.config.js`; point `settings.tailwindcss.config` at the Tailwind v4 `@theme` entry (`./src/app.css`), set `no-arbitrary-value` and `no-custom-classname` to `error`, and set `callees` to `["cva","cx","cn","tv"]`.
3. Register `boundaries/elements` for `components/ui/*` (ui-kit) and `features/*` (feature); add the `boundaries/external` rule disallowing `@base-ui-components/*` from `feature`, with a message pointing to `@/components/ui/*`.
4. Define the escape hatch: add the named `@utility u-density-override`, whitelist exactly that class, and document it in `DESIGN.md` as the only sanctioned arbitrary surface.
5. Author `src/Frontend/registry.json` under `@tradebook` with `registry:base` primitives and composed `DataGrid`, `Combobox`, `Toolbar`, wiring `registryDependencies`; run `npx shadcn@latest build`.
6. Configure registry bearer auth in `components.json` `registries[]` and register the shadcn MCP server in `.mcp.json` so agents enumerate and scaffold approved parts.
7. Extend `docs/design/DESIGN.md` with token tables + usage rules, the density scales, do/don't pairs, and trading hard rules; link it from root `AGENTS.md`, `src/Frontend/AGENTS.md`, and `GEMINI.md`.
8. Stand up Storybook 10.5 in `src/Frontend/.storybook/`; enable `@storybook/addon-a11y` and `storybook-addon-pseudo-states`; author a `*.stories.tsx` per registry component; pin fonts and disable animations in `preview.ts`.
9. Add `@argos-ci/storybook` upload of `storybook-static` plus `@argos-ci/playwright` screenshots in `tests/e2e/visual.spec.ts` (mask live price/timestamp regions), and `@axe-core/playwright` assertions in `tests/e2e/a11y.spec.ts` asserting zero violations across covered flows.
10. Add `.github/workflows/visual-qa.yml`; mark the Argos check, the a11y/e2e job, and the Task 18 lint job as REQUIRED status checks in branch protection for `main`.
11. Add `.claude/agents/design-review.md` and `.claude/commands/design-review.md` (OneRedOak) driven by Playwright MCP, and author `docs/design/design-principles.md` (Linear-grade). Keep the loop advisory.
12. Record ADR-0023 in `docs/architecture/decision-log.md`; update both `AGENTS.md` files with the new gates and the "compose, don't hand-roll" rule, then open the PR and confirm every required check is present and that seeded violations fail as designed.

## 5. Independent Verification & Acceptance Workflow

### 5.1 Verification Commands

```bash
# Token-lock + boundaries (Task 18 pipeline) — from src/Frontend
npm run lint                                     # 0 errors on a clean tree
printf 'export const X = () => <div className="p-[7px] bg-[#ff0000]" />;\n' > src/features/__fixtures__/drift.tsx
npm run lint || echo "EXPECTED: token-lock blocked arbitrary value + hex color"

# Registry discoverable via the shadcn MCP server
npx shadcn@latest build && npx shadcn@latest view @tradebook/data-grid

# Component workbench builds (a11y + pseudo-states addons load)
npm run build-storybook

# Task 09 harness with a11y + visual gates — from repo root
npx playwright install --with-deps chromium
npx playwright test                              # @axe-core → 0 violations; @argos → screenshots
npx @argos-ci/cli upload src/Frontend/storybook-static

# Advisory design-review loop (Playwright MCP)
claude /design-review --url http://localhost:5173
```

### 5.2 Acceptance Criteria

| ID      | Criterion                                                                               | Verification                             |
| ------- | --------------------------------------------------------------------------------------- | ---------------------------------------- |
| AGUI-01 | `bg-[#ff0000]` and `p-[7px]` fail `eslint` with token-lock errors                       | seeded fixture → `npm run lint` non-zero |
| AGUI-02 | Importing `@base-ui-components/react` inside `src/Frontend/features/**` fails boundaries | `npm run lint` non-zero                  |
| AGUI-03 | The single `@utility` (`u-density-override`) passes lint; no other custom class does     | lint green on hatch, red otherwise       |
| AGUI-04 | Arbitrary values inside `cva()` / `tv()` are caught via `callees`                       | seeded variant fixture fails             |
| AGUI-05 | shadcn MCP lists `@tradebook` items `DataGrid`, `Combobox`, `Toolbar`                    | MCP list / `shadcn view` returns them    |
| AGUI-06 | `npm run build-storybook` succeeds with a11y + pseudo-states addons loaded               | `storybook-static/` emitted              |
| AGUI-07 | Argos is a REQUIRED check and turns red on an intentional visual change                  | branch protection + seeded diff fails    |
| AGUI-08 | `@axe-core/playwright` asserts zero violations across covered flows                      | `npx playwright test` green              |
| AGUI-09 | Screenshots mask only live price/timestamp regions; fonts pinned (no flaky diffs)        | re-run yields identical baseline         |
| AGUI-10 | `/design-review` returns structured feedback scored against `design-principles.md`      | command output attached to PR            |

## 6. Anti-Cheating & Integrity Guardrails

1. Token-lock and visual-regression are BLOCKING. An agent cannot merge a PR while either is red; these are branch-protection-required checks, not advisory warnings.
2. Never disable, downgrade to `warn`, `eslint-disable`, or whitelist-expand the token-lint or a11y gate to make a PR pass. Widening `no-custom-classname` beyond the single documented `@utility` is a review-blocking change.
3. Never hand-roll a component that exists in the `@tradebook` registry. The boundaries rule backstops this; deleting or loosening it to import a raw Base UI primitive is prohibited.
4. Generated and registry output stays committed and human-reviewable React. No runtime LLM-generated markup and no build step that emits UI from a model at render time.
5. Mask only genuinely dynamic regions (live prices, timestamps, session IDs). Masking real, static UI to hide a legitimate visual diff defeats the gate and is prohibited.
6. Do not delete, `.skip`, or narrow Playwright/axe specs, viewports, or stories to dodge a failure or shrink the diff surface. Fix the UI.
7. The `/design-review` agent is advisory and never a substitute for the deterministic gates. A green review does not authorize merging past a red lint or a red screenshot.
8. Argos baselines are updated only by an intentional, reviewed change; never auto-approve a baseline to clear a check.
9. Every commit routes through `bin/agent-commit.sh` (conventional commits); no direct pushes to `main`.
