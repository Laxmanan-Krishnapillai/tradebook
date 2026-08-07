import { CandlestickSeries, createChart, type IChartApi, type ISeriesApi } from 'lightweight-charts';
import type { ChartAdapter, ChartSpec, SeriesData, ThemeTokens } from '../../types/visualizations';
export class LightweightChartsAdapter implements ChartAdapter {
  private el: HTMLElement | null = null; private chart: IChartApi | null = null; private series: ISeriesApi<'Candlestick'> | null = null;
  mount(el: HTMLElement, _spec: ChartSpec): void { if (this.chart) return; this.el = el; this.chart = createChart(el, { width: el.clientWidth || 500, height: el.clientHeight || 300 }); this.series = this.chart.addSeries(CandlestickSeries, { upColor: '#10B981', downColor: '#EF4444', borderVisible: false, wickUpColor: '#10B981', wickDownColor: '#EF4444' }); }
  update(data: SeriesData): void { if (data.ohlc) this.series?.setData(data.ohlc.map((row) => ({ time: (row.time / 1000) as never, open: row.open, high: row.high, low: row.low, close: row.close }))); }
  resize(): void { if (this.el && this.chart) this.chart.applyOptions({ width: this.el.clientWidth, height: this.el.clientHeight }); }
  setTheme(tokens: ThemeTokens): void { this.chart?.applyOptions({ layout: { background: { color: tokens.background }, textColor: tokens.textPrimary }, grid: { vertLines: { color: tokens.gridLine }, horzLines: { color: tokens.gridLine } } }); }
  destroy(): void { this.chart?.remove(); this.chart = null; this.series = null; }
}
