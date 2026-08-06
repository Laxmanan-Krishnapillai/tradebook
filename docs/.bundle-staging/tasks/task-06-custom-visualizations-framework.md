# Task 06: Plug-and-Play Custom Visualizations & OffscreenCanvas Web Worker Pipeline

- **Phase**: Visualizations & Analytics Engine (Phase 4)
- **Lead / Owner**: Frontend Visualization Specialist
- **Complexity**: High
- **Prerequisites**: Task 04 (Dynamic Semantic Layer & dbt/Cube Analytical Query Pipeline), Task 05 (React 19 Keyboard-First Snappy CRUD UI & TanStack Local Sync Architecture)
- **Target Files**:
  - `src/Frontend/src/types/visualizations.ts`
  - `src/Frontend/src/types/dashboardSchema.json`
  - `src/Frontend/src/lib/governance/WebGLContextPoolManager.ts`
  - `src/Frontend/src/lib/governance/ClientMemoryGovernor.ts`
  - `src/Frontend/src/workers/lttbDownsample.worker.ts`
  - `src/Frontend/src/workers/offscreenCanvasRenderer.worker.ts`
  - `src/Frontend/src/hooks/useOffscreenCanvasChart.ts`
  - `src/Frontend/src/hooks/useManagedChartLifecycle.ts`
  - `src/Frontend/src/components/visualizations/TremorKpiCard.tsx`
  - `src/Frontend/src/components/visualizations/EChartsWrapper.tsx`
  - `src/Frontend/src/components/visualizations/LightweightChartsWrapper.tsx`
  - `src/Frontend/src/components/visualizations/WidgetRegistry.ts`
  - `src/Frontend/src/components/visualizations/VisualEncodingMapper.ts`
  - `src/Frontend/src/components/visualizations/QueryBindingConfigurator.tsx`
  - `src/Frontend/src/components/dashboard/DashboardGrid.tsx`
  - `tests/Frontend/governance/WebGLContextPoolManager.test.ts`
  - `tests/Frontend/governance/ClientMemoryGovernor.test.ts`
  - `tests/Frontend/workers/lttbDownsample.test.ts`

---

## 1. Detailed Scope & Feature Coverage

### 1.1 Architectural Vision & Objectives
Tradebook needs multi-tiered, hardware-accelerated visual analytics framework serving executive dashboard summaries and high-density financial trading streams. To maintain continuous 60 FPS UI performance without main-thread freezing during high-frequency updates, downsampling and render draw commands must offload from main React thread via Web Workers and `OffscreenCanvas`.

Browser environments enforce strict memory/WebGL context constraints (e.g. Chrome caps active WebGL contexts per domain). Uncontrolled canvas creation causes silent WebGL context loss, memory bloat, browser tab crashes. Task specifies complete client-side visualization pipeline: GPU context pooling, memory governance, off-thread downsampling, dynamic widget binding to Task 04's Semantic Data Layer.

### 1.2 3-Tier Chart Engine Architecture
To balance aesthetics, computational efficiency, high-frequency stream performance, visualization layer structures into 3 tiers:

1. **Tier 1: Executive KPI Cards & Summary Metrics (Tremor / Tailwind CSS)**
   - Target use case: high-level executive summaries, portfolio total values, daily PnL callouts, mini trend sparklines, delta badges.
   - Technology stack: Tremor primitives wrapped around Tailwind CSS v4 and lightweight SVG elements.
   - Performance profile: instant load (<5ms/card), low DOM node footprint, responsive to container resize.

2. **Tier 2: Core Analytical Multi-Axis Hypercubes (Apache ECharts 2D Canvas/WebGL)**
   - Target use case: multi-axis OLAP breakdown, correlation heatmaps, sector exposure bar charts, trade execution scatter plots, risk distribution histograms, multi-series line charts.
   - Technology stack: Apache ECharts using 2D Canvas and WebGL renderers, managed by dirty-rectangle rendering (`useDirtyRect: true`).
   - Performance profile: handles up to 100,000 data points smoothly; governed by WebGL context pooling.

3. **Tier 3: High-Frequency Financial Tick Streams (TradingView Lightweight Charts)**
   - Target use case: hardware-accelerated financial candlestick (OHLC) charts, volume histograms, order book depth charts, microsecond tick streams.
   - Technology stack: TradingView Lightweight Charts (ultra-lean ~45KB engine, hardware-accelerated 2D Canvas).
   - Performance profile: renders 1,000,000+ tick series at 60 FPS with microsecond incremental `series.update()` calls.

