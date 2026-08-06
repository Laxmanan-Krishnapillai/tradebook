# Frontend Library Evaluation — OpenUI & json-render

**Date**: August 5, 2026
**Evaluated against**: Tradebook v2 bootstrap scope — React 19 + Vite + TanStack Query v5 + SignalR (MessagePack) realtime, TypeGen-generated TypeScript contracts (`src/Frontend/src/types/generated/`), AI-agent-built UI with human-on-the-loop review, single entity (biotickets) CRUD.
**Method**: Two independent parallel research agents, one per library (live GitHub/docs review).

> ⚠️ Note: this doc's scope ("Tradebook v2 bootstrap", biotickets-only CRUD, `tasks/README-v2-bootstrap.md`) doesn't match the Iteration 3 master task breakdown in `tasks/README.md` (10-task full platform breakdown) — appears to be from a separate/earlier bootstrap-scoped initiative. `tasks/README-v2-bootstrap.md` does not exist in this repo.

---

## 1. Verdict at a Glance

| Library | What it is | Status / License | Verdict for bootstrap | Primary reason |
|---|---|---|---|---|
| [openui](https://github.com/thesysdev/openui) | Generative UI — LLM composes UI at runtime via "OpenUI Lang" DSL streamed to a `<Renderer>` | Pre-1.0, active, MIT | **Avoid** | Output is a runtime dependency (LLM-authored per session), not committed source — inverts the agent-writes-code/human-reviews workflow |
| [json-render](https://github.com/vercel-labs/json-render) | Generative UI — LLM emits a JSON element tree constrained to an allowlisted Zod catalog; `<Renderer>` renders safely; `@json-render/codegen` exports specs as standalone React | Pre-1.0 (0.19.0, ~6 mo old), very active, Apache-2.0 | **Evaluate-more** (not for CRUD runtime) | Guardrailed + codegen-native + SKILL.md/MCP-first = excellent agent fit; but it is not a schema→CRUD/forms library and is too young for the core path |

**Both libraries solve "AI-authored UI" — not "deterministic data-driven CRUD."** Neither should drive the biotickets CRUD screens.

---

## 1b. Data-Visualization Re-Evaluation (round 2)

Follow-up review, **data-visualization lens** (dashboards, charts, KPIs, audit-history timelines, real-time tick updates). Both libraries re-examined for shipped chart primitives, data binding, dashboard layout, realtime fit, data-exposure risk.

| Library | Ships chart primitives? | Chart data flow | Realtime push | Raw data sent to LLM? | Viz verdict |
|---|---|---|---|---|---|
| openui | **Yes** — Recharts-based `LineChart`, `BarChart`, `AreaChart`, `RadarChart`, `HorizontalBarChart`, `PieChart`/donut, `RadialChart`, `SingleStackedBarChart`, `ScatterChart`, plus KPI `Card`/`@Count` and `Table` (sparklines exist but are not in the model library) | `Query` tool calls + reactive `$variables` (no LLM round-trip on refresh); polling auto-refresh supported; inline props also possible | **Not built-in** — polling only; SignalR bridge is DIY | **No** — the LLM emits Query references; the runtime injects results (favorable for financial data) | **Evaluate-more** — viable for ad-hoc/exploratory dashboards, wrong for production or tick surfaces |
| json-render | **None** — 36-component catalog has no chart components and no Recharts dependency; charts require hand-written custom components registered in the catalog | Inline `data` props (raw data travels through the LLM) or `$state` refs into a controlled store (zustand/redux/jotai/xstate adapters) | Possible via `store.set(...)` but re-render granularity is whole-spec/subtree — wrong for high-frequency ticks | **Yes, if using inline props** — unacceptable for financial/audit data without private deployment | **Avoid** for viz runtime |

**Key round-2 findings:**

- **openui**: real working chart primitives bound to Recharts v2 (D3 rewrite in flight, pre-1.0, flagged "no unit tests"). Data flows through `Query` tool calls — raw ticket/trading data **never sent to the model**, LLM only emits query references. Runtime reactivity (`$variables` re-trigger queries) → updates after initial render are LLM-free. Costs: ~4.9s initial full-render latency (60 tok/s benchmark), ~0.3s incremental patches, heavy bundle (Recharts + Radix + lucide + markdown), per-session non-deterministic layout, no dashboard-persistence primitive, pre-1.0 API churn.
- **json-render**: spec/registry framework, **no charting out of the box** — own demo dashboard hand-writes a `chart.tsx` wrapping Recharts and registers it. Cleanest data path (`$state` → controlled store) keeps data out of LLM but re-renders whole specs. `@json-render/codegen` is an immature scaffold exporter (full Next.js project), not a spec→committed-component pipeline. Verdict: keep out of viz path.

**Viz recommendation:** keep hand-written 3-tier component approach (KPI cards / ECharts OLAP / Lightweight Charts ticks) as committed, agent-written React components fed by TanStack Query + SignalR. If agent-authored dashboard surface ever wanted, openui is the candidate to pilot for **ad-hoc/exploratory dashboards and audit-history timelines only** — one LLM render, then client-side updates — never the production realtime dashboard.

---

## 1c. Recommended Custom Visualization Stack (2026)

Four parallel research agents evaluated current (mid-2026) landscape. Result: a **three-tier committed-component stack**, one maintained OSS dependency per tier, all MIT/Apache-2.0, data stays client-side.

| Tier | Use for | Library | Version / Status / License | Key facts |
|---|---|---|---|---|
| **Tier 1 — KPI & simple charts** | KPI cards, status pills, sparklines, simple bar/line/donut on the biotickets admin dashboard | **Recharts v3** via the **shadcn/ui `chart`** component (commit the generated `components/ui/chart.tsx`) | Recharts v3.10.x — active, MIT, native React 19 | ~50 kB gz tree-shaken; JSX-first (ideal for committed agent-written code); fine under ~5k points/chart, which this admin UI never exceeds. KPI cards/sparklines hand-rolled Tailwind (zero deps). **Tremor is sunset** (Vercel acquisition; last release Jan 2025) — use shadcn/ui as its successor, do not adopt |
| **Tier 2 — OLAP / analytics / large series** | Dashboards, 100k+ point series, time bucketing, audit-history timelines | **Apache ECharts 6** | v6.1.0 — active, Apache-2.0 | Tree-shake via `echarts/core` + `echarts.use([...])` (~150–300 kB bundle). Canvas renderer, `sampling:'lttb'`, `progressive` chunking, `dataZoom`, `appendData` for streams. Use **direct `echarts.init()` in `useEffect`** behind a thin committed wrapper (dispose + ResizeObserver) — **not** `echarts-for-react` (legacy, `option:any`) |
| **Tier 3 — Realtime ticks / candles** | Live SignalR push series (tick streams, time-ordered updates) | **TradingView Lightweight Charts v5** (primary) or **uPlot** (if raw throughput and no candles is enough) | LWC v5.2.x — active, **Apache-2.0 + NOTICE attribution required**; uPlot v1.6.x — MIT, ~17 kB gz, used by Grafana | LWC: streaming-first `series.update()`, ~35 kB, no React coupling (thin `useEffect` wrapper, `autoSize:true`). uPlot: fastest canvas (166k pts ~25 ms), columnar Float64Array, no built-in candles/tooltips — build as small plugins. ECharts as general-purpose fallback for non-time-series |

**Shared integration rules (bake into B05 Frontend):**
1. **Thin committed wrappers**, not third-party React wrappers — `useEffect` init, `dispose()`/`remove()` + ResizeObserver cleanup (StrictMode-safe), typed props.
2. **Libraries never own data** — TanStack Query cache + SignalR buffer in a ref feed the chart imperatively; rAF-throttled `setData`/`appendData`/`series.update()`, sliding-window/ring-buffer caps.
3. Realtime feeds need monotonic ordering and `resetScales:false` on incremental updates.
4. **Dropped from original plan:** Tremor (sunset), custom WebGL context-pool engine and the 8-context/512MB claims (canvas-only is simpler and honest), `echarts-for-react`, DuckDB-WASM edge (out of bootstrap scope).

**Anti-over-engineering guard:** start with Tier 1 only. Add Tier 2 when real OLAP queries exist; add Tier 3 only when realtime tick pushes are actually consumed by a screen.

---

## 2. OpenUI (thesysdev/openui)

**What it is**: generative UI framework. At runtime an LLM composes the interface in *OpenUI Lang* (compact line-oriented DSL); client-side `<Renderer>` progressively parses the stream, maps it onto a **Zod-typed component library you register**. npm packages `@openuidev/react-lang`, `@openui/react-ui`, `@openui/lang-core`, `@openui/cli`; ~1M downloads; MIT; best features (validation, observability, rollback) gated behind commercial "OpenUI Cloud".

**Applicability findings:**
- **React 19 / Vite / TS**: compatible (peer `react ^18.3 || ^19`, TS 5.9, Zod 3/4, dual ESM/CJS).
- **TanStack Query CRUD**: conceptual conflict — OpenUI's data model is "model chooses what to fetch" (MCP/tools), a second non-deterministic server-state path bypassing query-key discipline, invalidation, TypeGen types.
- **AI-agent codegen workflow**: **biggest mismatch.** UI generated per-session by the LLM in the browser → runtime dependency, not reviewable in git, not reproducible, per-render token/latency cost. Inverts committed-code + human-review loop.
- **Realtime/audit**: marginal fit. A bi-temporal audit timeline is exactly the kind of deterministic, dense UI a generative layer does worst.
- **Security**: reasonably safe by default (only registered, Zod-validated components compose; React escapes text). Residual risk via `MarkDownRenderer` (markdown→HTML XSS classic), URL/image props, any self-registered component accepting `dangerouslySetInnerHTML`.

**Verdict: Avoid for bootstrap.** Narrow optional later use: an "AI insight" chat surface fed TanStack data via props, streaming OpenUI Lang over SignalR, with markdown/URL components excluded.

---

## 3. json-render (vercel-labs/json-render)

**What it is**: generative UI framework. You define a **catalog** of allowed components + actions (Zod-validated props); an LLM generates a **JSON spec** (element tree) constrained to the catalog; `<Renderer>` renders it. Packages: `@json-render/core` (schemas/catalogs/prompts/streaming), `@json-render/react` (`StateStore`, `$state`/`$bindState` expressions), `@json-render/shadcn` (36 prebuilt shadcn/Radix components), `@json-render/codegen` (exports specs as standalone React code), `@json-render/mcp` + bundled `skills/*.md`. Apache-2.0, ~586k weekly downloads, pre-1.0 with fast API churn.

**Applicability findings:**
- **React 19 / Vite / TS**: compatible (`react ^19.2.3`, `zod ^4`, ESM+CJS+d.ts, plain npm lib, no build changes).
- **DTO→CRUD**: **no automatic generation.** Renders *specs* (catalog component trees), not raw DTO data. Forms/tables authorable as specs (Input/Select/Textarea, `$bindState`, `validateForm`, `repeat`), but no RJSF-style JSON-Schema→form/table generator, and TypeGen types won't map to specs by themselves — you'd build a catalog + spec-mapping layer.
- **AI-agent codegen**: **excellent fit — the project's core thesis.** Catalog is an agent guardrail; `catalog.prompt()` builds the system prompt; shipped `SKILL.md` files + MCP target AI clients; `@json-render/codegen` emits standalone React components with **zero runtime dependency** — ideal for commit + human review.
- **Realtime/audit**: orthogonal/no native support; SignalR can drive re-renders through `StateStore` (Redux/Zustand/Jotai adapters) or updated specs; audit history is just another catalog component.
- **Security**: strong — allowlist catalog means only your components render, props Zod-validated, no raw-HTML by default. Risk only if you author a component that inlines untrusted HTML or render unvalidated specs.
- **Gotchas**: 0.x breaking churn (spec format/props already rewritten once); `@json-render/shadcn` drags in Radix + Tailwind (a bootstrap styling decision); requires an LLM endpoint for any generative flow (bootstrap currently has none); releases can lag across packages (0.19.0 shipped core-only).

**Verdict: Evaluate-more — do not adopt as the CRUD runtime.** Guardrail- and codegen-first design is a superb fit for agent-built UI, but it's a ~6-month-old 0.x framework and not a schema-driven CRUD/forms library.

---

## 4. Recommendation for the Tradebook Bootstrap

1. **Build biotickets CRUD deterministically** — React 19 + TanStack Query + TypeGen types + shadcn (or equivalent), SignalR-driven invalidation. Resilient path, matches "single source of truth" contract discipline from adversarial review (`review/adversarial-tasklist-review.md`).
2. **Build data visualization deterministically** — committed React chart components fed by TanStack Query + SignalR, using the three-tier stack in §1c: Recharts v3 (via shadcn `chart`) for KPI/simple charts, Apache ECharts 6 (tree-shaken, direct-init wrapper) for OLAP/large series, TradingView Lightweight Charts v5 (or uPlot) for realtime ticks. No generative layer on production/realtime dashboard path. Start Tier 1 only, add tiers when actually needed.
3. **Pilot json-render later, in codegen mode only**, for a scoped AI-authored surface (e.g. reporting/dashboard widgets or ticket-summary cards):
   - Catalog of 10–15 shadcn components + typed actions mapped to existing API calls.
   - Emit specs → export via `@json-render/codegen` → **commit generated React code** (reproducible, reviewable, zero runtime dependency).
   - Treat every spec as data (validate before render), pin versions, keep escape hatch to plain React.
   - Note: for visualization specifically, json-render ships **no chart components** — must register your own, inline `data` props leak raw data to the model, so bind charts via `$state`/controlled stores only. **Avoid** for viz path.
4. **Skip openui for CRUD**, but keep as the one generative candidate worth a pilot for **ad-hoc/exploratory dashboards and audit-history timelines** — ships real chart primitives, keeps raw data out of the LLM via `Query` tool calls. Scope to one LLM render + client-side updates; never the production realtime surface. Reassess pre-1.0 chart-engine churn before committing.
5. **Neither library changes the bootstrap tasklist** (B05 Frontend) — both would be additive post-bootstrap experiments.

---

*Related: adversarial review of the original tasklist — `review/adversarial-tasklist-review.md`; bootstrap roadmap — `tasks/README-v2-bootstrap.md`.*
