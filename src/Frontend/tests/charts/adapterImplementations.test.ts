import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ChartSpec, SeriesData, ThemeTokens } from '../../src/types/visualizations';

const engine = vi.hoisted(() => ({
  dispose: vi.fn(), setOption: vi.fn(), resize: vi.fn(), isDisposed: vi.fn(() => false),
  remove: vi.fn(), applyOptions: vi.fn(), setData: vi.fn(),
  render: vi.fn(), unmount: vi.fn()
}));

vi.mock('echarts', () => ({ init: vi.fn(() => ({ dispose: engine.dispose, setOption: engine.setOption, resize: engine.resize, isDisposed: engine.isDisposed })) }));
vi.mock('lightweight-charts', () => ({ CandlestickSeries: {}, createChart: vi.fn(() => ({ addSeries: () => ({ setData: engine.setData }), applyOptions: engine.applyOptions, remove: engine.remove })) }));
vi.mock('react-dom/client', () => ({ createRoot: vi.fn(() => ({ render: engine.render, unmount: engine.unmount })) }));
vi.mock('@tremor/react/dist/components/layout-elements/Card/Card.js', () => ({ default: 'div' }));
vi.mock('@tremor/react/dist/components/text-elements/Metric/Metric.js', () => ({ default: 'strong' }));
vi.mock('@tremor/react/dist/components/text-elements/Text/Text.js', () => ({ default: 'span' }));

import { EChartsAdapter } from '../../src/lib/charts/echartsAdapter';
import { LightweightChartsAdapter } from '../../src/lib/charts/lightweightChartsAdapter';
import { TableAdapter } from '../../src/lib/charts/tableAdapter';
import { KpiAdapter } from '../../src/lib/charts/kpiAdapter';

const spec: ChartSpec = { chartType: 'BAR', encodings: { xAxis: 'date', yAxis: ['value'] } };
const data: SeriesData = { series: [{ name: 'value', x: [Date.parse('2026-01-01')], y: [2] }], ohlc: [{ time: Date.parse('2026-01-01'), open: 1, high: 3, low: 1, close: 2 }] };
const theme: ThemeTokens = { background: '#fff', textPrimary: '#111', textSecondary: '#555', gridLine: '#ddd', axisLine: '#aaa', seriesPalette: ['#00f'], positive: '#0f0', negative: '#f00', fontFamily: 'sans' };

describe('registered adapter implementations', () => {
  beforeEach(() => vi.clearAllMocks());

  it('disposes the ECharts engine', () => {
    const adapter = new EChartsAdapter();
    adapter.mount(document.createElement('div'), spec); adapter.setTheme(theme); adapter.update(data); adapter.resize(); adapter.destroy();
    expect(engine.setOption).toHaveBeenCalled(); expect(engine.resize).toHaveBeenCalled(); expect(engine.dispose).toHaveBeenCalledOnce();
  });

  it('removes the Lightweight Charts v5 instance', () => {
    const adapter = new LightweightChartsAdapter();
    adapter.mount(document.createElement('div'), { ...spec, chartType: 'CANDLESTICK' }); adapter.setTheme(theme); adapter.update(data); adapter.resize(); adapter.destroy();
    expect(engine.setData).toHaveBeenCalled(); expect(engine.applyOptions).toHaveBeenCalled(); expect(engine.remove).toHaveBeenCalledOnce();
  });

  it('cleans up table DOM and the Tremor React root', () => {
    const tableHost = document.createElement('div'); const table = new TableAdapter(); table.mount(tableHost, { ...spec, chartType: 'TABLE' }); table.update(data); expect(engine.render).toHaveBeenCalledOnce(); table.destroy(); expect(engine.unmount).toHaveBeenCalledOnce();
    const kpi = new KpiAdapter(); kpi.mount(document.createElement('div'), { ...spec, chartType: 'KPI_CARD' }); kpi.update(data); kpi.destroy(); expect(engine.render).toHaveBeenCalledTimes(2); expect(engine.unmount).toHaveBeenCalledTimes(2);
  });
});
