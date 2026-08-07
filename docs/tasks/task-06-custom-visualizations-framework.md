# Task 06: Custom Visualizations Framework — ChartAdapter Contract & Chart Engines

> **DESCOPE NOTICE (2026-08-06 — applied in this revision)** — per [`architecture/decision-log.md`](../architecture/decision-log.md) **D8**: the primary deliverable is the **`ChartAdapter` contract** (series/data spec, lifecycle `mount/update/resize/setTheme/destroy`, theming tokens, registry by chart type, LTTB downsampling hook) with two engines behind it: Apache ECharts (default) and TradingView Lightweight Charts (price/candles). Tremor is a KPI component kit, not an engine tier. **Deleted**: `WebGLContextPoolManager` and the 512MB `ClientMemoryGovernor` — neither engine used WebGL as configured and the governor could not measure what it claimed to govern; also deleted the custom off-thread canvas renderer (ECharts covers those cases; LTTB workers remain). Adopted Task 04's contract: endpoint `POST /api/v1/analytics/query` and Task 04's AST (this file's old `SemanticQueryAST` with `not_equals`/`between`/`second` is void, D11/ownership matrix).

- **Phase**: Visualizations & Analytics Engine (Phase 4)
- **Lead / Owner**: Frontend Visualization Specialist
- **Complexity**: Medium
- **Prerequisites**: Task 04 (Dynamic Semantic Query Layer — canonical AST + endpoint), Task 05 (React 19 UI foundations, test root convention)
- **Target Files**:
  - `src/Frontend/src/types/visualizations.ts`
  - `src/Frontend/src/types/dashboardSchema.json`
  - `src/Frontend/src/lib/charts/adapterRegistry.ts`
  - `src/Frontend/src/lib/charts/echartsAdapter.ts`
  - `src/Frontend/src/lib/charts/lightweightChartsAdapter.ts`
  - `src/Frontend/src/lib/charts/tremorKpiAdapter.tsx`
  - `src/Frontend/src/lib/workers/workerPool.ts`
  - `src/Frontend/src/workers/lttbDownsample.worker.ts`
  - `src/Frontend/src/hooks/useChartAdapter.ts`
  - `src/Frontend/src/components/visualizations/ChartHost.tsx`
  - `src/Frontend/src/components/visualizations/VisualEncodingMapper.ts`
  - `src/Frontend/src/components/visualizations/QueryBindingConfigurator.tsx`
  - `src/Frontend/src/components/dashboard/DashboardGrid.tsx`
  - `src/Frontend/tests/charts/adapterRegistry.test.ts`
  - `src/Frontend/tests/charts/adapterLifecycle.test.ts`
  - `src/Frontend/tests/workers/lttbDownsample.test.ts`

---

## 1. Detailed Scope & Feature Coverage

### 1.1 Architectural Vision & Objectives
The future-proofing deliverable is a single **`ChartAdapter` contract**: every chart on every dashboard mounts, updates, resizes, themes, and destroys through the same five-method interface, dispatched by a registry keyed on chart type. Engines are implementation details behind adapters — swapping or adding an engine touches one adapter module, never widget or dashboard code.

Heavy series are downsampled off the main thread: any series above 5,000 points passes through a Web Worker running Largest-Triangle-Three-Buckets (LTTB) before it reaches an engine.

### 1.2 Engines & Component Kits (replaces the old 3-tier model)

| Role | Library | Notes |
| :--- | :--- | :--- |
| **Default engine** | Apache ECharts | 2D canvas renderer only (`renderer: 'canvas'`, `useDirtyRect: true`). No GPU-context renderer is configured; reintroduce pooling/governance only if one (e.g. `echarts-gl`) ever enters. |
| **Price/candlestick engine** | TradingView Lightweight Charts | ~45KB, self-contained. **Pinned to major v5**; uses the v5 series API `chart.addSeries(CandlestickSeries, options)` — the v4 `addCandlestickSeries()` method was removed in v5. |
| **KPI component kit** | Tremor (`@tremor/react`) | A React component kit, **not** an engine. Wrapped as adapters for KPI cards so KPI widgets share the registry/lifecycle. |

### 1.3 Off-Main-Thread Downsampling
- **Rule**: series **>5,000 points MUST pass the worker LTTB downsampler** before `update()` is called on any adapter.
- **Protocol**: `postMessage` with **`Float64Array` transferables — two arrays, `x[]` and `y[]`** — in both directions. No structured-clone copies of point objects anywhere in the pipeline.
- **Pool**: one **shared** worker pool for the whole app, sized `max(1, navigator.hardwareConcurrency - 1)`. Never one worker per widget.
- The previous custom `OffscreenCanvas` renderer worker and its hook are dropped — every case it served is covered by ECharts. The LTTB worker pipeline remains.

### 1.4 Resource Lifecycle Rules (replaces the deleted governance modules)
Enforceable rules, checked by tests:
1. Every adapter MUST have `destroy()` called on unmount, and `destroy()` MUST dispose the engine instance (`echarts` `dispose()`; Lightweight Charts `remove()`). Empty cleanup functions are prohibited.
2. The >5,000-point downsampling rule of §1.3.
3. All widgets share the single worker pool of §1.3.

### 1.5 Dynamic Dashboard Framework & Semantic Layer Binding
- **Grid**: `react-grid-layout`, responsive 12-column layout (`lg: 1200`, `md: 996`, `sm: 768`).
- **Query binding**: widgets hold a Task 04 `JsonQueryAst` and execute it via **`POST /api/v1/analytics/query`** (JSON in, `{ columns, rows }` out). Task 04's `semanticAst.ts` types are imported directly — this task defines **no** AST types of its own.
- **Persistence & validation**: dashboard/widget definitions serialize to JSON and persist in PostgreSQL (`workspace_dashboards.layout_json`). The backend validates every save against `dashboardSchema.json` — a strict schema whose `chartType` enum lists exactly the allowed chart types. Unknown adapter keys are rejected with 400.

### 1.6 Plugin Story
**The adapter registry IS the plugin mechanism (D8).** A new chart type ships as an adapter factory registered at build time. There is no runtime plugin loader, no sandbox, and no arbitrary plugin references — the old `CUSTOM_PLUGIN`/`pluginRef` mechanism is deleted (no loader or sandbox ever existed for it).

---

## 2. Key Deliverables & File Layout

```
src/Frontend/src/
├── types/
│   ├── visualizations.ts        # ChartAdapter, ChartSpec, SeriesData, ThemeTokens, widget/dashboard types
│   └── dashboardSchema.json     # strict JSON Schema for persisted dashboards (chartType enum)
├── lib/
│   ├── charts/
│   │   ├── adapterRegistry.ts          # registry keyed by chart type — the plugin mechanism
│   │   ├── echartsAdapter.ts           # default engine adapter (2D canvas)
│   │   ├── lightweightChartsAdapter.ts # candlestick adapter (Lightweight Charts v5 API)
│   │   └── tremorKpiAdapter.tsx        # Tremor KPI card wrapped as an adapter
│   └── workers/
│       └── workerPool.ts               # shared LTTB pool: max(1, hardwareConcurrency - 1)
├── workers/
│   └── lttbDownsample.worker.ts        # LTTB worker; Float64Array x[]/y[] transferables
├── hooks/
│   └── useChartAdapter.ts              # mount/resize/theme/destroy lifecycle + downsample routing
└── components/
    ├── visualizations/
    │   ├── ChartHost.tsx               # binds a widget config to a mounted adapter
    │   ├── VisualEncodingMapper.ts     # query result -> SeriesData / engine options
    │   └── QueryBindingConfigurator.tsx# metric picker & encoding binding UI
    └── dashboard/
        └── DashboardGrid.tsx           # react-grid-layout dashboard

src/Frontend/tests/                     # test root shared with Task 05
├── charts/
│   ├── adapterRegistry.test.ts
│   └── adapterLifecycle.test.ts
└── workers/
    └── lttbDownsample.test.ts
```

**Dependencies** (pinned major versions): `echarts@^6.1.0` (the maintained v6 line; the adapter was revalidated against its migration contract), `lightweight-charts@^5.0.0` (**pinned v5**), `@tremor/react@^3.18.0`, `react-grid-layout@^1.4.0`; from Task 05: `@tanstack/react-query@^5.0.0`, `@tanstack/react-table@^8.0.0` (TABLE widgets reuse the virtualized table).

---

## 3. Architecture & Code Contract Blueprints

### 3.1 Type Definitions (`src/Frontend/src/types/visualizations.ts`)

```typescript
import type { JsonQueryAst } from './semanticAst'; // Task 04 canonical AST — no local variant

export type ChartType =
  | 'KPI_CARD'
  | 'SPARK_LINE'
  | 'LINE'
  | 'AREA'
  | 'BAR'
  | 'STACKED_BAR'
  | 'SCATTER'
  | 'HEATMAP'
  | 'CANDLESTICK'
  | 'TABLE';

export interface ThemeTokens {
  background: string;
  textPrimary: string;
  textSecondary: string;
  gridLine: string;
  axisLine: string;
  seriesPalette: string[];
  positive: string;
  negative: string;
  fontFamily: string;
}

export interface SeriesData {
  series: Array<{
    name: string;
    /** Category labels or epoch-ms timestamps. */
    x: Array<string | number>;
    /** Numeric values; Float64Array when produced by the LTTB worker. */
    y: Float64Array | number[];
  }>;
  /** OHLC rows for CANDLESTICK charts; produced by VisualEncodingMapper. */
  ohlc?: Array<{ time: number; open: number; high: number; low: number; close: number }>;
}

export interface VisualEncodingSpec {
  xAxis: string;
  yAxis: string[];
  colorBy?: string;
  sizeBy?: string;
  tooltipFields?: string[];
}

export interface WidgetStyleOverrides {
  showLegend?: boolean;
  showGridlines?: boolean;
  strokeWidth?: number;
  opacity?: number;
}

export interface ChartSpec {
  chartType: ChartType;
  encodings: VisualEncodingSpec;
  style?: WidgetStyleOverrides;
}

/** The deliverable contract (decision-log D8). Every chart implements exactly this. */
export interface ChartAdapter {
  mount(el: HTMLElement, spec: ChartSpec): void;
  update(data: SeriesData): void;
  resize(): void;
  setTheme(tokens: ThemeTokens): void;
  destroy(): void;
}

export interface ChartWidgetConfig {
  id: string;
  title: string;
  chartType: ChartType;
  semanticModelRef: string;
  queryAst: JsonQueryAst;              // Task 04 contract, verbatim
  visualEncodings: VisualEncodingSpec;
  styleOverrides?: WidgetStyleOverrides;
}

export interface DashboardGridItem {
  widgetId: string;
  x: number;
  y: number;
  w: number;
  h: number;
  minW?: number;
  minH?: number;
  static?: boolean;
}

export interface DashboardSpecification {
  dashboardId: string;
  title: string;
  description?: string;
  version: number;
  theme: 'DARK' | 'LIGHT' | 'SYSTEM';
  refreshRateMs: number;
  gridLayout: {
    columns: number;
    rowHeight: number;
    items: DashboardGridItem[];
  };
  widgets: ChartWidgetConfig[];
}
```

### 3.2 Adapter Registry (`src/Frontend/src/lib/charts/adapterRegistry.ts`)

```typescript
import type { ChartAdapter, ChartType } from '../../types/visualizations';

export type ChartAdapterFactory = () => ChartAdapter;

/**
 * The registry IS the plugin mechanism (decision-log D8): a new chart type
 * ships as an adapter factory registered here at build time. There is no
 * runtime plugin loader and no sandbox.
 */
class ChartAdapterRegistry {
  private factories = new Map<ChartType, ChartAdapterFactory>();

  register(type: ChartType, factory: ChartAdapterFactory): void {
    this.factories.set(type, factory);
  }

  create(type: string): ChartAdapter {
    const factory = this.factories.get(type as ChartType);
    if (!factory) {
      // Never silently fall back to a default adapter.
      throw new Error(`Unknown chart adapter key '${type}'.`);
    }
    return factory();
  }

  has(type: string): boolean {
    return this.factories.has(type as ChartType);
  }

  registeredTypes(): ChartType[] {
    return Array.from(this.factories.keys());
  }
}

export const chartAdapterRegistry = new ChartAdapterRegistry();
```

Registration wiring (app bootstrap): ECharts adapter → `LINE`, `AREA`, `BAR`, `STACKED_BAR`, `SCATTER`, `HEATMAP`, `SPARK_LINE`; Lightweight Charts adapter → `CANDLESTICK`; Tremor adapter → `KPI_CARD`; TABLE → a thin adapter around Task 05's `VirtualizedDataTable`.

### 3.3 ECharts Adapter (`src/Frontend/src/lib/charts/echartsAdapter.ts`)

```typescript
import * as echarts from 'echarts';
import type { ChartAdapter, ChartSpec, SeriesData, ThemeTokens } from '../../types/visualizations';
import { toEChartsOption } from '../../components/visualizations/VisualEncodingMapper';

export class EChartsAdapter implements ChartAdapter {
  private chart: echarts.ECharts | null = null;
  private spec: ChartSpec | null = null;
  private tokens: ThemeTokens | null = null;
  private lastData: SeriesData | null = null;

  mount(el: HTMLElement, spec: ChartSpec): void {
    if (this.chart) return; // idempotent under React StrictMode double-invoke
    this.spec = spec;
    // 2D canvas renderer only — no GPU-context renderer is configured (D8).
    this.chart = echarts.init(el, undefined, { renderer: 'canvas', useDirtyRect: true });
  }

  update(data: SeriesData): void {
    if (!this.chart || !this.spec) return;
    this.lastData = data;
    this.chart.setOption(toEChartsOption(this.spec, data, this.tokens));
  }

  resize(): void {
    this.chart?.resize();
  }

  setTheme(tokens: ThemeTokens): void {
    this.tokens = tokens;
    if (this.lastData) this.update(this.lastData);
  }

  destroy(): void {
    if (this.chart && !this.chart.isDisposed()) this.chart.dispose();
    this.chart = null;
  }
}
```

### 3.4 Lightweight Charts Adapter (`src/Frontend/src/lib/charts/lightweightChartsAdapter.ts`)

```typescript
// lightweight-charts is PINNED to major v5. The v5 series API is
// chart.addSeries(SeriesDefinition, options); v4's addCandlestickSeries()
// was removed in v5 and must not appear anywhere in this codebase.
import {
  createChart,
  CandlestickSeries,
  IChartApi,
  ISeriesApi
} from 'lightweight-charts';
import type { ChartAdapter, ChartSpec, SeriesData, ThemeTokens } from '../../types/visualizations';

export class LightweightChartsAdapter implements ChartAdapter {
  private el: HTMLElement | null = null;
  private chart: IChartApi | null = null;
  private series: ISeriesApi<'Candlestick'> | null = null;

  mount(el: HTMLElement, _spec: ChartSpec): void {
    if (this.chart) return; // idempotent under StrictMode double-invoke
    this.el = el;
    this.chart = createChart(el, {
      width: el.clientWidth || 500,
      height: el.clientHeight || 300
    });
    this.series = this.chart.addSeries(CandlestickSeries, {
      upColor: '#10B981',
      downColor: '#EF4444',
      borderVisible: false,
      wickUpColor: '#10B981',
      wickDownColor: '#EF4444'
    });
  }

  update(data: SeriesData): void {
    if (!data.ohlc) return;
    this.series?.setData(
      data.ohlc.map((r) => ({
        time: (r.time / 1000) as never, // epoch seconds
        open: r.open, high: r.high, low: r.low, close: r.close
      }))
    );
  }

  resize(): void {
    if (this.el && this.chart) {
      this.chart.applyOptions({ width: this.el.clientWidth, height: this.el.clientHeight });
    }
  }

  setTheme(tokens: ThemeTokens): void {
    this.chart?.applyOptions({
      layout: { background: { color: tokens.background }, textColor: tokens.textPrimary },
      grid: {
        vertLines: { color: tokens.gridLine },
        horzLines: { color: tokens.gridLine }
      }
    });
  }

  destroy(): void {
    this.chart?.remove();
    this.chart = null;
    this.series = null;
  }
}
```

### 3.5 Tremor KPI Adapter (`src/Frontend/src/lib/charts/tremorKpiAdapter.tsx`)

```tsx
import React from 'react';
import { createRoot, Root } from 'react-dom/client';
import { Card, Metric, Text } from '@tremor/react';
import type { ChartAdapter, ChartSpec, SeriesData, ThemeTokens } from '../../types/visualizations';

/**
 * Tremor is a React component kit, not a chart engine — this adapter wraps its
 * KPI card primitives so KPI widgets participate in the same registry/lifecycle.
 */
export class TremorKpiAdapter implements ChartAdapter {
  private root: Root | null = null;
  private spec: ChartSpec | null = null;
  private lastData: SeriesData | null = null;

  mount(el: HTMLElement, spec: ChartSpec): void {
    if (this.root) return; // idempotent under StrictMode double-invoke
    this.spec = spec;
    this.root = createRoot(el);
  }

  update(data: SeriesData): void {
    this.lastData = data;
    this.render();
  }

  resize(): void { /* Tremor cards are fluid — no imperative resize needed */ }

  setTheme(_tokens: ThemeTokens): void {
    this.render();
  }

  destroy(): void {
    this.root?.unmount();
    this.root = null;
  }

  private render(): void {
    if (!this.root || !this.lastData) return;
    const series = this.lastData.series[0];
    const value = series && series.y.length > 0 ? series.y[series.y.length - 1] : null;
    this.root.render(
      <Card>
        <Text>{this.spec?.encodings.yAxis[0] ?? ''}</Text>
        <Metric>{value ?? '—'}</Metric>
      </Card>
    );
  }
}
```

### 3.6 LTTB Downsample Worker (`src/Frontend/src/workers/lttbDownsample.worker.ts`)

Worker protocol — `Float64Array` transferables in **both** directions, the single protocol for the whole file:

```
Request : { requestId, x: Float64Array, y: Float64Array, threshold }   transfer: [x.buffer, y.buffer]
Response: { requestId, x: Float64Array, y: Float64Array }              transfer: [x.buffer, y.buffer]
```

```typescript
export interface LttbRequest {
  requestId: string;
  x: Float64Array;
  y: Float64Array;
  threshold: number;
}

/**
 * Largest-Triangle-Three-Buckets over parallel x/y arrays.
 * Guarantees: first and last input points are retained; output x stays
 * monotonically increasing (input order preserved); output length === threshold.
 * NOTE: LTTB does NOT guarantee global max/min retention — do not assert it.
 */
export function lttbDownsample(
  x: Float64Array,
  y: Float64Array,
  threshold: number
): { x: Float64Array; y: Float64Array } {
  const n = x.length;
  if (threshold >= n || threshold < 3) return { x, y };

  const outX = new Float64Array(threshold);
  const outY = new Float64Array(threshold);
  let sampledIndex = 0;

  // Bucket size excluding the fixed first and last points
  const every = (n - 2) / (threshold - 2);

  let a = 0;
  outX[sampledIndex] = x[0];
  outY[sampledIndex] = y[0];
  sampledIndex++;

  for (let i = 0; i < threshold - 2; i++) {
    // Average point of the next bucket (bucket c)
    let avgX = 0;
    let avgY = 0;
    let avgRangeStart = Math.floor((i + 1) * every) + 1;
    const avgRangeEnd = Math.min(Math.floor((i + 2) * every) + 1, n);
    const avgRangeLength = avgRangeEnd - avgRangeStart;

    for (; avgRangeStart < avgRangeEnd; avgRangeStart++) {
      avgX += x[avgRangeStart];
      avgY += y[avgRangeStart];
    }
    avgX /= avgRangeLength;
    avgY /= avgRangeLength;

    // Current bucket range (bucket b)
    let rangeOffs = Math.floor(i * every) + 1;
    const rangeTo = Math.floor((i + 1) * every) + 1;

    const pointAX = x[a];
    const pointAY = y[a];

    let maxArea = -1;
    let nextA = rangeOffs;

    for (; rangeOffs < rangeTo; rangeOffs++) {
      // Triangle area: 0.5 * |cross product of (a->avg) and (a->candidate)|
      const area = Math.abs(
        (pointAX - avgX) * (y[rangeOffs] - pointAY) -
        (pointAX - x[rangeOffs]) * (avgY - pointAY)
      ) * 0.5;
      if (area > maxArea) {
        maxArea = area;
        nextA = rangeOffs;
      }
    }

    outX[sampledIndex] = x[nextA];
    outY[sampledIndex] = y[nextA];
    sampledIndex++;
    a = nextA;
  }

  outX[sampledIndex] = x[n - 1]; // always retain the final point
  outY[sampledIndex] = y[n - 1];
  return { x: outX, y: outY };
}

self.onmessage = (e: MessageEvent<LttbRequest>) => {
  const { requestId, x, y, threshold } = e.data;
  const result = lttbDownsample(x, y, threshold);
  (self as unknown as Worker).postMessage(
    { requestId, x: result.x, y: result.y },
    [result.x.buffer, result.y.buffer] // transferables — no structured-clone copy
  );
};
```

### 3.7 Shared Worker Pool (`src/Frontend/src/lib/workers/workerPool.ts`)

```typescript
/**
 * ONE shared pool for all chart widgets — never one worker per widget.
 * Size: max(1, navigator.hardwareConcurrency - 1).
 */
export class LttbWorkerPool {
  private workers: Worker[] = [];
  private nextWorker = 0;
  private pending = new Map<string, (r: { x: Float64Array; y: Float64Array }) => void>();

  constructor() {
    const size = Math.max(1, (navigator.hardwareConcurrency || 2) - 1);
    for (let i = 0; i < size; i++) {
      const worker = new Worker(
        new URL('../../workers/lttbDownsample.worker.ts', import.meta.url),
        { type: 'module' }
      );
      worker.onmessage = (e: MessageEvent) => {
        const { requestId, x, y } = e.data;
        this.pending.get(requestId)?.({ x, y });
        this.pending.delete(requestId);
      };
      this.workers.push(worker);
    }
  }

  downsample(
    x: Float64Array,
    y: Float64Array,
    threshold: number
  ): Promise<{ x: Float64Array; y: Float64Array }> {
    const requestId = crypto.randomUUID();
    const worker = this.workers[this.nextWorker];
    this.nextWorker = (this.nextWorker + 1) % this.workers.length;
    return new Promise((resolve) => {
      this.pending.set(requestId, resolve);
      worker.postMessage({ requestId, x, y, threshold }, [x.buffer, y.buffer]);
    });
  }
}

export const lttbWorkerPool = new LttbWorkerPool();
```

### 3.8 Lifecycle Hook (`src/Frontend/src/hooks/useChartAdapter.ts`)

```typescript
import { useEffect, useRef } from 'react';
import { chartAdapterRegistry } from '../lib/charts/adapterRegistry';
import { lttbWorkerPool } from '../lib/workers/workerPool';
import type { ChartAdapter, ChartWidgetConfig, SeriesData, ThemeTokens } from '../types/visualizations';

const DOWNSAMPLE_THRESHOLD_POINTS = 5_000;

export function useChartAdapter(
  widget: ChartWidgetConfig,
  data: SeriesData | null,
  tokens: ThemeTokens
) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const adapterRef = useRef<ChartAdapter | null>(null);

  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;
    const adapter = chartAdapterRegistry.create(widget.chartType);
    adapter.mount(el, {
      chartType: widget.chartType,
      encodings: widget.visualEncodings,
      style: widget.styleOverrides
    });
    adapterRef.current = adapter;

    const resizeObserver = new ResizeObserver(() => adapter.resize());
    resizeObserver.observe(el);

    // MANDATORY: destroy() on unmount (§1.4). Effect cleanup plus idempotent
    // mount() keeps this correct under React StrictMode's double-invoke.
    return () => {
      resizeObserver.disconnect();
      adapter.destroy();
      adapterRef.current = null;
    };
  }, [widget.id, widget.chartType]);

  useEffect(() => {
    adapterRef.current?.setTheme(tokens);
  }, [tokens]);

  useEffect(() => {
    if (!data) return;
    void (async () => {
      const prepared = await downsampleIfNeeded(data);
      adapterRef.current?.update(prepared);
    })();
  }, [data]);

  return { containerRef };
}

/** Series above the threshold MUST go through the shared worker pool (§1.3). */
async function downsampleIfNeeded(data: SeriesData): Promise<SeriesData> {
  const series = await Promise.all(
    data.series.map(async (s) => {
      if (s.y.length <= DOWNSAMPLE_THRESHOLD_POINTS) return s;
      const x = Float64Array.from(s.x as number[]);
      const y = s.y instanceof Float64Array ? s.y : Float64Array.from(s.y);
      const out = await lttbWorkerPool.downsample(x, y, DOWNSAMPLE_THRESHOLD_POINTS);
      return { ...s, x: Array.from(out.x), y: out.y };
    })
  );
  return { ...data, series };
}
```

### 3.9 Query Binding, Encoding Mapper & Persisted-Dashboard Validation

- **`ChartHost.tsx`**: fetches widget data with TanStack Query — `POST /api/v1/analytics/query` with `widget.queryAst` (Task 04 contract; the API client wrapper from Task 05 attaches the JWT) — then feeds the result through the mapper into `useChartAdapter`.
- **`VisualEncodingMapper.ts`**:
  - `toSeriesData(result: { columns: string[]; rows: unknown[][] }, encodings: VisualEncodingSpec): SeriesData` — maps result columns to `x`/`y` series (and OHLC rows for CANDLESTICK).
  - `toEChartsOption(spec: ChartSpec, data: SeriesData, tokens: ThemeTokens | null): echarts.EChartsOption`.
- **`dashboardSchema.json`** (excerpt) — the backend validates every dashboard save against this schema; a `chartType` outside the enum → 400 rejection:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "DashboardSpecification",
  "type": "object",
  "required": ["dashboardId", "title", "version", "gridLayout", "widgets"],
  "properties": {
    "widgets": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["id", "title", "chartType", "semanticModelRef", "queryAst", "visualEncodings"],
        "properties": {
          "chartType": {
            "enum": ["KPI_CARD", "SPARK_LINE", "LINE", "AREA", "BAR", "STACKED_BAR",
                     "SCATTER", "HEATMAP", "CANDLESTICK", "TABLE"]
          }
        },
        "additionalProperties": false
      }
    }
  }
}
```

Client-side, `chartAdapterRegistry.create()` throws on unknown keys (no silent fallback) — covering rows persisted before the schema tightened.

---

## 4. Subagent Implementation Step-by-Step Workflow

```
1. Contract & Engines   -> types/visualizations.ts, adapterRegistry.ts,
                           echartsAdapter.ts, lightweightChartsAdapter.ts (v5 API)
