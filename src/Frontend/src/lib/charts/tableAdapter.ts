import type { ChartAdapter, ChartSpec, SeriesData, ThemeTokens } from '../../types/visualizations';
export class TableAdapter implements ChartAdapter {
  private el: HTMLElement | null = null;
  mount(el: HTMLElement, _spec: ChartSpec): void { this.el = el; }
  update(data: SeriesData): void { if (!this.el) return; this.el.replaceChildren(...data.series.map((series) => { const table = document.createElement('table'); const caption = document.createElement('caption'); caption.textContent = series.name; table.append(caption); series.x.forEach((x, index) => { const row = table.insertRow(); row.insertCell().textContent = String(x); row.insertCell().textContent = String(series.y[index] ?? ''); }); return table; })); }
  resize(): void { }
  setTheme(_tokens: ThemeTokens): void { }
  destroy(): void { this.el?.replaceChildren(); this.el = null; }
}
