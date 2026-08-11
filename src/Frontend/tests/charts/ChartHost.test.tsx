import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  ChartHost,
  MIN_CHART_REFRESH_INTERVAL_MS,
  resolveChartRefreshInterval
} from '../../src/components/visualizations/ChartHost';
import { chartAdapterRegistry } from '../../src/lib/charts/adapterRegistry';
import { resolveApiUrl } from '../../src/lib/api/client';
import { sharedLttbWorkerPool } from '../../src/lib/workers/workerPool';
import { server } from '../../src/mocks/server';
import type { ChartAdapter, ChartWidgetConfig, SeriesData } from '../../src/types/visualizations';

function widget(limit: number): ChartWidgetConfig {
  return {
    id: 'chart-1',
    title: 'Volumes',
    chartType: 'LINE',
    semanticModelRef: 'delivery_pnl_analytics',
    queryAst: {
      modelName: 'delivery_pnl_analytics',
      dimensions: ['supply_month'],
      measures: ['volume_mwh'],
      limit
    },
    visualEncodings: { xAxis: 'supply_month', yAxis: ['volume_mwh'] }
  };
}

afterEach(() => vi.restoreAllMocks());

describe('ChartHost', () => {
  it('destroys its adapter on a query error and mounts a fresh one after recovery', async () => {
    server.use(http.post(resolveApiUrl('/api/v1/analytics/query'), async ({ request }) => {
      const body = await request.json() as { limit?: number };
      if (body.limit === 2) return HttpResponse.json({ title: 'failed' }, { status: 500 });
      return HttpResponse.json({ columns: ['supply_month', 'volume_mwh'], rows: [['2026-01-01', body.limit ?? 0]] });
    }));

    const adapters: ChartAdapter[] = [];
    chartAdapterRegistry.register('LINE', () => {
      const adapter: ChartAdapter = { mount: vi.fn(), update: vi.fn(), resize: vi.fn(), setTheme: vi.fn(), destroy: vi.fn() };
      adapters.push(adapter);
      return adapter;
    });
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const view = render(
      <QueryClientProvider client={queryClient}>
        <ChartHost widget={widget(1)} />
      </QueryClientProvider>
    );

    expect(screen.getByRole('status', { name: 'Loading Volumes' })).toBeTruthy();
    await waitFor(() => expect(adapters[0].update).toHaveBeenCalledOnce());
    expect(screen.getByText('MWh · Top 1')).toBeTruthy();
    expect(screen.queryByText(/delivery_pnl_analytics/)).toBeNull();
    view.rerender(
      <QueryClientProvider client={queryClient}>
        <ChartHost widget={widget(2)} />
      </QueryClientProvider>
    );
    await screen.findByRole('alert');
    await waitFor(() => expect(adapters[0].destroy).toHaveBeenCalledOnce());

    view.rerender(
      <QueryClientProvider client={queryClient}>
        <ChartHost widget={widget(3)} />
      </QueryClientProvider>
    );
    await waitFor(() => expect(adapters).toHaveLength(2));
    await waitFor(() => expect(adapters[1].update).toHaveBeenCalledOnce());
    expect(adapters[1].mount).toHaveBeenCalledOnce();

    view.unmount();
    expect(adapters[1].destroy).toHaveBeenCalledOnce();
    queryClient.clear();
  });

  it('disables polling by default and clamps enabled refresh rates', () => {
    expect(resolveChartRefreshInterval(undefined)).toBe(false);
    expect(resolveChartRefreshInterval(0)).toBe(false);
    expect(resolveChartRefreshInterval(-1)).toBe(false);
    expect(resolveChartRefreshInterval(Number.NaN)).toBe(false);
    expect(resolveChartRefreshInterval(1)).toBe(MIN_CHART_REFRESH_INTERVAL_MS);
    expect(resolveChartRefreshInterval(30_000)).toBe(30_000);
  });

  it('marks a render ready only after downsampling and the adapter update complete', async () => {
    server.use(http.post(resolveApiUrl('/api/v1/analytics/query'), async ({ request }) => {
      const body = await request.json() as { limit?: number };
      return HttpResponse.json({ columns: ['supply_month', 'volume_mwh'], rows: [['2026-01-01', body.limit ?? 0]] });
    }));

    const adapter: ChartAdapter = { mount: vi.fn(), update: vi.fn(), resize: vi.fn(), setTheme: vi.fn(), destroy: vi.fn() };
    chartAdapterRegistry.register('LINE', () => adapter);
    const pending: Array<() => void> = [];
    vi.spyOn(sharedLttbWorkerPool, 'downsample').mockImplementation((data: SeriesData) => new Promise((resolve) => {
      pending.push(() => resolve(data));
    }));
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const view = render(
      <QueryClientProvider client={queryClient}>
        <ChartHost widget={widget(10)} />
      </QueryClientProvider>
    );
    const surface = () => view.container.querySelector<HTMLElement>('[data-chart-widget-id="chart-1"]')!;

    expect(surface().dataset.chartRenderState).toBe('loading');
    await waitFor(() => expect(pending).toHaveLength(1));
    expect(surface().dataset.chartRenderState).toBe('rendering');
    expect(surface().getAttribute('aria-busy')).toBe('true');
    expect(adapter.update).not.toHaveBeenCalled();

    await act(async () => pending.shift()?.());
    await waitFor(() => expect(surface().dataset.chartRenderState).toBe('ready'));
    expect(adapter.update).toHaveBeenCalledOnce();
    expect(surface().dataset.chartRenderSequence).toBe('1');
    expect(Number(surface().dataset.chartRenderCompletedAtMs)).toBeGreaterThan(0);
    expect(surface().getAttribute('aria-busy')).toBe('false');

    view.rerender(
      <QueryClientProvider client={queryClient}>
        <ChartHost widget={widget(11)} />
      </QueryClientProvider>
    );
    await waitFor(() => expect(pending).toHaveLength(1));
    expect(surface().dataset.chartRenderState).toBe('rendering');
    await act(async () => pending.shift()?.());
    await waitFor(() => expect(surface().dataset.chartRenderSequence).toBe('2'));
    expect(surface().dataset.chartRenderState).toBe('ready');
    expect(adapter.update).toHaveBeenCalledTimes(2);

    view.unmount();
    queryClient.clear();
  });
});
