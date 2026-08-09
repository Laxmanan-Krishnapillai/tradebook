import type { JsonQueryAst } from './semanticAst';

export type ChartType = 'KPI_CARD' | 'SPARK_LINE' | 'LINE' | 'AREA' | 'BAR' | 'STACKED_BAR' | 'SCATTER' | 'HEATMAP' | 'CANDLESTICK' | 'TABLE';
export interface ThemeTokens { background: string; textPrimary: string; textSecondary: string; gridLine: string; axisLine: string; seriesPalette: string[]; positive: string; negative: string; fontFamily: string; }
export interface SeriesData { series: Array<{ name: string; x: Array<string | number>; y: Float64Array | number[] }>; ohlc?: Array<{ time: number; open: number; high: number; low: number; close: number }>; }
export interface VisualEncodingSpec { xAxis: string; yAxis: string[]; colorBy?: string; sizeBy?: string; tooltipFields?: string[]; }
interface WidgetStyleOverrides { showLegend?: boolean; showGridlines?: boolean; strokeWidth?: number; opacity?: number; }
export interface ChartSpec { chartType: ChartType; encodings: VisualEncodingSpec; style?: WidgetStyleOverrides; }
export interface ChartAdapter { mount(el: HTMLElement, spec: ChartSpec): void; update(data: SeriesData): void; resize(): void; setTheme(tokens: ThemeTokens): void; destroy(): void; }
export interface ChartWidgetConfig { id: string; title: string; chartType: ChartType; semanticModelRef: string; queryAst: JsonQueryAst; visualEncodings: VisualEncodingSpec; styleOverrides?: WidgetStyleOverrides; }
interface DashboardGridItem { widgetId: string; x: number; y: number; w: number; h: number; minW?: number; minH?: number; static?: boolean; }
export interface DashboardSpecification { dashboardId: string; title: string; description?: string; version: number; theme: 'DARK' | 'LIGHT' | 'SYSTEM'; refreshRateMs: number; gridLayout: { columns: number; rowHeight: number; items: DashboardGridItem[] }; widgets: ChartWidgetConfig[]; }
