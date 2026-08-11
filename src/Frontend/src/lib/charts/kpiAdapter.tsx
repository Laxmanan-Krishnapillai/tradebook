import NumberFlow from '@number-flow/react';
import { createRoot, type Root } from 'react-dom/client';
import type { ChartAdapter, ChartSpec, SeriesData, ThemeTokens } from '../../types/visualizations';

function metricUnit(member: string | undefined): string | undefined {
  if (member?.endsWith('_eur')) return 'EUR';
  if (member?.endsWith('_mwh')) return 'MWh';
  return undefined;
}

export class KpiAdapter implements ChartAdapter {
  private root: Root | null = null; private spec: ChartSpec | null = null; private lastData: SeriesData | null = null;
  mount(el: HTMLElement, spec: ChartSpec): void { if (this.root) return; this.spec = spec; this.root = createRoot(el); }
  update(data: SeriesData): void { this.lastData = data; this.render(); }
  resize(): void { /* KPI tiles are fluid. */ }
  setTheme(_tokens: ThemeTokens): void { this.render(); }
  destroy(): void { this.root?.unmount(); this.root = null; }
  private render(): void {
    const series = this.lastData?.series[0];
    const value = Number(series?.y.length ? series.y[series.y.length - 1] : Number.NaN);
    const unit = metricUnit(this.spec?.encodings.yAxis[0]);
    if (this.root) this.root.render(
      <article className="kpi-tile">
        {Number.isFinite(value)
          ? <NumberFlow animated className="kpi-value" format={{ maximumFractionDigits: 2, notation: 'compact' }} respectMotionPreference value={value} />
          : <span className="kpi-value">—</span>}
        {unit ? <span className="kpi-unit">{unit}</span> : null}
      </article>
    );
  }
}
