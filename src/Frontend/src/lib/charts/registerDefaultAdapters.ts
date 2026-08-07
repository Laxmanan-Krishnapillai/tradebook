import { chartAdapterRegistry } from './adapterRegistry';
import { EChartsAdapter } from './echartsAdapter';
import { LightweightChartsAdapter } from './lightweightChartsAdapter';
import { TableAdapter } from './tableAdapter';
import { TremorKpiAdapter } from './tremorKpiAdapter';
for (const type of ['LINE', 'AREA', 'BAR', 'STACKED_BAR', 'SCATTER', 'HEATMAP', 'SPARK_LINE'] as const) chartAdapterRegistry.register(type, () => new EChartsAdapter());
chartAdapterRegistry.register('CANDLESTICK', () => new LightweightChartsAdapter());
chartAdapterRegistry.register('KPI_CARD', () => new TremorKpiAdapter());
chartAdapterRegistry.register('TABLE', () => new TableAdapter());
