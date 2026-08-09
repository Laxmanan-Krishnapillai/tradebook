# Task 18: Frontend Platform Upgrade — React Compiler, Vite 8, Tailwind v4 & ESLint 10 Type-Aware Linting

> **GREENFIELD MODERNIZATION TASK (2026-08-07)** — This task adopts **React Compiler 1.0 (GA)**, **Vite 8 (Rolldown default)**, **Tailwind v4 (CSS-first)**, **ESLint 10 flat config** with **typescript-eslint type-checked linting**, and **Knip** across the entire `src/Frontend` workspace. It is a committed, repo-wide adoption — every `src` file is compiled by React Compiler and type-checked-linted, with no per-file opt-in. The legacy `.eslintrc.cjs` is **deleted** (ESLint 10 removed the eslintrc format). Record the rationale and pinned version matrix under [`docs/architecture/decision-log.md`](../architecture/decision-log.md).

- **Phase**: Frontend Platform Hardening (lands after Task 16's generated client, before Task 19's UI/forms wave)
- **Lead / Owner**: Frontend Platform & Build Tooling Specialist
- **Complexity**: High
- **Prerequisites**: Task 13 (coordinates Task 16 generated client + Zod boundary; unblocks Task 19 UI/forms)
- **Status**: Specified
- **Target Files**:
  - `src/Frontend/vite.config.ts` (Vite 8 / Rolldown; `@vitejs/plugin-react` + `babel-plugin-react-compiler`; `@tailwindcss/vite`)
  - `src/Frontend/eslint.config.js` (**new** — flat config, replaces `.eslintrc.cjs`, which is **DELETED**)
  - `src/Frontend/package.json` (bumped/pinned deps; `engines`; lint/knip scripts; npm/pnpm lockfile)
  - `src/Frontend/tsconfig.json` (strict; consumed by ESLint `projectService`)
  - `src/Frontend/tailwind.config.ts` (**removed/shim**) / `src/Frontend/src/styles.css` (Tailwind v4 CSS-first `@theme` tokens)
  - `src/Frontend/knip.json` (**new** — unused files/exports/deps gate for CI)

---

## 1. Detailed Scope & Feature Coverage

### 1.1 Problem Statement

The `src/Frontend` workspace runs **React 19.0 on Vite 6.4.3 with TypeScript 5.8 (strict)**, but its quality gates trail the ecosystem. Linting still executes **ESLint 8.57.1** against a legacy `.eslintrc.cjs`; although `@typescript-eslint` 8.31 and `eslint-plugin-boundaries` 5.0.1 are installed, **no type-aware rules are enabled**, so floating promises, misused promises, and `any`-typed unsafe access pass silently — precisely the failures that erode the Zod validation boundary emitted by the generated client (Task 16). Components carry hand-written `useMemo`/`useCallback`/`React.memo`, Tailwind is imported (`tailwind-merge`, `clsx`) with no config or stylesheet pipeline, and there is no dead-code gate. This task closes every gap in one committed upgrade and raises the Node floor.

### 1.2 Required Outcomes

- **Adopt React Compiler 1.0** repo-wide via `babel-plugin-react-compiler` (pinned exact) inside `@vitejs/plugin-react`; strip the memoization the compiler makes redundant.
- **Upgrade Vite 6.4 → 8** with Rolldown as the default bundler; rename any `build.rollupOptions` to `build.rolldownOptions`; stay on npm/pnpm.
- **Adopt Tailwind v4** through `@tailwindcss/vite` with CSS-first config and design tokens declared in `styles.css`.
- **Migrate ESLint 8 → 10 flat config**: port every `eslint-plugin-boundaries` element/rule and enable `typescript-eslint` `recommendedTypeChecked` with `projectService: true` across **all** `src`, enforcing `no-floating-promises`, `no-misused-promises`, and `no-unsafe-*`.
- **Add** `eslint-plugin-react-hooks` (bundles the React Compiler rule), `@vitest/eslint-plugin`, `eslint-plugin-testing-library`, and `eslint-plugin-jsx-a11y`.
- **Wire Knip into CI**, taught about TanStack Router file routes, MSW handlers, and the Vite/Vitest configs.

### 1.3 In Scope

- Rewriting `vite.config.ts` for Vite 8 + React Compiler + Tailwind v4; authoring `eslint.config.js` and **deleting** `.eslintrc.cjs`; adding `knip.json`; pinning `package.json` deps; raising `engines` to **Node ≥20.19 / 22.13 / 24**.
- Authoring `src/styles.css` (`@import "tailwindcss"` + `@theme` tokens) and removing the JS Tailwind config runtime.
- Resolving every new type-aware finding across `src`, and adopting `@total-typescript/ts-reset` so `JSON.parse`/`res.json()` return `unknown`, reinforcing the Task 16/18 Zod boundary.