### 1.3 Off-Main-Thread Data & Rendering Pipeline
- Web Worker LTTB downsampling: high-density datasets (>100,000 points) transfer as ArrayBuffers (`Float64Array`) to background Web Worker running **Largest-Triangle-Three-Buckets (LTTB)** algorithm, reducing points to match exact screen pixel width before render.
- OffscreenCanvas transferable setup: canvas DOM elements transfer control off main thread via `canvas.transferControlToOffscreen()`. Background workers handle render draw loops, preventing main-thread React re-renders from impacting tick rendering.

### 1.4 Resource Governance & Memory Limits
- `WebGLContextPoolManager`: enforces strict hard cap of **max 8 active rendered canvas contexts per browser tab**. Manages context acquisition, viewport intersection deferral, context loss recovery (`webglcontextlost`), mandatory `.dispose()` hooks on unmount.
- `ClientMemoryGovernor`: implements unified **512MB per tab** memory limit across DuckDB WASM (128MB), TanStack DB (64MB), Visual Workers (128MB), Canvas VRAM (128MB), GC reserve (64MB). Emits pressure warning events past 80% usage (410MB) forcing worker buffer recycling and LRU cache eviction.

### 1.5 Dynamic Dashboard Framework & Semantic Layer Binding
- React Grid Layout integration: drag-and-drop, responsive 12-column dashboard layout grid with mobile breakpoints (`lg: 1200`, `md: 996`, `sm: 768`).
- Semantic AST binding: widgets define JSON Query AST (`dimensions`, `measures`, `time_dimensions`, `filters`) binding directly to Task 04's Semantic Layer API (`/api/v1/semantic/query`).
- Dashboard schema serialization: grid layouts and widget visual encodings serialize into clean JSON schemas for backend storage in PostgreSQL (`workspace_dashboards.layout_json`).

---

## 2. Key Deliverables & File Layout

```
src/Frontend/src/
├── types/
│   ├── visualizations.ts                  # Strong TypeScript contracts for 3-tier widgets, AST & encodings
│   └── dashboardSchema.json               # JSON Schema for dashboard grid & widget validation
├── lib/
│   └── governance/
│       ├── WebGLContextPoolManager.ts     # Context pool manager (max 8 canvas cap, context loss handling)
│       └── ClientMemoryGovernor.ts        # Unified 512MB memory governor & compaction warning system
├── workers/
│   ├── lttbDownsample.worker.ts           # Web Worker implementing LTTB downsampling algorithm
│   └── offscreenCanvasRenderer.worker.ts  # OffscreenCanvas renderer worker handling off-thread draw commands
├── hooks/
│   ├── useOffscreenCanvasChart.ts        # Hook for transferring canvas control to offscreen worker
│   └── useManagedChartLifecycle.ts       # Hook combining context pool acquisition, resize & unmount disposal
└── components/
    ├── visualizations/
    │   ├── TremorKpiCard.tsx              # Tier 1 Tremor summary KPI card component
    │   ├── EChartsWrapper.tsx             # Tier 2 Apache ECharts 2D/WebGL canvas component
    │   ├── LightweightChartsWrapper.tsx   # Tier 3 TradingView Lightweight Charts candlestick component
    │   ├── WidgetRegistry.ts              # Widget component factory & type registration table
    │   ├── VisualEncodingMapper.ts        # Transforms Semantic AST query output into chart configs
    │   └── QueryBindingConfigurator.tsx   # Interactive metric picker & visual encoding binding UI
    └── dashboard/
        └── DashboardGrid.tsx              # React Grid Layout dynamic dashboard component

tests/Frontend/
├── governance/
│   ├── WebGLContextPoolManager.test.ts    # Unit tests for 8-canvas hard cap and context disposal
│   └── ClientMemoryGovernor.test.ts       # Unit tests for memory budget tracking & pressure events
└── workers/
    └── lttbDownsample.test.ts             # Unit tests for LTTB downsampling accuracy & peak retention
```

---

## 3. Architecture & Code Contract Blueprints

### 3.1 Type Definitions (`src/Frontend/src/types/visualizations.ts`)

