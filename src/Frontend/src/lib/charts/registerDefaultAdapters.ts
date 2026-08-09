import { chartAdapterRegistry } from './adapterRegistry';
import { EChartsAdapter } from './echartsAdapter';
import { LightweightChartsAdapter } from './lightweightChartsAdapter';
import { TableAdapter } from './tableAdapter';
import { KpiAdapter } from './kpiAdapter';

let registered = false;

export function registerDefaultAdapters(): void {
  if (registered) return;
  for (const type of ['LINE', 'AREA', 'BAR', 'STACKED_BAR', 'SCATTER', 'HEATMAP', 'SPARK_LINE'] as const) chartAdapterRegistry.register(type, () => new EChartsAdapter());
  chartAdapterRegistry.register('CANDLESTICK', () => new LightweightChartsAdapter());
  chartAdapterRegistry.register('KPI_CARD', () => new KpiAdapter());
  chartAdapterRegistry.register('TABLE', () => new TableAdapter());
  registered = true;
}