### 1.4 Out of Scope

- **UI component migration and forms** — owned by **Task 19**; this task changes the platform, not the components' markup.
- **The API codegen itself** (Hey API → types + Zod + TanStack Query hooks) — owned by **Task 16**; Task 18 only lints and bundles its output.
- Backend, database, or CI-runner provisioning beyond the frontend lint/build/knip steps; any visual redesign beyond wiring the token scaffold.

---

## 2. Key Deliverables & File Layout

```text
src/Frontend/
├── eslint.config.js            # NEW — flat config (typed tseslint + boundaries + react-hooks/a11y/vitest/testing-library)
├── knip.json                   # NEW — unused file/export/dep gate for CI
├── vite.config.ts              # Vite 8 (Rolldown) + React Compiler babel plugin + Tailwind v4 plugin
├── package.json                # pinned deps; engines Node ≥20.19/22.13/24; lint + knip scripts
├── tsconfig.json               # strict; discovered by ESLint projectService
├── tailwind.config.ts          # REMOVED — Tailwind v4 is CSS-first (see src/styles.css)
├── .eslintrc.cjs               # DELETED (ESLint 10 removed the eslintrc format)
└── src/
    ├── styles.css              # @import "tailwindcss"; @theme design tokens
    ├── main.tsx                # imports ./styles.css; @total-typescript/ts-reset side-effect import
    ├── api/generated/          # Task 16 Hey API client + routeTree.gen.ts — ignored by ESLint + Knip
    └── app/ routes/ features/ components/ lib/   # eslint-plugin-boundaries layers
```

---

## 3. Architecture & Code Contract Blueprints

### 3.1 Vite 8 + React Compiler (`vite.config.ts`)

```ts
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { tanstackRouter } from '@tanstack/router-plugin/vite'

const reactCompilerConfig = { target: '19' } // React Compiler 1.0 (GA), pinned exact in package.json

export default defineConfig({
  plugins: [
    tanstackRouter({ target: 'react', autoCodeSplitting: true }),
    react({ babel: { plugins: [['babel-plugin-react-compiler', reactCompilerConfig]] } }),
    tailwindcss(),          // Tailwind v4 — CSS-first, no JS config runtime
  ],
  // Vite 8 ships Rolldown as the default bundler: rollupOptions → rolldownOptions.
  build: { rolldownOptions: { output: { /* explicit chunking overrides only */ } } },
})
```

### 3.2 ESLint 10 Flat Config (`eslint.config.js`) — Type-Checked + Boundaries Ported

```js
// @ts-check
import js from '@eslint/js'
import tseslint from 'typescript-eslint'
import reactHooks from 'eslint-plugin-react-hooks'
import jsxA11y from 'eslint-plugin-jsx-a11y'
import boundaries from 'eslint-plugin-boundaries'
import vitest from '@vitest/eslint-plugin'
import testingLibrary from 'eslint-plugin-testing-library'
import globals from 'globals'

export default tseslint.config(   // typescript-eslint flat config helper
  { ignores: ['dist', 'src/api/generated/**', 'src/routeTree.gen.ts'] }, // generated: bundled, never linted
  js.configs.recommended,
  ...tseslint.configs.recommendedTypeChecked,                            // type-aware across ALL src
  {
    languageOptions: {
      parserOptions: { projectService: true, tsconfigRootDir: import.meta.dirname },
      globals: globals.browser,
    },
    plugins: { boundaries, 'react-hooks': reactHooks, 'jsx-a11y': jsxA11y },
    settings: {
      'boundaries/elements': [
        { type: 'app', pattern: 'src/app/**' }, { type: 'features', pattern: 'src/features/*/**' },
        { type: 'components', pattern: 'src/components/**' }, { type: 'api', pattern: 'src/api/**' },
        { type: 'lib', pattern: 'src/lib/**' },
      ],
      'import/resolver': { typescript: { alwaysTryTypes: true } },
    },
    rules: {
      ...reactHooks.configs.recommended.rules,          // bundles the React Compiler rule
      'react-hooks/react-compiler': 'error',
      ...jsxA11y.flatConfigs.recommended.rules,
      '@typescript-eslint/no-floating-promises': 'error',
      '@typescript-eslint/no-misused-promises': 'error', // no-unsafe-* stay on via recommendedTypeChecked
      'boundaries/no-private': 'error',
      'boundaries/element-types': ['error', { default: 'disallow', rules: [
        { from: 'app', allow: ['features', 'components', 'api', 'lib'] },
        { from: 'features', allow: ['components', 'api', 'lib'] },
        { from: 'components', allow: ['components', 'lib'] }, { from: 'api', allow: ['lib'] },
      ] }],
    },
  },
  { files: ['**/*.{test,spec}.{ts,tsx}', 'src/test/**'],  // Vitest + Testing Library overrides
    plugins: { vitest, 'testing-library': testingLibrary },
    rules: { ...vitest.configs.recommended.rules, ...testingLibrary.configs['flat/react'].rules } },
)
```