```typescript
export type ChartEngineTier = 'TIER_1_KPI' | 'TIER_2_ANALYTICS' | 'TIER_3_FINANCIAL' | 'CUSTOM_PLUGIN';

export type ChartType = 
  | 'KPI_CARD' 
  | 'SPARK_LINE' 
  | 'LINE' 
  | 'AREA' 
  | 'BAR' 
  | 'STACKED_BAR' 
  | 'SCATTER' 
  | 'HEATMAP' 
  | 'TREEMAP' 
  | 'CANDLESTICK' 
  | 'ORDER_DEPTH' 
  | 'TABLE';

export interface SemanticASTFilter {
  member: string;
  operator: 'equals' | 'not_equals' | 'in' | 'greater_than' | 'less_than' | 'between';
  values: (string | number | boolean)[];
}

export interface SemanticASTTimeDimension {
  dimension: string;
  granularity: 'second' | 'minute' | 'hour' | 'day' | 'week' | 'month';
  dateRange?: [string, string];
}

export interface SemanticQueryAST {
  dimensions: string[];
  measures: string[];
  timeDimensions?: SemanticASTTimeDimension[];
  filters?: SemanticASTFilter[];
}

export interface VisualEncodingSpec {
  xAxis: string;
  yAxis: string[];
  colorBy?: string;
  sizeBy?: string;
  tooltipFields?: string[];
  colorScale?: {
    type: 'CATEGORICAL' | 'SEQUENTIAL' | 'DIVERGING';
    palette: string[];
  };
}

export interface WidgetStyleOverrides {
  showLegend?: boolean;
  showGridlines?: boolean;
  strokeWidth?: number;
  opacity?: number;
  theme?: 'dark' | 'light';
}

export interface ChartWidgetConfig {
  id: string;
  title: string;
  tier: ChartEngineTier;
  chartType: ChartType;
  pluginRef?: string;
  semanticModelRef: string;
  queryAst: SemanticQueryAST;
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

---

### 3.2 Web Worker LTTB Downsampling Algorithm (`src/Frontend/src/workers/lttbDownsample.worker.ts`)

```typescript
export interface DataPoint {
  x: number; // Epoch timestamp in ms
  y: number; // Price / Volume / Metric value
}

/**
 * Largest-Triangle-Three-Buckets (LTTB) Downsampling Algorithm.
 * Downsamples high-density time-series data while retaining visually prominent peaks and troughs.
 */
export function lttbDownsample(data: DataPoint[], threshold: number): DataPoint[] {
  const dataLength = data.length;
  if (threshold >= dataLength || threshold === 0) return data;

  const sampled: DataPoint[] = new Array(threshold);
  let sampledIndex = 0;

  // Bucket size excluding start and end points
  const every = (dataLength - 2) / (threshold - 2);

  let a = 0; // First point index
  sampled[sampledIndex++] = data[a];

  for (let i = 0; i < threshold - 2; i++) {
    // Compute average point for next bucket (bucket c)
    let avgX = 0;
    let avgY = 0;
    let avgRangeStart = Math.floor((i + 1) * every) + 1;
    let avgRangeEnd = Math.floor((i + 2) * every) + 1;
    avgRangeEnd = avgRangeEnd < dataLength ? avgRangeEnd : dataLength;

    const avgRangeLength = avgRangeEnd - avgRangeStart;
    for (; avgRangeStart < avgRangeEnd; avgRangeStart++) {
      avgX += data[avgRangeStart].x;
      avgY += data[avgRangeStart].y;
    }
    avgX /= avgRangeLength;
    avgY /= avgRangeLength;

    // Get current bucket range (bucket b)
    let rangeOffs = Math.floor((i + 0) * every) + 1;
    const rangeTo = Math.floor((i + 1) * every) + 1;

    // Point a values
    const pointAX = data[a].x;
    const pointAY = data[a].y;

    let maxArea = -1;
    let nextA = rangeOffs;

    for (; rangeOffs < rangeTo; rangeOffs++) {
      // Triangle area calculation: 0.5 * |Ax(By - Cy) + Bx(Cy - Ay) + Cx(Ay - By)|
      const area = Math.abs(
        (pointAX - avgX) * (data[rangeOffs].y - pointAY) -
        (pointAX - data[rangeOffs].x) * (avgY - pointAY)
      ) * 0.5;

      if (area > maxArea) {
        maxArea = area;
        nextA = rangeOffs;
      }
    }

    sampled[sampledIndex++] = data[nextA]; // Select point with maximum triangle area
    a = nextA; // Advance point a
  }

  sampled[sampledIndex++] = data[dataLength - 1]; // Always include final data point
  return sampled;
}

// Worker message protocol supporting ArrayBuffer transfers
self.onmessage = (e: MessageEvent<{ data: DataPoint[]; threshold: number; requestId: string }>) => {
  const { data, threshold, requestId } = e.data;
  const downsampled = lttbDownsample(data, threshold);
  
  self.postMessage({ requestId, downsampled });
};
```

---

### 3.3 WebGLContextPoolManager & Managed Hook (`src/Frontend/src/lib/governance/WebGLContextPoolManager.ts`)

```typescript
export interface ContextPoolStatus {
  activeCount: number;
  maxCap: number;
  activeWidgetIds: string[];
}

/**
 * WebGL Context Pool Governor managing active canvas context slots per browser tab.
 * Enforces hard cap of 8 active canvas contexts to prevent VRAM memory leaks and context eviction.
 */
export class WebGLContextPoolManager {
  private static instance: WebGLContextPoolManager;
  private readonly MAX_CANVAS_CAP = 8;
  private activeSlots = new Set<string>();
  private contextLossListeners = new Map<string, () => void>();

