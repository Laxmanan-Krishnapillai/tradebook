import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { describe, expect, it, vi } from 'vitest';
import { useChartAdapter } from '../../src/hooks/useChartAdapter';
import { chartAdapterRegistry } from '../../src/lib/charts/adapterRegistry';
import type { ChartAdapter, SeriesData, ThemeTokens } from '../../src/types/visualizations';

const theme: ThemeTokens = { background: '#fff', textPrimary: '#111', textSecondary: '#555', gridLine: '#ddd', axisLine: '#aaa', seriesPalette: ['#00f'], positive: '#0f0', negative: '#f00', fontFamily: 'sans' };

describe('useChartAdapter', () => {
  it('routes data through the shared pool and destroys the mounted adapter', async () => {
    const lifecycle: string[] = [];
    const adapter: ChartAdapter = { mount: vi.fn(), update: vi.fn(() => lifecycle.push('update')), resize: vi.fn(), setTheme: vi.fn(), destroy: vi.fn() };
    chartAdapterRegistry.register('BAR', () => adapter);
    const data: SeriesData = { series: [{ name: 'value', x: Array.from({ length: 5001 }, (_, index) => index), y: Array.from({ length: 5001 }, (_, index) => index) }] };
    const pool = { downsample: vi.fn(async (value: SeriesData) => ({ ...value, series: [{ ...value.series[0], x: value.series[0].x.slice(0, 5000), y: Array.from(value.series[0].y).slice(0, 5000) }] })) };
    function Harness() { const ref = useChartAdapter('BAR', { chartType: 'BAR', encodings: { xAxis: 'date', yAxis: ['value'] } }, data, theme, { workerPool: pool, onRenderStart: () => lifecycle.push('start'), onRenderReady: () => lifecycle.push('ready') }); return <div ref={ref} />; }
    const host = document.createElement('div');
    const root = createRoot(host);
    await act(async () => root.render(<Harness />));
    await vi.waitFor(() => expect(adapter.update).toHaveBeenCalled());
    expect(pool.downsample).toHaveBeenCalledWith(data);
    expect(adapter.mount).toHaveBeenCalledOnce();
    expect(adapter.setTheme).toHaveBeenCalled();
    expect(lifecycle).toEqual(['start', 'update', 'ready']);
    await act(async () => root.unmount());
    expect(adapter.destroy).toHaveBeenCalledOnce();
  });

  it('destroys a disabled adapter and mounts a fresh adapter when re-enabled', async () => {
    const adapters: ChartAdapter[] = [];
    chartAdapterRegistry.register('LINE', () => {
      const adapter: ChartAdapter = { mount: vi.fn(), update: vi.fn(), resize: vi.fn(), setTheme: vi.fn(), destroy: vi.fn() };
      adapters.push(adapter);
      return adapter;
    });
    const data: SeriesData = { series: [{ name: 'value', x: [1], y: [2] }] };
    const pool = { downsample: vi.fn(async (value: SeriesData) => value) };

    function Harness({ enabled }: { enabled: boolean }) {
      const ref = useChartAdapter(
        'LINE',
        { chartType: 'LINE', encodings: { xAxis: 'date', yAxis: ['value'] } },
        data,
        theme,
        { enabled, workerPool: pool }
      );
      return enabled ? <div ref={ref} /> : <p role="alert">Chart unavailable</p>;
    }

    const host = document.createElement('div');
    const root = createRoot(host);
    await act(async () => root.render(<Harness enabled />));
    await vi.waitFor(() => expect(adapters[0].update).toHaveBeenCalledOnce());

    await act(async () => root.render(<Harness enabled={false} />));
    expect(adapters[0].destroy).toHaveBeenCalledOnce();

    await act(async () => root.render(<Harness enabled />));
    await vi.waitFor(() => expect(adapters[1].update).toHaveBeenCalledOnce());
    expect(adapters).toHaveLength(2);
    expect(adapters[1].mount).toHaveBeenCalledOnce();

    await act(async () => root.unmount());
    expect(adapters[1].destroy).toHaveBeenCalledOnce();
  });
});