### 3.3 Tailwind v4 & Knip Wiring

`src/styles.css` is the single stylesheet — `@import "tailwindcss";` then an `@theme { … }` block declaring the design tokens (`--color-*`, `--font-sans`, `--radius-*`) with **no** JS `tailwind.config` runtime. `knip.json` lists `src/main.tsx`, `src/routes/**/*.tsx` (TanStack Router file routes), and `src/mocks/handlers.ts` (MSW) as `entry`, globs `src/**/*.{ts,tsx}` as `project`, ignores the generated client + `routeTree.gen.ts`, and reads the `vite`/`vitest` config plugins so unused files, exports, and deps surface in CI.

### 3.4 Pinned Version Matrix (Aug 2026)

| Package | Today | Target | Notes |
| :--- | :--- | :--- | :--- |
| `react` / `react-dom` | 19.0 | 19.2.x | compiler `target: '19'`; TS 5.8 → 5.9.x (strict) |
| `babel-plugin-react-compiler` | — | **1.0.0 (exact)** | pinned, no caret; GA |
| `vite` / `@vitejs/plugin-react` | 6.4.3 / 4.x | 8.0.x / 5.0.x | Rolldown default bundler |
| `eslint` | 8.57.1 | 10.0.x | flat config only; eslintrc removed |
| `typescript-eslint` / `eslint-plugin-boundaries` | 8.31 / 5.0.1 | 8.39.x / 5.0.x | typed rules + ported layers |
| `-react-hooks` / `-jsx-a11y` / `@vitest/eslint-plugin` / `-testing-library` | — | 6.1.x / 6.10.x / 1.3.x / 7.6.x | flat plugins; hooks bundles `react-compiler` |
| `tailwindcss` / `@tailwindcss/vite` | — | 4.1.x | CSS-first tokens |
| `knip` / `@total-typescript/ts-reset` | — | 5.x / 0.6.x | CI gate; `unknown`-return reset |
| Node engine | — | ≥20.19 / 22.13 / 24 | required by Vite 8 + ESLint 10 |