  private constructor() {}

  public static getInstance(): WebGLContextPoolManager {
    if (!WebGLContextPoolManager.instance) {
      WebGLContextPoolManager.instance = new WebGLContextPoolManager();
    }
    return WebGLContextPoolManager.instance;
  }

  public acquireContextSlot(widgetId: string): boolean {
    if (this.activeSlots.has(widgetId)) return true;
    if (this.activeSlots.size >= this.MAX_CANVAS_CAP) {
      console.warn(`[WebGLContextPoolManager] Widget ${widgetId} rejected. Hard cap of ${this.MAX_CANVAS_CAP} canvas contexts reached.`);
      return false;
    }
    this.activeSlots.add(widgetId);
    return true;
  }

  public releaseContextSlot(widgetId: string): void {
    this.activeSlots.delete(widgetId);
    this.contextLossListeners.delete(widgetId);
  }

  public registerContextLossHandler(widgetId: string, handler: () => void): void {
    this.contextLossListeners.set(widgetId, handler);
  }

  public getStatus(): ContextPoolStatus {
    return {
      activeCount: this.activeSlots.size,
      maxCap: this.MAX_CANVAS_CAP,
      activeWidgetIds: Array.from(this.activeSlots)
    };
  }

  public resetPoolForTesting(): void {
    this.activeSlots.clear();
    this.contextLossListeners.clear();
  }
}
```

```typescript
// hooks/useManagedChartLifecycle.ts
import { useEffect, useRef } from 'react';
import * as echarts from 'echarts';
import { WebGLContextPoolManager } from '../lib/governance/WebGLContextPoolManager';

export interface UseManagedChartProps {
  widgetId: string;
  options: echarts.EChartsOption;
  onContextLost?: () => void;
}

export function useManagedChartLifecycle({ widgetId, options, onContextLost }: UseManagedChartProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const chartInstanceRef = useRef<echarts.ECharts | null>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    // Acquire context slot from governor
    const poolManager = WebGLContextPoolManager.getInstance();
    const slotGranted = poolManager.acquireContextSlot(widgetId);
    if (!slotGranted) {
      if (onContextLost) onContextLost();
      return;
    }

    // Instantiate ECharts instance
    const chart = echarts.init(container, 'dark', {
      renderer: 'canvas',
      useDirtyRect: true
    });
    chartInstanceRef.current = chart;
    chart.setOption(options);

    // Listen for WebGL context loss
    const canvas = container.querySelector('canvas');
    const handleContextLost = (e: Event) => {
      e.preventDefault();
      console.warn(`[useManagedChartLifecycle] Context lost for widget: ${widgetId}`);
      if (onContextLost) onContextLost();
    };

    if (canvas) {
      canvas.addEventListener('webglcontextlost', handleContextLost, false);
    }

    // Resize observer
    const resizeObserver = new ResizeObserver(() => {
      if (chartInstanceRef.current && !chartInstanceRef.current.isDisposed()) {
        chartInstanceRef.current.resize();
      }
    });
    resizeObserver.observe(container);

    // Mandatory Unmount Disposal Hook
    return () => {
      resizeObserver.disconnect();
      if (canvas) {
        canvas.removeEventListener('webglcontextlost', handleContextLost);
      }
      if (chartInstanceRef.current) {
        if (!chartInstanceRef.current.isDisposed()) {
          chartInstanceRef.current.clear();
          chartInstanceRef.current.dispose();
        }
        chartInstanceRef.current = null;
      }
      poolManager.releaseContextSlot(widgetId);
    };
  }, [widgetId, options, onContextLost]);

  return { containerRef, chartInstance: chartInstanceRef.current };
}
```

---

### 3.4 ClientMemoryGovernor Implementation (`src/Frontend/src/lib/governance/ClientMemoryGovernor.ts`)

```typescript
export interface MemoryBudgetStatus {
  totalAllocatedMB: number;
  maxBudgetMB: number;
  pressurePercentage: number;
  isCritical: boolean;
  duckDbMB: number;
  tanStackDbMB: number;
  workerBufferMB: number;
  canvasVramMB: number;
}

/**
 * Unified Client Memory Governor enforcing a strict 512MB per tab budget across all subsystem modules.
 */
export class ClientMemoryGovernor {
  private static instance: ClientMemoryGovernor;
  private readonly MAX_BUDGET_MB = 512;
  private readonly DUCKDB_LIMIT_MB = 128;
  private readonly TANSTACK_LIMIT_MB = 64;
  private readonly WORKER_LIMIT_MB = 128;
  private readonly VRAM_LIMIT_MB = 128;

