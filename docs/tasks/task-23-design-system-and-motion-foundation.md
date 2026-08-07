# Task 23: Design System & Motion Foundation

> **GREENFIELD MODERNIZATION TASK (2026-08-07)** — Adopt an OKLCH design-token system, typography-for-data, six-state components, and a restrained motion vocabulary so the UI reaches Linear/Twenty-grade polish. This task establishes the visual and motion foundation that makes 500 screens read as one premium product, layered on top of the already-decided performance substrate. Part of the Greenfield Modernization Program; see `docs/architecture/decision-log.md` for the governing ADRs.

- **Phase**: Greenfield Modernization — Frontend Foundation
- **Lead / Owner**: Frontend Platform / Design Systems
- **Complexity**: High
- **Prerequisites**: Task 18, Task 19 (relates to Task 05, Task 06)
- **Status**: Specified
- **Target Files** (full layout in §2):
  - `src/Frontend/src/styles.css`, `src/Frontend/src/styles/fonts.css`
  - `src/Frontend/src/components/providers/motion-provider.tsx`, `src/Frontend/src/router.tsx`
  - `src/Frontend/src/components/ui/{numeric-cell,table,skeleton,empty-state,density-toggle}.tsx`
  - `src/Frontend/src/stores/preferences.ts`, `src/Frontend/src/lib/motion/tokens.ts`, `src/Frontend/package.json`

---

## 1. Detailed Scope & Feature Coverage

### 1.1 Problem Statement

Task 18 and Task 19 settled the substrate: React 19 with the React Compiler, Vite 8, TypeScript strict, Tailwind v4 CSS-first `@theme`, and Base UI primitives wired through shadcn/ui. The performance envelope is likewise fixed and non-negotiable in `docs/research/snappy-crud-ui-ux.md`: **0 ms optimistic latency, <16.6 ms / 60 fps on interactive surfaces, <10 ms keyboard command, RxJS `bufferTime(50)` realtime batching, and Motion routed through `LazyMotion` + `m` (~4.6 kb) and kept OFF canvas/grid/editor hot surfaces**. What is missing is a shared visual and motion language. Without it, this data-dense, keyboard-first, realtime trading UI drifts across 500 screens: ad-hoc hex and pixel values leak into components, numeric columns jitter as live values re-render, spinners stand in for structure, and motion sneaks onto the surfaces the budgets protect. This task closes that gap with one source of truth.

### 1.2 Required Outcomes

- A two-layer OKLCH token system in Tailwind v4 `@theme` owns six scales — color, spacing, type, radius, shadow/ring, and motion — as the only source components consume.
- Typography is tuned for data: one UI typeface plus a mono, with `tabular-nums slashed-zero lining-nums` on every numeric cell and metric.
- Every interactive Base UI/shadcn element ships six designed states, one focus ring, hairline borders, and a defined elevation ladder.
- A restrained motion vocabulary drives chrome, live numbers, and route swaps while honoring `prefers-reduced-motion` and preserving the existing budgets verbatim.
- Iconography is unified on Lucide at one stroke width and fixed sizes.

### 1.3 In Scope

