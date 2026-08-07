import Card from '@tremor/react/dist/components/layout-elements/Card/Card.js';
import Metric from '@tremor/react/dist/components/text-elements/Metric/Metric.js';
import Text from '@tremor/react/dist/components/text-elements/Text/Text.js';
import { createRoot, type Root } from 'react-dom/client';
import type { ChartAdapter, ChartSpec, SeriesData, ThemeTokens } from '../../types/visualizations';
export class TremorKpiAdapter implements ChartAdapter {
  private root: Root | null = null; private spec: ChartSpec | null = null; private lastData: SeriesData | null = null;
  mount(el: HTMLElement, spec: ChartSpec): void { if (this.root) return; this.spec = spec; this.root = createRoot(el); }
  update(data: SeriesData): void { this.lastData = data; this.render(); }
  resize(): void { /* Tremor cards are fluid. */ }
  setTheme(_tokens: ThemeTokens): void { this.render(); }
  destroy(): void { this.root?.unmount(); this.root = null; }
  private render(): void { const series = this.lastData?.series[0]; const value = series?.y.length ? series.y[series.y.length - 1] : '—'; if (this.root) this.root.render(<Card><Text>{this.spec?.encodings.yAxis[0] ?? ''}</Text><Metric>{value}</Metric></Card>); }
}