  private currentDuckDbMB = 0;
  private currentTanStackDbMB = 0;
  private currentWorkerBufferMB = 0;
  private currentVramMB = 0;
  private monitorIntervalId: number | null = null;

  private constructor() {
    this.startMonitoring();
  }

  public static getInstance(): ClientMemoryGovernor {
    if (!ClientMemoryGovernor.instance) {
      ClientMemoryGovernor.instance = new ClientMemoryGovernor();
    }
    return ClientMemoryGovernor.instance;
  }

  public registerWorkerAllocation(bytes: number): boolean {
    const mb = bytes / (1024 * 1024);
    if (this.currentWorkerBufferMB + mb > this.WORKER_LIMIT_MB) {
      this.triggerCompaction('WORKER_LIMIT_EXCEEDED');
    }
    this.currentWorkerBufferMB += mb;
    return this.evaluateHealth();
  }

  public releaseWorkerAllocation(bytes: number): void {
    const mb = bytes / (1024 * 1024);
    this.currentWorkerBufferMB = Math.max(0, this.currentWorkerBufferMB - mb);
  }

  public registerVramAllocation(estimatedMB: number = 16): boolean {
    if (this.currentVramMB + estimatedMB > this.VRAM_LIMIT_MB) {
      console.warn(`[ClientMemoryGovernor] VRAM limit (${this.VRAM_LIMIT_MB}MB) reached. Allocation denied.`);
      return false;
    }
    this.currentVramMB += estimatedMB;
    return this.evaluateHealth();
  }

  public releaseVramAllocation(estimatedMB: number = 16): void {
    this.currentVramMB = Math.max(0, this.currentVramMB - estimatedMB);
  }

  public getStatus(): MemoryBudgetStatus {
    const total = this.currentDuckDbMB + this.currentTanStackDbMB + this.currentWorkerBufferMB + this.currentVramMB;
    const pct = (total / this.MAX_BUDGET_MB) * 100;
    return {
      totalAllocatedMB: total,
      maxBudgetMB: this.MAX_BUDGET_MB,
      pressurePercentage: pct,
      isCritical: pct > 80,
      duckDbMB: this.currentDuckDbMB,
      tanStackDbMB: this.currentTanStackDbMB,
      workerBufferMB: this.currentWorkerBufferMB,
      canvasVramMB: this.currentVramMB
    };
  }

  private evaluateHealth(): boolean {
    const status = this.getStatus();
    if (status.isCritical) {
      this.triggerCompaction('PRESSURE_THRESHOLD_80_PERCENT');
    }
    return !status.isCritical;
  }

  private triggerCompaction(reason: string): void {
    console.warn(`[ClientMemoryGovernor] Memory pressure warning (${reason}). Dispatching compaction event.`);
    if (typeof window !== 'undefined') {
      window.dispatchEvent(new CustomEvent('TRADEBOOK_MEMORY_PRESSURE_WARNING', { detail: { reason } }));
    }
  }

  private startMonitoring(): void {
    if (typeof window !== 'undefined' && 'performance' in window) {
      const perf = window.performance as unknown as { memory?: { usedJSHeapSize: number } };
      if (perf.memory) {
        this.monitorIntervalId = window.setInterval(() => {
          const heapMB = perf.memory!.usedJSHeapSize / (1024 * 1024);
          if (heapMB > 410) { // 80% of 512MB
            this.triggerCompaction('V8_HEAP_HIGH');
          }
        }, 5000);
      }
    }
  }

  public resetGovernorForTesting(): void {
    if (this.monitorIntervalId !== null && typeof window !== 'undefined') {
      window.clearInterval(this.monitorIntervalId);
      this.monitorIntervalId = null;
    }
    this.currentDuckDbMB = 0;
    this.currentTanStackDbMB = 0;
    this.currentWorkerBufferMB = 0;
    this.currentVramMB = 0;
  }
}
```

---

### 3.5 OffscreenCanvas Renderer Worker & Hook (`src/Frontend/src/workers/offscreenCanvasRenderer.worker.ts` & `useOffscreenCanvasChart.ts`)

```typescript
// workers/offscreenCanvasRenderer.worker.ts
export interface InitCanvasMessage {
  action: 'INIT';
  canvas: OffscreenCanvas;
  width: number;
  height: number;
}

export interface DrawSeriesMessage {
  action: 'DRAW';
  points: Array<{ x: number; y: number }>;
  color: string;
}

export interface DisposeCanvasMessage {
  action: 'DISPOSE';
}

type WorkerMessage = InitCanvasMessage | DrawSeriesMessage | DisposeCanvasMessage;

let offscreenCanvas: OffscreenCanvas | null = null;
let ctx: OffscreenCanvasRenderingContext2D | null = null;

