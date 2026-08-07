import type { ColumnDef } from '@tanstack/react-table';
import { createRoot, type Root } from 'react-dom/client';
import { VirtualizedDataTable } from '../grid/VirtualizedDataTable';
import type { ChartAdapter, ChartSpec, SeriesData, ThemeTokens } from '../../types/visualizations';

type SeriesRow = Record<string, string | number> & { x: string | number };

export class TableAdapter implements ChartAdapter {
  private root: Root | null = null;
  private element: HTMLElement | null = null;
  mount(el: HTMLElement, _spec: ChartSpec): void { if (this.root) return; this.element = el; this.root = createRoot(el); }
  update(data: SeriesData): void {
    if (!this.root) return;
    const first = data.series[0];
    const rows: SeriesRow[] = (first?.x ?? []).map((x, index) => Object.assign({ x }, ...data.series.map((series) => ({ [series.name]: Number(series.y[index]) }))));
    const columns: ColumnDef<SeriesRow>[] = [
      { accessorKey: 'x', header: 'Category' },
      ...data.series.map((series) => ({ accessorKey: series.name, header: series.name }))
    ];
    this.root.render(<VirtualizedDataTable data={rows} columns={columns} height={320} getRowId={(row) => String(row.x)} />);
  }
  resize(): void { }
  setTheme(tokens: ThemeTokens): void { if (this.element) { this.element.style.background = tokens.background; this.element.style.color = tokens.textPrimary; } }
  destroy(): void { this.root?.unmount(); this.root = null; this.element = null; }
}
