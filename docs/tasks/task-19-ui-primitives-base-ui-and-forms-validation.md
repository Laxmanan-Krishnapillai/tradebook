# Task 19: UI Primitives on Base UI, Tremor Removal & Schema-Validated Forms

> **GREENFIELD MODERNIZATION TASK (2026-08-07)** — Migrate every UI primitive in `src/Frontend` to Base UI via shadcn/ui, remove `@tremor/react` entirely, and adopt React Hook Form + Zod schema validation for all forms and external inputs. This is a committed, repo-wide adoption. See `docs/architecture/decision-log.md` for the primitive-library and forms/validation decisions, and `docs/architecture/spec-issues.md` for the recorded `@tremor/react` React 19 peer conflict that forces `--legacy-peer-deps` today.

- **Phase**: Frontend Modernization
- **Complexity**: High
- **Prerequisites**: Task 16, Task 18
- **Relates To**: Task 11
- **Status**: Specified
- **Target Files**:
  - `src/Frontend/src/components/ui/` — copied-and-owned Base UI primitive source (`button.tsx`, `dialog.tsx`, `select.tsx`, `popover.tsx`, `form.tsx`, …)
  - `src/Frontend/src/components/kpi/kpi-tile.tsx` and feature KPI/stat tiles rebuilt on Base UI + Tailwind
  - `src/Frontend/package.json` — drop `@tremor/react`; add Base UI, RHF, resolvers, Zod
  - `src/Frontend/src/features/*/components/*Form.tsx` — form components under feature folders
  - `src/Frontend/src/lib/validation/` — Problem Details, return-URL, and storage validation helpers
  - `src/Frontend/src/app/routes/` — `validateSearch` search-param schemas

---

## 1. Context

### 1.1 Problem Statement
The frontend's UI primitives sit on two libraries pulling in opposite directions. Radix UI is in slow maintenance, and `@tremor/react@3.18.7` (last released January 2025, effectively frozen) declares a React 18 peer range that conflicts with the app's React 19, forcing every install to run with `--legacy-peer-deps` — a fragile, CI-hostile workaround recorded in `docs/architecture/spec-issues.md`. Separately, the app has no form library and no runtime schema library: forms are assembled from raw `useState`, submissions reach mutations unvalidated, and external inputs (route search params, return URLs, `localStorage`, API and SignalR payloads) cross the trust boundary unchecked. Task 18's ts-reset now types `JSON.parse` as `unknown`, turning that gap into a compile error rather than a latent bug.

### 1.2 Required Outcomes
- Every primitive is owned shadcn/ui source under `src/Frontend/src/components/ui/`, backed by Base UI (`@base-ui-components/react`, 1.x, MUI-funded) — the shadcn default registry since July 2026.
- `@tremor/react` is removed from `package.json` and the lockfile; all KPI/stat tiles are rebuilt on Base UI + Tailwind v4 + the existing chart libraries.
- `--legacy-peer-deps` is no longer needed anywhere; a clean install resolves cleanly on React 19.
- All forms use React Hook Form 7 + `@hookform/resolvers` (Standard Schema) + Zod 4, reuse Task 16 generated schemas where shapes match, and map RFC 9457 Problem Details onto the correct fields.
- Every external input is Zod-validated at the boundary (Standard Schema, no adapter).

### 1.3 In Scope
- Migrating all primitives to Base UI via shadcn/ui and owning their source under `components/ui/`.
- Removing `@tremor/react` and rebuilding every KPI/stat tile on Base UI + Tailwind + echarts/lightweight-charts.
- Introducing RHF + resolvers + Zod and converting every form, including Problem Details → `setError` mapping.
- `validateSearch` search-param schemas, return-URL validation, and `localStorage`/payload validation helpers.
- Component, interaction, and a11y tests for the migrated primitives and forms.

### 1.4 Out of Scope
- The OpenAPI → Zod/TanStack Query codegen pipeline (owned by Task 16); consume its output, do not modify it.
- Routing and session boundary definitions (owned by Task 11); this task supplies the search-param and return-URL schemas that plug into them (aligns ROUTE-02).
- Chart library selection (already fixed: echarts, lightweight-charts, `@xyflow/react`).
- The backend Problem Details contract (RFC 9457, produced by the API).

## 2. Technical Approach
Four workstreams land together behind one dependency change:

- **Primitives.** Run `shadcn init` against the Base UI registry, then `add` each primitive. The CLI writes source we own into `components/ui/`, so we tune Tailwind v4 tokens and keyboard behavior for a data-dense, keyboard-first app. Each `@radix-ui/*` primitive package is removed once its equivalent lands. **react-aria** stays for the highest-a11y-risk custom widgets (virtualized grid, combobox/listbox), and **cmdk** stays for the command palette.
- **Tiles.** Delete Tremor `<Card>/<Metric>/<BarList>` usages; the replacement `KpiTile` is plain Base UI + Tailwind, with sparklines rendered by the already-chosen chart libraries. `recharts` is explicitly excluded.
- **Forms.** A shared `Form` primitive (shadcn) wires RHF context. Each feature form declares a Zod input schema — or imports the Task 16 generated one where the shape matches — and passes it to `standardSchemaResolver`. Controlled Base UI/react-aria inputs bind through `Controller`. On mutation failure, `applyProblemDetails` parses the RFC 9457 body and calls `setError` per field; errors clear via `clearErrors` on the next submit.
- **Boundaries.** Zod schemas validate every untrusted input. TanStack Router consumes the Zod schema directly in `validateSearch` (Zod 4 implements Standard Schema, so `@tanstack/zod-adapter` is unnecessary). Return URLs pass through `internalPath`, which rejects anything that is not a same-origin absolute path.

## 3. Implementation Blueprint

### 3.1 Owned Base UI primitive (shadcn source)
```tsx
// src/Frontend/src/components/ui/dialog.tsx — owned shadcn/ui source on Base UI
import * as React from 'react'
import { Dialog as BaseDialog } from '@base-ui-components/react/dialog'
import { cn } from '@/lib/utils'

export const Dialog = BaseDialog.Root
export const DialogTrigger = BaseDialog.Trigger
export const DialogClose = BaseDialog.Close

export function DialogContent({
  className,
  children,
  ...props
}: React.ComponentProps<typeof BaseDialog.Popup>) {
  return (
    <BaseDialog.Portal>
      <BaseDialog.Backdrop className="fixed inset-0 z-50 bg-black/50 backdrop-blur-sm" />
      <BaseDialog.Popup
        className={cn(
          'fixed left-1/2 top-1/2 z-50 grid w-full max-w-lg -translate-x-1/2 -translate-y-1/2',
          'gap-4 rounded-lg border bg-background p-6 shadow-lg outline-none',
          className,
        )}
        {...props}
      >
        {children}
      </BaseDialog.Popup>
    </BaseDialog.Portal>
  )
}
```

### 3.2 Schema-validated form with Problem Details → `setError`
```tsx
// src/Frontend/src/features/trades/components/TradeForm.tsx
import { useForm, Controller } from 'react-hook-form'
import { standardSchemaResolver } from '@hookform/resolvers/standard-schema'
import { z } from 'zod'
import { useCreateTrade } from '@/features/trades/api'          // Task 16 generated hook
import { applyProblemDetails } from '@/lib/validation/problem-details'
import { Select } from '@/components/ui/select'
import { Button } from '@/components/ui/button'

const TradeInput = z.object({
  instrumentId: z.uuid(),
  side: z.enum(['Buy', 'Sell']),
  quantity: z.number().positive(),
  price: z.number().positive(),
  tradeDate: z.iso.date(),
})
type TradeInput = z.infer<typeof TradeInput>

export function TradeForm({ onDone }: { onDone: () => void }) {
  const createTrade = useCreateTrade()
  const form = useForm<TradeInput>({
    resolver: standardSchemaResolver(TradeInput),           // Standard Schema, reuses Task 16 shapes
    defaultValues: { side: 'Buy', quantity: 0, price: 0 },
  })

  async function onSubmit(values: TradeInput) {
    form.clearErrors()                                       // clear prior server errors on resubmit
    try {
      await createTrade.mutateAsync(values)
      onDone()
    } catch (err) {
      applyProblemDetails(err, form.setError)               // RFC 9457 errors → field errors
    }
  }

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="grid gap-4">
      <Controller                                           // Base UI select is controlled
        control={form.control}
        name="side"
        render={({ field }) => (
          <Select value={field.value} onValueChange={field.onChange} options={['Buy', 'Sell']} />
        )}
      />
      {/* instrumentId / quantity / price / tradeDate inputs bound via register or Controller */}
      <Button type="submit" disabled={form.formState.isSubmitting}>Save trade</Button>
    </form>
  )
}
```