2. Worker Pipeline      -> lttbDownsample.worker.ts (Float64Array protocol), workerPool.ts
3. KPI & Table Adapters -> tremorKpiAdapter.tsx, TABLE adapter, useChartAdapter.ts
4. Binding & Dashboard  -> VisualEncodingMapper, ChartHost, QueryBindingConfigurator,
                           DashboardGrid, dashboardSchema.json + server-side validation
5. Tests & Baselines    -> §5 suites; record render-timing baselines (D10)
```

---

## 5. Independent Verification & Acceptance Workflow

```bash
# 1. TypeScript compile check
cd src/Frontend
npx tsc --noEmit

# 2. Unit tests (test root: src/Frontend/tests/)
npx vitest run tests/charts tests/workers

# 3. Production bundle build
npm run build
```

### Acceptance Criteria

| Criteria | Requirement | Verification |
| :--- | :--- | :--- |
| **Adapter lifecycle** | `mount → update → resize → setTheme → destroy` works on every registered adapter; `destroy()` disposes the engine instance (ECharts `isDisposed()` true; Lightweight Charts `remove()` called) | `adapterLifecycle.test.ts` |
| **Registry rejection** | `create('UNKNOWN')` throws; no silent fallback adapter | `adapterRegistry.test.ts` |
| **LTTB correctness** | First and last input points retained; output `x` monotonically increasing; output length `=== threshold`. Global max/min retention is **not** asserted — LTTB does not guarantee it | `lttbDownsample.test.ts` |
| **Downsample routing** | A series above 5,000 points reaches the adapter only via the shared worker pool (pool spy asserts invocation; adapter receives `threshold`-length series) | `adapterLifecycle.test.ts` |
| **Persisted-dashboard validation** | Saving a dashboard whose `chartType` is outside the schema enum returns 400 | backend integration test (Task 02 test project) |
| **Build** | `npx tsc --noEmit` and `npm run build` pass with zero errors | commands above |
| **Performance** | No absolute FPS/latency/throughput gates (D10). A Playwright timing run records measured render baselines on a documented reference machine into `src/Frontend/tests/baselines/chartRender.baseline.json`; a later run more than 20% below its recorded baseline fails | baseline runner |

---

## 6. Anti-Cheating & Integrity Guardrails

1. **NO hardcoded LTTB returns**: the worker must execute genuine triangle-area calculations over the incoming arrays; static pre-calculated outputs are forbidden.
2. **NO facade adapters**: adapters must instantiate the real engines (`echarts.init`, `createChart`). A stub adapter that renders nothing to satisfy lifecycle tests is a violation.
3. **NO leaked engine instances**: `destroy()` must genuinely dispose (`dispose()` / `remove()` / `unmount()`); empty `useEffect` cleanup functions are prohibited.
4. **NO main-thread downsampling** for series above the 5,000-point threshold — the shared worker pool is the only permitted path; synchronous loops masquerading as worker calls will be caught by the pool-spy test.
5. **NO divergent AST or endpoint**: widgets must import Task 04's `semanticAst.ts` and call `POST /api/v1/analytics/query`. Declaring a local query-AST type or a different analytics route violates the contract ownership matrix.
6. **NO silent adapter fallback**: unknown chart-type keys must throw client-side and be rejected server-side by the schema.

*Task specification updated for the 2026-08-06 de-scope (decision-log D8/D10/D11).*