- **OKLCH design tokens** in `@theme` — raw primitives in `:root`/`.dark`, semantic tokens via `@theme inline`; near-neutral ramp plus ONE accent; dark mode swaps lightness rather than inverting; hover/selected derived through `color-mix(in oklch, …)`; extra hues only for buy/sell/warn semantics, never color-alone; spacing on Tailwind's single `--spacing` 4px multiplier; `--text-*` with bundled line-heights and negative tracking as size grows; radius scale; hairline-border-first shadow/ring with one focus token; motion easings and durations.
- **Typography for data** — self-hosted or npm-delivered Inter/Geist for full OpenType features (the Google Fonts Geist build lacks them), a mono for IDs/tickets, 4–6 sizes, tabular numerics applied to every numeric surface.
- **Component craft** — six states per interactive element, one focus ring, hairline 1px dividers over zebra, explicit elevation ladder, low-opacity accent tints for hover/selected rows, a density switcher (condensed ~28–32px / regular / relaxed), left-aligned text and right-aligned tabular numbers, designed empty states, and layout-matched skeletons as the loading treatment.
- **Motion vocabulary** — Motion via `LazyMotion` + `m` for chrome, NumberFlow on all live numbers, native View Transitions through TanStack Router `defaultViewTransition`, `tw-animate-css` for `data-state` enter/exit, and AutoAnimate available for small non-virtualized lists.
- **Motion accessibility & budgets** — `<MotionConfig reducedMotion="user">`, an in-app reduce-motion / max-density toggle, compositor-only properties, and the existing 0 ms / 60 fps / <10 ms / `bufferTime(50)` budgets carried through unchanged.
- **Icons** — Lucide at ~1.5 stroke, fixed 14/16/20 sizes, solid only for active.

### 1.4 Out of Scope

- Agent lint and visual-QA guardrails that machine-enforce these rules — owned by Task 24; this task supplies the grep-able contracts it will police.
- Chart and visualization engines — ECharts, TradingView lightweight-charts, and @xyflow are already chosen; this task only hands them tokens.
- The performance substrate itself (Task 18) and the CRUD screens (Task 05) and KPI/visualization surfaces (Task 06) beyond adopting the tokens defined here.
- No Magic UI, Aceternity, Skiper, or Cult component libraries enter the codebase.

---

## 2. Key Deliverables & File Layout

```text
src/Frontend/src/
├─ styles.css                       # @theme tokens: color · spacing · type · radius · shadow · motion
├─ styles/fonts.css                 # self-hosted Inter/Geist + mono @font-face, OpenType features on
├─ router.tsx                       # createRouter({ defaultViewTransition: true })
├─ stores/preferences.ts            # zustand: theme · density · reduceMotion (persisted)
├─ lib/motion/tokens.ts             # TS mirror of --ease-* / --duration-* for m variants
├─ components/providers/motion-provider.tsx   # <LazyMotion features={domAnimation} strict> + <MotionConfig>
└─ components/ui/
   ├─ numeric-cell.tsx              # NumberFlow + tabular-nums; viewport + meaningful-change gate
   ├─ table.tsx                     # density-aware; hairline dividers; row hover/selected tints
   ├─ skeleton.tsx                  # layout-matched loading treatment (NOT spinners)
   ├─ empty-state.tsx               # designed empty states
   └─ density-toggle.tsx            # condensed / regular / relaxed
src/Frontend/package.json           # motion · @number-flow/react · tw-animate-css · lucide-react · fonts
docs/architecture/decision-log.md   # ADR reference (this task's decisions)
docs/research/snappy-crud-ui-ux.md  # HARD budgets, reused verbatim
```

---

## 3. Architecture & Code Contract Blueprints

**Two-layer OKLCH tokens + motion tokens** — `src/Frontend/src/styles.css`:

```css
@import "tailwindcss";
@import "tw-animate-css";
@custom-variant dark (&:where(.dark, .dark *));

/* Layer 1 — raw OKLCH primitives. Dark mode swaps LIGHTNESS, never inverts.
   Extra hues exist ONLY for buy/sell/warn; chrome is a near-neutral ramp + ONE accent. */
:root {
  --neutral-50: oklch(0.985 0.002 265); --neutral-200: oklch(0.922 0.004 265);
  --neutral-500: oklch(0.556 0.012 265); --neutral-900: oklch(0.198 0.008 265);
  --accent-500: oklch(0.62 0.17 258);
  --buy-500: oklch(0.68 0.16 152); --sell-500: oklch(0.63 0.20 25); --warn-500: oklch(0.80 0.15 85);
  --spacing: 0.25rem; --radius: 0.5rem;   /* single 4px multiplier + radius base */
}
.dark {   /* same hue + chroma, lightness swapped — not inverted */
  --neutral-50: oklch(0.146 0.006 265); --neutral-200: oklch(0.262 0.010 265);
  --neutral-500: oklch(0.646 0.012 265); --neutral-900: oklch(0.968 0.003 265);
  --accent-500: oklch(0.68 0.16 258);
}

/* Layer 2 — semantic tokens. Components consume ONLY these. */
@theme inline {
  --color-background: var(--neutral-50); --color-foreground: var(--neutral-900);
  --color-muted-foreground: var(--neutral-500); --color-border: var(--neutral-200);
  --color-accent: var(--accent-500);
  --color-row-hover:    color-mix(in oklch, var(--accent-500) 6%, transparent);
  --color-row-selected: color-mix(in oklch, var(--accent-500) 12%, transparent);
  --color-buy: var(--buy-500); --color-sell: var(--sell-500); --color-warn: var(--warn-500);

  /* Type — bundled line-heights, negative tracking as size grows */
  --text-xs: 0.75rem;   --text-xs--line-height: 1.1rem;
  --text-sm: 0.8125rem; --text-sm--line-height: 1.25rem;
  --text-base: 0.875rem;--text-base--line-height: 1.4rem;
  --text-lg: 1.0625rem; --text-lg--line-height: 1.6rem; --text-lg--letter-spacing: -0.01em;
  --text-xl: 1.375rem;  --text-xl--line-height: 1.8rem; --text-xl--letter-spacing: -0.02em;

  /* Elevation — hairline-border-first, low-opacity shadows, ONE focus ring */
  --shadow-sm:  0 1px 3px 0 oklch(0 0 0 / 0.06), 0 1px 2px -1px oklch(0 0 0 / 0.06);
  --ring-focus: 0 0 0 2px var(--color-background), 0 0 0 4px var(--accent-500);

  /* MOTION — single source of truth */
  --ease-standard: cubic-bezier(0.2,0,0,1); --ease-decelerate: cubic-bezier(0,0,0,1);
  --ease-accelerate: cubic-bezier(0.3,0,1,1); --ease-swift: cubic-bezier(0.16,1,0.3,1);
  --duration-instant: 75ms; --duration-fast: 100ms;
  --duration-base: 150ms; --duration-moderate: 200ms;
}

@layer base {
  html { font-variant-numeric: lining-nums; }
  @media (prefers-reduced-motion: reduce) {
    *, *::before, *::after { animation-duration: 1ms !important; transition-duration: 1ms !important; }
  }
}
```

**NumberFlow numeric cell** — `src/Frontend/src/components/ui/numeric-cell.tsx`. Every numeric surface renders through this; virtualized rows gate the roll by viewport and meaningful change and prefer a color flash:

```tsx
import NumberFlow from "@number-flow/react";
import { cn } from "@/lib/utils";

interface NumericCellProps {
  value: number;                   // money is decimal end-to-end
  format?: Intl.NumberFormatOptions;
  animate?: boolean;               // true only for on-screen live cells (viewport-gated)
  flashOnChange?: boolean;         // dense virtualized rows favor a flash over a roll
  className?: string;
}

export function NumericCell({ value, format, animate = false, flashOnChange, className }: NumericCellProps) {
  return (
    <span className={cn(
      "text-right tabular-nums slashed-zero lining-nums",   // never jitters
      flashOnChange && "transition-colors duration-[--duration-fast]", className,
    )}>
      <NumberFlow value={value} format={format} animated={animate} respectMotionPreference />
    </span>
  );
}
```

**LazyMotion + MotionConfig provider** — `src/Frontend/src/components/providers/motion-provider.tsx`. `strict` throws if the full `motion` component is ever used, and `reducedMotion="user"` binds every animation to the OS preference and the in-app toggle:

```tsx
import { LazyMotion, MotionConfig, domAnimation } from "motion/react";
import type { PropsWithChildren } from "react";

export function MotionProvider({ children }: PropsWithChildren) {
  return (
    <LazyMotion features={domAnimation} strict>
      <MotionConfig reducedMotion="user">{children}</MotionConfig>
    </LazyMotion>
  );
}
```

**TanStack Router View Transitions** — `src/Frontend/src/router.tsx`. Structural swaps only; hot routes opt out per-navigation with `viewTransition: false`:

```tsx
import { createRouter } from "@tanstack/react-router";
import { routeTree } from "./routeTree.gen";

export const router = createRouter({
  routeTree,
  defaultViewTransition: true,   // structural swaps only; hot routes pass viewTransition: false
  defaultPreload: "intent",
  scrollRestoration: true,
});
```

**Six designed states** — every interactive element resolves to token-only treatments:

| State | Treatment | Token(s) |
| --- | --- | --- |
| default | surface + hairline border | `--color-background`, `--color-border` |
| hover | low-opacity accent tint | `--color-row-hover` |
| focus-visible | one consistent ring | `--ring-focus` |
| active | selected accent tint | `--color-row-selected` |
| disabled | reduced foreground, no shadow | `--color-muted-foreground` |
| loading | layout-matched skeleton (never a spinner) | `skeleton.tsx` |

**Adopted versions** — `src/Frontend/package.json`:

| Package | Version | Purpose |
| --- | --- | --- |
| `tailwindcss` | `^4.1` | CSS-first `@theme` tokens |
| `motion` | `^12.23` | chrome motion via `LazyMotion` + `m` |
| `@number-flow/react` | `^0.5` | live-number animation |
| `tw-animate-css` | `^1.4` | `data-state` CSS enter/exit |
| `@tanstack/react-router` | `^1.132` | `defaultViewTransition` |
| `@fontsource-variable/inter` | `^5.2` | self-hosted UI typeface + OpenType |
| `lucide-react` | `^0.545` | icons at 1.5 stroke, 14/16/20 |
| `@formkit/auto-animate` | `^0.8` | small non-virtualized lists (optional) |
| `geist` | `^1.5` | alternate self-hosted UI/mono typeface |

**Reference docs**: `docs/architecture/decision-log.md` (governing ADRs), `docs/research/snappy-crud-ui-ux.md` (HARD budgets), Tailwind v4 `@theme`, NumberFlow, Motion `LazyMotion`, and TanStack Router View Transitions upstream guides.

---

## 4. Subagent Implementation Step-by-Step Workflow

1. Author Layer 1 OKLCH primitives and Layer 2 semantic tokens in `styles.css`; wire `@custom-variant dark`; add the reduced-motion base reset. Verify dark mode swaps lightness while hue and chroma hold.
2. Self-host Inter/Geist and the mono in `fonts.css` with full OpenType features enabled; set `tabular-nums slashed-zero lining-nums` defaults for numeric contexts.
3. Build `NumericCell` over NumberFlow; route every price, P&L, quantity, and total through it; gate the roll in virtualized rows by viewport and meaningful change, defaulting dense cells to a color flash.
4. Add `MotionProvider` (`LazyMotion` `strict` + `MotionConfig reducedMotion="user"`) and mount it at the app root; convert all chrome call sites to `m`.
5. Set `defaultViewTransition: true` on the router; opt grid, canvas, and editor routes out per-navigation.
6. Refactor Base UI/shadcn components to the six states, one focus ring, hairline borders, and the elevation ladder; apply row hover/selected tints and hairline dividers.
7. Extend the `preferences` zustand store with theme, density (condensed/regular/relaxed), and reduceMotion; persist it and surface the reduce-motion / max-density toggle.
8. Replace every data-surface spinner with a layout-matched skeleton and add designed empty states.
9. Standardize all icons on Lucide at ~1.5 stroke and 14/16/20 sizes, solid only for active.
10. Run the full verification suite and hand the grep contracts to Task 24 for CI enforcement.