self.onmessage = (e: MessageEvent<WorkerMessage>) => {
  const msg = e.data;
  switch (msg.action) {
    case 'INIT':
      offscreenCanvas = msg.canvas;
      offscreenCanvas.width = msg.width;
      offscreenCanvas.height = msg.height;
      ctx = offscreenCanvas.getContext('2d');
      if (ctx) {
        ctx.fillStyle = '#111827';
        ctx.fillRect(0, 0, msg.width, msg.height);
      }
      break;

    case 'DRAW':
      if (!ctx || !offscreenCanvas) return;
      ctx.fillStyle = '#111827';
      ctx.fillRect(0, 0, offscreenCanvas.width, offscreenCanvas.height);

      if (msg.points.length < 2) return;
      ctx.beginPath();
      ctx.strokeStyle = msg.color || '#3B82F6';
      ctx.lineWidth = 2;

      const w = offscreenCanvas.width;
      const h = offscreenCanvas.height;
      const xMin = msg.points[0].x;
      const xMax = msg.points[msg.points.length - 1].x;
      let yMin = Infinity;
      let yMax = -Infinity;

      for (let i = 0; i < msg.points.length; i++) {
        if (msg.points[i].y < yMin) yMin = msg.points[i].y;
        if (msg.points[i].y > yMax) yMax = msg.points[i].y;
      }
      const yRange = yMax - yMin || 1;

      for (let i = 0; i < msg.points.length; i++) {
        const px = ((msg.points[i].x - xMin) / (xMax - xMin)) * w;
        const py = h - ((msg.points[i].y - yMin) / yRange) * h;
        if (i === 0) ctx.moveTo(px, py);
        else ctx.lineTo(px, py);
      }
      ctx.stroke();
      break;

    case 'DISPOSE':
      ctx = null;
      offscreenCanvas = null;
      break;
  }
};
```

```typescript
// hooks/useOffscreenCanvasChart.ts
import { useEffect, useRef } from 'react';

export function useOffscreenCanvasChart(points: Array<{ x: number; y: number }>, color: string = '#3B82F6') {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const workerRef = useRef<Worker | null>(null);

  useEffect(() => {
    if (!canvasRef.current || !containerRef.current) return;
    const canvas = canvasRef.current;
    const rect = containerRef.current.getBoundingClientRect();

    if ('transferControlToOffscreen' in canvas) {
      const offscreen = canvas.transferControlToOffscreen();
      const worker = new Worker(new URL('../workers/offscreenCanvasRenderer.worker.ts', import.meta.url), { type: 'module' });
      workerRef.current = worker;

      worker.postMessage({
        action: 'INIT',
        canvas: offscreen,
        width: rect.width || 600,
        height: rect.height || 300
      }, [offscreen]);
    }

    return () => {
      if (workerRef.current) {
        workerRef.current.postMessage({ action: 'DISPOSE' });
        workerRef.current.terminate();
        workerRef.current = null;
      }
    };
  }, []);

  useEffect(() => {
    if (workerRef.current && points.length > 0) {
      workerRef.current.postMessage({ action: 'DRAW', points, color });
    }
  }, [points, color]);

  return { containerRef, canvasRef };
}
```

---

### 3.6 Tier 1, 2, and 3 Widget Components

#### Tier 1 Component: Tremor KPI Card (`src/Frontend/src/components/visualizations/TremorKpiCard.tsx`)
```tsx
import React from 'react';

export interface TremorKpiCardProps {
  title: string;
  metricValue: string | number;
  deltaValue: string;
  isPositive: boolean;
  sparklineData?: number[];
}