```ts
// src/Frontend/src/lib/validation/problem-details.ts
import { z } from 'zod'
import type { UseFormSetError, FieldValues, Path } from 'react-hook-form'

const ProblemDetails = z.object({
  type: z.string().optional(),
  title: z.string().optional(),
  status: z.number().optional(),
  errors: z.record(z.string(), z.array(z.string())).optional(), // RFC 9457 validation extension
})

export function applyProblemDetails<T extends FieldValues>(
  err: unknown,
  setError: UseFormSetError<T>,
): void {
  const parsed = ProblemDetails.safeParse((err as { body?: unknown })?.body ?? err)
  if (!parsed.success || !parsed.data.errors) return
  for (const [field, messages] of Object.entries(parsed.data.errors)) {
    const path = field.charAt(0).toLowerCase() + field.slice(1)  // PascalCase → camelCase
    setError(path as Path<T>, { type: 'server', message: messages.join(' ') })
  }
}
```

### 3.3 Route search-param validation (Standard Schema, no adapter)
```tsx
// src/Frontend/src/app/routes/trades.index.tsx
import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import { internalPath } from '@/lib/validation/return-url'

const TradesSearch = z.object({
  portfolioId: z.uuid().optional(),
  side: z.enum(['Buy', 'Sell']).optional(),
  from: z.iso.date().optional(),
  to: z.iso.date().optional(),
  page: z.number().int().min(1).catch(1),
  returnTo: internalPath.optional(),          // rejects external URLs — aligns Task 11 ROUTE-02
})

export const Route = createFileRoute('/trades/')({
  validateSearch: TradesSearch,               // Zod 4 is a Standard Schema; no @tanstack/zod-adapter
  component: TradesPage,
})
```

```ts
// src/Frontend/src/lib/validation/return-url.ts
import { z } from 'zod'

// Accept only same-origin absolute-path routes ("/trades", "/positions/42").
// Reject protocol-relative ("//evil.com"), absolute ("https://evil.com"), and "javascript:" URLs.
export const internalPath = z
  .string()
  .refine((v) => /^\/(?!\/)[\w\-./?=&%]*$/.test(v), { message: 'Only internal routes are allowed' })
```

### 3.4 KPI tile on Base UI + Tailwind (replaces Tremor)
```tsx
// src/Frontend/src/components/kpi/kpi-tile.tsx — replaces @tremor/react <Card>/<Metric>
import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

interface KpiTileProps { label: string; value: string; delta?: number; spark?: ReactNode; className?: string }

export function KpiTile({ label, value, delta, spark, className }: KpiTileProps) {
  const up = (delta ?? 0) >= 0
  return (
    <div className={cn('rounded-lg border bg-card p-4 tabular-nums', className)}>
      <div className="text-sm text-muted-foreground">{label}</div>
      <div className="mt-1 flex items-baseline gap-2">
        <span className="text-2xl font-semibold">{value}</span>
        {delta != null && (
          <span className={cn('text-xs', up ? 'text-emerald-600' : 'text-rose-600')}>
            {up ? '▲' : '▼'} {Math.abs(delta)}%
          </span>
        )}
      </div>
      {spark /* echarts / lightweight-charts sparkline — never recharts */}
    </div>
  )
}
```

### 3.5 Dependency versions (Aug 2026 current)
| Package | Version | Notes |
| --- | --- | --- |
| `react` / `react-dom` | `^19.1` | App baseline |
| `@base-ui-components/react` | `^1.0` | Base UI, MUI-funded; shadcn default registry since Jul 2026 |
| `shadcn` (CLI) | `^3` | Generates owned source; not a runtime dependency |
| `react-hook-form` | `^7.62` | Form state + `Controller` |
| `@hookform/resolvers` | `^5.2` | `standardSchemaResolver` entry point |
| `zod` | `^4.1` | Implements Standard Schema |
| `@tanstack/react-router` | `^1.131` | `validateSearch` takes the Zod schema directly |
| `react-aria-components` | `^1.x` | Retained for a11y-critical custom widgets |
| `cmdk` | `^1.1` | Retained for the command palette |
| `tailwindcss` | `^4.x` | From Task 18 |
| `@tremor/react` | **REMOVED** | Was `3.18.7`; React 19 peer conflict |

### 3.6 Reference docs
- `docs/architecture/decision-log.md` — primitive-library decision (Base UI over Radix/Tremor) and forms/validation decision.
- `docs/architecture/spec-issues.md` — recorded `@tremor/react` React 19 peer conflict and `--legacy-peer-deps` workaround.
- Base UI — https://base-ui.com | shadcn/ui — https://ui.shadcn.com
- React Hook Form — https://react-hook-form.com | Standard Schema — https://standardschema.dev | Zod 4 — https://zod.dev
- TanStack Router `validateSearch` — https://tanstack.com/router | RFC 9457 — https://www.rfc-editor.org/rfc/rfc9457

