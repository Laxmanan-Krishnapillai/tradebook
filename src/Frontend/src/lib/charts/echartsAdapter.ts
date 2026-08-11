import * as echarts from 'echarts';
import { toEChartsOption } from './visualEncodingMapper';
import type { ChartAdapter, ChartSpec, SeriesData, ThemeTokens } from '../../types/visualizations';
export class EChartsAdapter implements ChartAdapter {
  private chart: echarts.ECharts | null = null; private spec: ChartSpec | null = null; private tokens: ThemeTokens | null = null; private lastData: SeriesData | null = null; private reducedMotion: MediaQueryList | null = null;
  private readonly handleMotionChange = () => { if (this.lastData) this.update(this.lastData); };
  mount(el: HTMLElement, spec: ChartSpec): void { if (this.chart) return; this.spec = spec; this.chart = echarts.init(el, undefined, { renderer: 'canvas', useDirtyRect: true }); this.reducedMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)') ?? null; this.reducedMotion?.addEventListener('change', this.handleMotionChange); }
  update(data: SeriesData): void { if (!this.chart || !this.spec) return; this.lastData = data; const option = toEChartsOption(this.spec, data, this.tokens); if (this.reducedMotion?.matches) option.animation = false; this.chart.setOption(option); }
  resize(): void { this.chart?.resize(); }
  setTheme(tokens: ThemeTokens): void { this.tokens = tokens; if (this.lastData) this.update(this.lastData); }
  destroy(): void { this.reducedMotion?.removeEventListener('change', this.handleMotionChange); this.reducedMotion = null; if (this.chart && !this.chart.isDisposed()) this.chart.dispose(); this.chart = null; }
}
