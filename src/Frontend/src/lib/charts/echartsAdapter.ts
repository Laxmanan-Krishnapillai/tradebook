import * as echarts from 'echarts';
import { toEChartsOption } from './visualEncodingMapper';
import type { ChartAdapter, ChartSpec, SeriesData, ThemeTokens } from '../../types/visualizations';
export class EChartsAdapter implements ChartAdapter {
  private chart: echarts.ECharts | null = null; private spec: ChartSpec | null = null; private tokens: ThemeTokens | null = null; private lastData: SeriesData | null = null;
  mount(el: HTMLElement, spec: ChartSpec): void { if (this.chart) return; this.spec = spec; this.chart = echarts.init(el, undefined, { renderer: 'canvas', useDirtyRect: true }); }
  update(data: SeriesData): void { if (!this.chart || !this.spec) return; this.lastData = data; this.chart.setOption(toEChartsOption(this.spec, data, this.tokens)); }
  resize(): void { this.chart?.resize(); }
  setTheme(tokens: ThemeTokens): void { this.tokens = tokens; if (this.lastData) this.update(this.lastData); }
  destroy(): void { if (this.chart && !this.chart.isDisposed()) this.chart.dispose(); this.chart = null; }
}