## 4. Migration Steps
1. Land the dependency change: remove `@tremor/react`, add Base UI + RHF + resolvers + Zod, and delete every `--legacy-peer-deps` invocation from CI and install docs.
2. Run `shadcn init` and `shadcn add` for the full primitive set; commit the generated `components/ui/*` source.
3. Replace primitive imports feature by feature; delete each `@radix-ui/*` package once it is unreferenced.
4. Rebuild KPI/stat dashboards on `KpiTile`; remove all Tremor imports and wire sparklines to echarts/lightweight-charts.
5. Add `src/Frontend/src/lib/validation/` helpers (`problem-details.ts`, `return-url.ts`, `storage.ts`).
6. Convert each feature form to RHF + `standardSchemaResolver` + `Controller`; wire the Problem Details mapping.
7. Add `validateSearch` schemas to every route with search params; route all return-URLs through `internalPath`.
8. Add component, interaction, and a11y tests; run the §5.1 gate and confirm Tremor is absent from source and lockfile.

## 5. Testing & Acceptance

### 5.1 Commands
```bash
cd src/Frontend

# Remove Tremor and install the Base UI + forms stack (NO --legacy-peer-deps)
npm remove @tremor/react
npx shadcn@latest init                       # Base UI is the default registry since Jul 2026
npx shadcn@latest add button dialog select popover form
npm install react-hook-form @hookform/resolvers zod

# Clean install must resolve without peer overrides
rm -rf node_modules package-lock.json && npm install

npm run lint                                  # ESLint 10 + jsx-a11y (Task 18)
npm run build                                 # tsc + vite, must be green
npm run test                                  # Vitest: component, interaction, a11y (axe)

# Prove Tremor is gone from source, manifest, and lockfile
! grep -R "@tremor/react" src package.json package-lock.json
```

### 5.2 Acceptance Criteria
| ID | Criterion | Verification |
| --- | --- | --- |
| UI-01 | All primitives import from `@/components/ui/*` backed by `@base-ui-components/react`; no `@radix-ui/*` primitive remains except where react-aria owns the widget | grep + build |
| UI-02 | `@tremor/react` is absent from `package.json` **and** `package-lock.json` | §5.1 grep gate |
| UI-03 | `npm install` succeeds with no `--legacy-peer-deps`, `--force`, or peer overrides | clean install |
| UI-04 | KPI/stat tiles render on Base UI + Tailwind with echarts/lightweight-charts sparklines; no `recharts` dependency added | build + grep |
| UI-05 | `npm run build` and `npm run test` are green | CI |
| FORM-01 | Every form uses a schema resolver (`standardSchemaResolver`/`zodResolver`); no form submits unvalidated `useState` state | lint rule + review |
| FORM-02 | A 422/409 Problem Details response maps its `errors` onto the correct field via `setError`, cleared on resubmit | interaction test |
| FORM-03 | Base UI / react-aria controlled inputs are bound through `Controller` | review + test |
| SEC-01 | A hostile `?returnTo=https://evil.com` is rejected by the search-param schema; only internal routes pass (Task 11 ROUTE-02) | unit + interaction test |
| SEC-02 | `localStorage` reads and SignalR/API payloads are Zod-parsed; `JSON.parse` output is never consumed as unchecked `unknown` | review + test |
| A11Y-01 | jsx-a11y is clean and axe assertions pass for every migrated primitive | Vitest + lint |

## 6. Guardrails
1. Never reintroduce `@tremor/react`, and never add `recharts`. Charts stay on echarts, lightweight-charts, and `@xyflow/react`.
2. Never pass `--legacy-peer-deps` or `--force`; the dependency graph must resolve cleanly on React 19.
3. Every form uses a schema resolver — no unvalidated `useState`-driven submit reaches a mutation.
4. Every external input — route search params, return URLs, `localStorage`, and API + SignalR payloads — is Zod-validated at the boundary (Standard Schema, no adapter).
5. Reuse Task 16 generated Zod schemas wherever the shape matches; never hand-edit generated schema files — compose or extend them in `src/Frontend/src/lib/validation/`.
6. Keep `eslint-plugin-boundaries` layering intact; owned `components/ui/*` source stays in the UI layer and must not import feature code.
7. Keep react-aria for the most a11y-critical custom widgets and cmdk for the command palette; do not swap them for Base UI equivalents.
8. Bind controlled Base UI/react-aria inputs through `Controller`; do not bypass React Hook Form with ad-hoc component state.