---

## 5. Independent Verification & Acceptance Workflow

### 5.1 Verification Commands

```bash
# DS-01 build + unit/integration green
npm --prefix src/Frontend run build
npm --prefix src/Frontend run test

# DS-02 every numeric cell resolves tabular numerics
rg -n "NumericCell|tabular-nums slashed-zero lining-nums" src/Frontend/src

# DS-03 no ad-hoc hex/px in components — tokens only
rg -n "#[0-9a-fA-F]{3,8}\b|\b[0-9]+px\b" src/Frontend/src/components/ui && echo "FAIL raw hex/px" || echo "OK"

# DS-07 no heavy effects on hot surfaces
rg -n "box-shadow|backdrop-blur|AnimatePresence|\blayout\b" \
  src/Frontend/src/components/{grid,canvas,editor} && echo "FAIL hot-surface effect" || echo "OK"

# DS-08 Motion only via m / LazyMotion — never the full motion component
rg -n "\bmotion\.[a-z]" src/Frontend/src && echo "FAIL full motion import" || echo "OK"

# DS-09 skeletons, not spinners, on data surfaces
rg -n "Spinner|animate-spin" src/Frontend/src/components/ui && echo "FAIL spinner" || echo "OK"
```

### 5.2 Acceptance Criteria

| ID | Criterion | Verification |
| --- | --- | --- |
| DS-01 | `npm run build` and `npm run test` pass green | CI |
| DS-02 | Every numeric cell resolves `tabular-nums slashed-zero lining-nums` | grep + RTL |
| DS-03 | Tokens live only in `@theme`; no ad-hoc hex/px in components | grep + lint |
| DS-04 | Dark mode swaps lightness with hue and chroma stable, never inverts | token test |
| DS-05 | NumberFlow renders live numbers and honors reduced-motion | RTL |
| DS-06 | A reduced-motion test collapses animation to instant/fade | RTL + `matchMedia` mock |
| DS-07 | Static audit finds no box-shadow/backdrop-blur/continuous transform/`layout` on grid, canvas, or editor | grep |
| DS-08 | Motion imported only via `m` + `LazyMotion` (`strict`); no full `motion` component | grep |
| DS-09 | Skeletons are the loading treatment; no spinners on data surfaces | grep |
| DS-10 | Six states present per interactive element with one focus-ring token | visual + RTL |
| DS-11 | Density switcher toggles condensed/regular/relaxed row heights | RTL |
| DS-12 | Budgets hold: 0 ms optimistic, <16.6 ms / 60 fps, <10 ms keyboard, `bufferTime(50)` | perf assertions |

---

## 6. Anti-Cheating & Integrity Guardrails

1. Components reference semantic tokens only — no magic numbers, raw hex, or literal pixel values; every color and dimension resolves through `@theme`.
2. Motion enters exclusively through `m` + `LazyMotion` with `strict`; the full `motion` component import is forbidden and fails the grep gate.
3. No box-shadow, backdrop-blur, continuous transforms, `layout`, or `AnimatePresence` on grid, canvas, editor, or any interactive hot surface.
4. NumberFlow does not roll on every cell every tick — dense virtualized rows gate by viewport and meaningful change and prefer a color flash.
5. `prefers-reduced-motion` is honored everywhere via `MotionConfig reducedMotion="user"`, NumberFlow defaults, and the CSS `@media` reset; the in-app toggle mirrors it.
6. Loading states use layout-matched skeletons; spinners are not introduced on data surfaces.
7. Dark mode is produced by swapping lightness, never by inverting the ramp or introducing off-scale hues.
8. The existing 0 ms optimistic, <16.6 ms / 60 fps, <10 ms keyboard, and `bufferTime(50)` budgets are preserved unchanged and never weakened to accommodate visuals or motion.