export const TremorKpiCard: React.FC<TremorKpiCardProps> = ({
  title,
  metricValue,
  deltaValue,
  isPositive,
  sparklineData = []
}) => {
  return (
    <div className="p-4 rounded-xl bg-gray-900 border border-gray-800 text-white flex flex-col justify-between h-full">
      <div>
        <span className="text-xs font-medium text-gray-400 uppercase tracking-wider">{title}</span>
        <div className="flex items-baseline justify-between mt-2">
          <span className="text-2xl font-bold text-gray-100">{metricValue}</span>
          <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${
            isPositive ? 'bg-emerald-950 text-emerald-400 border border-emerald-800' : 'bg-rose-950 text-rose-400 border border-rose-800'
          }`}>
            {deltaValue}
          </span>
        </div>
      </div>
      {sparklineData.length > 0 && (
        <div className="mt-4 h-8 flex items-end gap-1">
          {sparklineData.map((val, idx) => (
            <div
              key={idx}
              className={`flex-1 rounded-t ${isPositive ? 'bg-emerald-500/40' : 'bg-rose-500/40'}`}
              style={{ height: `${Math.max(10, val)}%` }}
            />
          ))}
        </div>
      )}
    </div>
  );
};
```

#### Tier 2 Component: ECharts Analytics Wrapper (`src/Frontend/src/components/visualizations/EChartsWrapper.tsx`)
```tsx
import React from 'react';
import { useManagedChartLifecycle } from '../../hooks/useManagedChartLifecycle';
import { EChartsOption } from 'echarts';

export interface EChartsWrapperProps {
  widgetId: string;
  options: EChartsOption;
}

export const EChartsWrapper: React.FC<EChartsWrapperProps> = ({ widgetId, options }) => {
  const [isContextDeferred, setIsContextDeferred] = React.useState(false);

  const { containerRef } = useManagedChartLifecycle({
    widgetId,
    options,
    onContextLost: () => setIsContextDeferred(true)
  });

  if (isContextDeferred) {
    return (
      <div className="w-full h-full flex flex-col items-center justify-center bg-gray-900 border border-gray-800 text-gray-400 text-xs p-4 rounded-xl">
        <span className="font-semibold text-amber-400 mb-1">Canvas Limit Reached</span>
        <span>Widget deferred (Max 8 active canvas cap per tab reached).</span>
      </div>
    );
  }

  return <div ref={containerRef} className="w-full h-full min-h-[250px]" />;
};
```

#### Tier 3 Component: Lightweight Charts Financial Engine Wrapper (`src/Frontend/src/components/visualizations/LightweightChartsWrapper.tsx`)
```tsx
import React, { useEffect, useRef } from 'react';
import { createChart, IChartApi, ISeriesApi, CandlestickData } from 'lightweight-charts';
import { WebGLContextPoolManager } from '../../lib/governance/WebGLContextPoolManager';

export interface LightweightChartsWrapperProps {
  widgetId: string;
  data: CandlestickData[];
}

export const LightweightChartsWrapper: React.FC<LightweightChartsWrapperProps> = ({ widgetId, data }) => {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const chartRef = useRef<IChartApi | null>(null);
  const seriesRef = useRef<ISeriesApi<'Candlestick'> | null>(null);
  const [isDeferred, setIsDeferred] = React.useState(false);

  useEffect(() => {
    if (!containerRef.current) return;
    const poolManager = WebGLContextPoolManager.getInstance();
    if (!poolManager.acquireContextSlot(widgetId)) {
      setIsDeferred(true);
      return;
    }

    const chart = createChart(containerRef.current, {
      layout: { background: { color: '#111827' }, textColor: '#9CA3AF' },
      grid: { vertLines: { color: '#1F2937' }, horzLines: { color: '#1F2937' } },
      width: containerRef.current.clientWidth || 500,
      height: containerRef.current.clientHeight || 300
    });

    const candlestickSeries = chart.addCandlestickSeries({
      upColor: '#10B981',
      downColor: '#EF4444',
      borderVisible: false,
      wickUpColor: '#10B981',
      wickDownColor: '#EF4444'
    });

    candlestickSeries.setData(data);
    chartRef.current = chart;
    seriesRef.current = candlestickSeries;

    const handleResize = () => {
      if (containerRef.current && chartRef.current) {
        chartRef.current.applyOptions({
          width: containerRef.current.clientWidth,
          height: containerRef.current.clientHeight
        });
      }
    };

    window.addEventListener('resize', handleResize);

    return () => {
      window.removeEventListener('resize', handleResize);
      if (chartRef.current) {
        chartRef.current.remove();
        chartRef.current = null;
      }
      poolManager.releaseContextSlot(widgetId);
    };
  }, [widgetId, data]);

  if (isDeferred) {
    return (
      <div className="w-full h-full flex flex-col items-center justify-center bg-gray-900 border border-gray-800 text-gray-400 text-xs p-4 rounded-xl">
        <span className="font-semibold text-amber-400 mb-1">Canvas Limit Reached</span>
        <span>Financial stream deferred (Max 8 canvas cap reached).</span>
      </div>
    );
  }

  return <div ref={containerRef} className="w-full h-full min-h-[300px]" />;
};
```

---

## 4. Subagent Implementation Step-by-Step Workflow

Subagents assigned Task 06 execute implementation in 5 sequential stages:

```
+---------------------------------------------------------------------------------------------------+
5-STEP IMPLEMENTATION WORKFLOW
+---------------------------------------------------------------------------------------------------+
1. Foundation & Governance -> Create types/visualizations.ts, WebGLContextPoolManager.ts, ClientMemoryGovernor.ts
2. Web Workers Pipeline    -> Create lttbDownsample.worker.ts, offscreenCanvasRenderer.worker.ts, hooks
3. 3-Tier Components      -> Build TremorKpiCard, EChartsWrapper, LightweightChartsWrapper
4. Integration & Registry -> Build WidgetRegistry, VisualEncodingMapper, DashboardGrid with React Grid Layout
5. Testing & Verification  -> Write Jest/Vitest unit tests for governance hard caps, workers, memory limits
+---------------------------------------------------------------------------------------------------+
```

### Step 1: Governance & Worker Foundations
1. Create `src/Frontend/src/types/visualizations.ts` defining widget interfaces, AST queries, dashboard schemas.
2. Build `WebGLContextPoolManager.ts` tracking active slots (max 8) and `releaseContextSlot()` logic.
3. Build `ClientMemoryGovernor.ts` tracking 512MB allocation budget and compaction warning dispatchers.

### Step 2: Web Worker Downsampling & OffscreenCanvas Setup
1. Implement `lttbDownsample.worker.ts` with genuine LTTB triangle area calculations.
2. Build `offscreenCanvasRenderer.worker.ts` for off-thread 2D canvas draw execution.
3. Construct React hooks `useOffscreenCanvasChart.ts` and `useManagedChartLifecycle.ts`.

### Step 3: 3-Tier Widget Component Suite
1. Build `TremorKpiCard.tsx` for Tier 1 summary callouts.
2. Build `EChartsWrapper.tsx` using `useManagedChartLifecycle` hook.
3. Build `LightweightChartsWrapper.tsx` integrating TradingView Lightweight Charts canvas.

### Step 4: Widget Registry & Dashboard Grid Layout
1. Implement `VisualEncodingMapper.ts` translating Semantic AST outputs to ECharts/Lightweight options.
2. Build `WidgetRegistry.ts` mapping `chartType` string identifiers to component rendering wrappers.
3. Build `DashboardGrid.tsx` using `react-grid-layout` with responsive breakpoints.

### Step 5: Unit & Verification Test Suite
1. Write unit tests `WebGLContextPoolManager.test.ts` asserting context rejection on 9th allocation request.
2. Write unit tests `ClientMemoryGovernor.test.ts` verifying event emission when budget exceeds 80%.
3. Write unit tests `lttbDownsample.test.ts` verifying data reduction while retaining min/max peaks.

---

## 5. Independent Verification & Acceptance Workflow

To guarantee zero regressions and strict compliance, implementation must pass following verification workflow:

```bash
# 1. Execute TypeScript Compiler Check
cd src/Frontend
npx tsc --noEmit

# 2. Run Unit Tests for Memory Governor, WebGL Pool & Workers
npm run test tests/Frontend/governance/WebGLContextPoolManager.test.ts
npm run test tests/Frontend/governance/ClientMemoryGovernor.test.ts
npm run test tests/Frontend/workers/lttbDownsample.test.ts

# 3. Execute Frontend Production Bundle Build
npm run build
```

### Quantitative Acceptance Criteria

| Criteria | Target Threshold | Verification Method |
| :--- | :--- | :--- |
| **Max WebGL Context Cap** | **Strictly 8 canvas instances** | Unit test `WebGLContextPoolManager.test.ts` requesting 9 slots asserts 9th is rejected (`false`). |
| **Client Memory Limit** | **Max 512MB per tab** | Unit test `ClientMemoryGovernor.test.ts` triggers `TRADEBOOK_MEMORY_PRESSURE_WARNING` when allocation exceeds 410MB. |
| **LTTB Downsampling Accuracy** | **Retains peaks & min/max** | Unit test `lttbDownsample.test.ts` reduces 100,000 points to 1,000 while retaining absolute max/min timestamps. |
| **Component Unmount Disposal** | **0 Context Leaks** | Unmounting `EChartsWrapper` component releases context slot back to pool (active count returns to prior value). |
| **Build Bundle Verification** | **0 TypeScript Errors** | `npx tsc --noEmit` and `npm run build` pass with zero warnings/errors. |

---

## 6. Anti-Cheating & Integrity Guardrails

To maintain system integrity, subagents/engineers on Task 06 must strictly adhere to following mandatory integrity rules:

1. **NO Hardcoded Test Returns**: `lttbDownsample` worker function must execute genuine triangle area calculations over real data arrays. Returning static pre-calculated arrays or mock responses to pass test suites strictly forbidden.
2. **NO Facade Memory Governors**: `WebGLContextPoolManager` and `ClientMemoryGovernor` must maintain real runtime `Set` collections and numeric memory counters. Dummy stubs unconditionally returning `true` without tracking context slots is a violation.
3. **NO Ignored Context Lost Handlers**: Visual components must bind genuine `webglcontextlost` event listeners and release GPU VRAM on unmount. Empty cleanup functions in `useEffect` hooks prohibited.
4. **NO Main-Thread Blocking Tricks**: Heavy downsampling on datasets >10,000 elements must run inside Web Worker threads. Synchronous main-thread loops masquerading as worker pipelines detected by performance profiling.

*Task specification completed & verified for Tradebook Task 06.*