Primary references: [React Compiler](https://react.dev/learn/react-compiler), [Vite 8 / Rolldown](https://vite.dev/guide/rolldown), [Tailwind v4](https://tailwindcss.com/docs/upgrade-guide), [ESLint flat config](https://eslint.org/docs/latest/use/configure/configuration-files), [typescript-eslint typed linting](https://typescript-eslint.io/getting-started/typed-linting), [Knip](https://knip.dev), and [`docs/architecture/decision-log.md`](../architecture/decision-log.md).

---

## 4. Subagent Implementation Step-by-Step Workflow

1. **Capture the baseline** — run `npm run build`, `npm run lint`, `npm run test`; record current versions and preserve unrelated working-tree changes.
2. **Raise the runtime floor** — set `engines` to Node ≥20.19 / 22.13 / 24; confirm an npm/pnpm lockfile and remove any Yarn PnP artifacts.
3. **Upgrade Vite 6 → 8** — bump `vite` + `@vitejs/plugin-react`; rename any `build.rollupOptions` to `build.rolldownOptions`; verify the Rolldown build.
4. **Adopt React Compiler** — add `babel-plugin-react-compiler` (pinned exact) inside `plugin-react`'s `babel.plugins` with `target: '19'`; rebuild, then remove the `useMemo`/`useCallback`/`React.memo` the compiler makes redundant.
5. **Adopt Tailwind v4** — add the `@tailwindcss/vite` plugin; author `src/styles.css` (`@import "tailwindcss"` + `@theme` tokens); delete the JS `tailwind.config.ts` runtime; import `styles.css` in `main.tsx`.
6. **Migrate ESLint to flat config** — author `eslint.config.js`, **delete `.eslintrc.cjs`**, port every boundaries element/rule, enable `recommendedTypeChecked` with `projectService: true` across all `src`, and add the `react-hooks`, `jsx-a11y`, `vitest`, and `testing-library` overrides.
7. **Resolve type-aware findings** — fix each `no-floating-promises`, `no-misused-promises`, and `no-unsafe-*` violation in real code; do not blanket-disable the rules.
8. **Add Knip** — write `knip.json` for routes/handlers/configs, add a `knip` npm script, and wire it into the CI job as a real gate.
9. **Reinforce the Zod boundary** — add `@total-typescript/ts-reset` (side-effect import in `main.tsx`) so `JSON.parse`/`res.json()` return `unknown`.
10. **Run the full §5 workflow**, record the version matrix in `docs/architecture/decision-log.md`, and update the frontend `AGENTS.md` only where paths changed.

---

## 5. Independent Verification & Acceptance Workflow

### 5.1 Verification Commands

```bash
node -v && npm ci --prefix src/Frontend                                  # Node ≥20.19, npm/pnpm
npm --prefix src/Frontend run build && npx --prefix src/Frontend tsc --noEmit   # Vite 8 (Rolldown), 0 TS errors
npm --prefix src/Frontend run lint                                       # flat config, type-checked rules
npx --prefix src/Frontend knip                                           # dead-code gate (CI step)
# eslintrc gone; boundaries ported; compiler pinned exact; no Yarn PnP.
test ! -e src/Frontend/.eslintrc.cjs && test ! -e src/Frontend/.pnp.cjs && test ! -e src/Frontend/.yarnrc.yml
rg -n "boundaries/element-types|no-floating-promises" src/Frontend/eslint.config.js
rg -n '"babel-plugin-react-compiler": "[0-9]' src/Frontend/package.json   # exact, no caret
# A deliberately floating promise MUST fail lint (expect non-zero exit).
printf 'async function f(){}\nexport function g(){ f() }\n' > src/Frontend/src/__floating.ts
! npm --prefix src/Frontend run lint ; rm src/Frontend/src/__floating.ts
```

### 5.2 Acceptance Criteria

| ID | Acceptance criterion | Evidence |
| :--- | :--- | :--- |
| **FE-01** | `npm run build` completes with **0 TypeScript errors** on Vite 8 (Rolldown). | Build log + `tsc --noEmit` clean |
| **FE-02** | React Compiler is **active in every build**; `babel-plugin-react-compiler` is pinned exact and `react-hooks/react-compiler` is green. | Compiled output + version `rg` |
| **FE-03** | `npm run lint` runs on the flat `eslint.config.js` with type-checked rules and passes clean. | Lint log |
| **FE-04** | A deliberately floating promise **fails** `@typescript-eslint/no-floating-promises`. | Non-zero exit on the §5.1 probe |
| **FE-05** | **No `.eslintrc*`** file remains under `src/Frontend`. | File-absent test |
| **FE-06** | `eslint-plugin-boundaries` layers are still enforced — a cross-layer import fails lint. | Boundaries violation test |
| **FE-07** | Tailwind v4 builds CSS-first; `styles.css` `@theme` tokens resolve with no JS `tailwind.config` runtime. | Build output + absent config |
| **FE-08** | `knip` runs in CI and reports unused files/exports/deps, taught about routes/handlers/configs. | CI job log |
| **FE-09** | `engines` requires **Node ≥20.19 / 22.13 / 24**; lockfile is npm/pnpm with **no Yarn PnP**. | `package.json` + absent `.pnp.cjs` |
| **FE-10** | `@total-typescript/ts-reset` narrows `JSON.parse`/`res.json()` to `unknown`, keeping the Zod boundary enforced. | Type probe asserting `unknown` |

---

## 6. Anti-Cheating & Integrity Guardrails

1. Leave **no `.eslintrc*`** anywhere under `src/Frontend`; the flat `eslint.config.js` is the single lint configuration, resolved by ESLint 10 without a legacy shim.
2. Keep type-checked rules enabled — `projectService: true` spans all `src`, and `no-floating-promises`, `no-misused-promises`, and `no-unsafe-*` are **never** disabled wholesale; only narrowly justified per-line disables are allowed, never to bury a real defect.
3. Do **not** remove `eslint-plugin-boundaries` — port every element and rule; a cross-layer import must still fail lint exactly as under the legacy config.
4. Pin `babel-plugin-react-compiler` to an **exact** version (no caret/range); React Compiler is on for every build, never opt-in per file, and never disabled to sidestep a rule.
5. Remove **only** the `useMemo`/`useCallback`/`React.memo` the compiler proves redundant; do not strip memoization it cannot prove safe.
6. Use **npm or pnpm only** — no Yarn PnP (`.pnp.cjs`/`.yarnrc.yml`), and a single committed lockfile.
7. Lint and Knip ignore **only** generated output (`src/api/generated/**`, `src/routeTree.gen.ts`); nothing else is broadly ignored to inflate a green run, and Knip's `project` glob still covers all `src`.
8. Keep Tailwind v4 **CSS-first** — tokens live in `styles.css` `@theme`; do not reintroduce a JS `tailwind.config` runtime to bypass the Vite plugin pipeline.
9. Do not touch Task 19 UI/forms or the Task 16 codegen output beyond linting and bundling it; changes stay inside the platform files listed above.
10. Do not mark the task Implemented until every §5 command has run and can fail with a non-zero exit, and the version matrix is recorded in `docs/architecture/decision-log.md`.
