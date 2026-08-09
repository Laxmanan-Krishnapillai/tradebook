import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { GetDeliveryHistoryResponse } from '../../src/api/generated/types.gen';
import type { PhysicalDeliveryDetailsDto } from '../../src/api/generated/types.gen';
import {
  useCreateDelivery,
  useDeleteDelivery,
  useUpdateDelivery,
} from '../../src/lib/mutations/entityMutations';
import { createTradebookQueryClient } from '../../src/lib/query/queryClient';
import { queryKeys } from '../../src/lib/query/queryKeys';
import { useAuthStore } from '../../src/lib/state/useAuthStore';
import { DashboardPage } from '../../src/components/dashboard/DashboardPage';
import type { DashboardSpecification } from '../../src/types/visualizations';

vi.mock('../../src/components/dashboard/DashboardGrid', () => ({ DashboardGrid: () => <div data-testid="dashboard-grid" /> }));
vi.mock('../../src/components/visualizations/QueryBindingConfigurator', () => ({ QueryBindingConfigurator: () => <div /> }));

const original = {
  deliveryId: 'delivery-1', contractId: 'contract-1', contractInstanceId: 'instance-1', bookType: 'Sales',
  supplyMonth: '2026-01-01', volumeRealisedMwh: '10', status: 'Pending - No Invoice', version: 1,
  createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T00:00:00Z',
} as PhysicalDeliveryDetailsDto;
const authoritative = { ...original, volumeRealisedMwh: '25', status: 'Issue', version: 2 };
const listKey = queryKeys.deliveries.list({ page: 1, pageSize: 100 });
const dashboard: DashboardSpecification = {
  dashboardId: 'actor-dashboard', title: 'Private dashboard', description: '', version: 1, theme: 'LIGHT',
  refreshRateMs: 60_000, gridLayout: { columns: 12, rowHeight: 50, items: [{ widgetId: 'w', x: 0, y: 0, w: 4, h: 4 }] },
  widgets: [{ id: 'w', title: 'Widget', chartType: 'BAR', semanticModelRef: 'delivery_pnl_analytics', queryAst: { modelName: 'delivery_pnl_analytics' }, visualEncodings: { xAxis: 'x', yAxis: ['y'] } }],
};

function history(row = original): GetDeliveryHistoryResponse {
  return { items: [row], totalCount: 1, page: 1, pageSize: 100, hasNextPage: false };
}

async function mountHooks(client: QueryClient, conflict = vi.fn(), error = vi.fn()) {
  let create!: ReturnType<typeof useCreateDelivery>;
  let update!: ReturnType<typeof useUpdateDelivery>;
  let remove!: ReturnType<typeof useDeleteDelivery>;
  function Harness() {
    create = useCreateDelivery(error);
    update = useUpdateDelivery(conflict, error);
    remove = useDeleteDelivery(conflict, error);
    return null;
  }
  const host = document.createElement('div');
  const root: Root = createRoot(host);
  await act(async () => root.render(<QueryClientProvider client={client}><Harness /></QueryClientProvider>));
  return { create: () => create, update: () => update, remove: () => remove, root };
}

describe('mutation safety and reconciliation', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    useAuthStore.getState().clearSession();
  });

  it('has mutation retries disabled globally and performs one network attempt', async () => {
    const client = createTradebookQueryClient();
    expect(client.getDefaultOptions().mutations?.retry).toBe(false);
    client.setQueryData(listKey, history());
    const fetchMock = vi.fn().mockRejectedValue(new TypeError('network down'));
    vi.stubGlobal('fetch', fetchMock);
    const hooks = await mountHooks(client);

    await act(async () => {
      await expect(hooks.update().mutateAsync({ id: original.deliveryId, version: 1, changes: { status: 'Issue' } })).rejects.toThrow('network down');
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(client.getQueryData<GetDeliveryHistoryResponse>(listKey)?.items[0]).toEqual(original);
    await act(async () => hooks.root.unmount());
  });

  it('installs authoritative success into paged-list and detail caches', async () => {
    const client = createTradebookQueryClient();
    client.setQueryData(listKey, history());
    client.setQueryData(queryKeys.deliveries.detail(original.deliveryId), original);
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(authoritative), { status: 200, headers: { 'Content-Type': 'application/json' } })));
    const hooks = await mountHooks(client);

    await act(async () => {
      await hooks.update().mutateAsync({ id: original.deliveryId, version: 1, changes: { status: 'Issue' } });
    });

    expect(client.getQueryData<GetDeliveryHistoryResponse>(listKey)?.items[0]).toEqual(authoritative);
    expect(client.getQueryData(queryKeys.deliveries.detail(original.deliveryId))).toEqual(authoritative);
    await act(async () => hooks.root.unmount());
  });

  it('restores create/delete snapshots after failures', async () => {
    const client = createTradebookQueryClient();
    client.setQueryData(listKey, history());
    client.setQueryData(queryKeys.deliveries.detail(original.deliveryId), original);
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('offline')));
    const hooks = await mountHooks(client);

    await act(async () => {
      await expect(hooks.create().mutateAsync({ contractId: 'contract-2', bookType: 'Sourcing', supplyMonth: '2026-02-01' })).rejects.toThrow('offline');
      await expect(hooks.remove().mutateAsync({ id: original.deliveryId, version: 1, reason: 'cancel' })).rejects.toThrow('offline');
    });

    expect(client.getQueryData<GetDeliveryHistoryResponse>(listKey)).toEqual(history());
    expect(client.getQueryData(queryKeys.deliveries.detail(original.deliveryId))).toEqual(original);
    await act(async () => hooks.root.unmount());
  });

  it('installs current server truth and exposes it on HTTP 409', async () => {
    const client = createTradebookQueryClient();
    client.setQueryData(listKey, history());
    client.setQueryData(queryKeys.deliveries.detail(original.deliveryId), original);
    const conflict = vi.fn();
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(authoritative), { status: 409, headers: { 'Content-Type': 'application/json' } })));
    const hooks = await mountHooks(client, conflict);

    await act(async () => {
      await expect(hooks.update().mutateAsync({ id: original.deliveryId, version: 1, changes: { status: 'Awaiting' } })).rejects.toMatchObject({ status: 409 });
    });

    expect(conflict).toHaveBeenCalledWith(original.deliveryId, authoritative);
    expect(client.getQueryData<GetDeliveryHistoryResponse>(listKey)?.items[0]).toEqual(authoritative);
    expect(client.getQueryData(queryKeys.deliveries.detail(original.deliveryId))).toEqual(authoritative);
    await act(async () => hooks.root.unmount());
  });

  it('does not let a late actor A mutation repopulate actor B cache data', async () => {
    const client = createTradebookQueryClient();
    useAuthStore.getState().setSession({ accountKey: 'account-a', actorId: 'actor-a' });
    client.setQueryData(listKey, history());
    let resolve!: (response: Response) => void;
    const fetchMock = vi.fn(() => new Promise<Response>((next) => { resolve = next; }));
    vi.stubGlobal('fetch', fetchMock);
    const hooks = await mountHooks(client);
    let pending!: Promise<PhysicalDeliveryDetailsDto>;
    await act(async () => {
      pending = hooks.update().mutateAsync({ id: original.deliveryId, version: 1, changes: { status: 'Issue' } });
      await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledOnce());
    });

    useAuthStore.getState().setSession({ accountKey: 'account-b', actorId: 'actor-b' });
    const actorB = { ...original, contractInstanceId: 'private-b', version: 8 };
    client.clear();
    client.setQueryData(listKey, history(actorB));
    resolve(new Response(JSON.stringify(authoritative), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    await act(async () => { await pending; });

    expect(client.getQueryData<GetDeliveryHistoryResponse>(listKey)?.items[0]).toEqual(actorB);
    await act(async () => hooks.root.unmount());
  });

  it('shows the dashboard conflict prompt and installs authoritative 409 layout', async () => {
    const client = createTradebookQueryClient();
    useAuthStore.getState().setSession({ accountKey: 'account-dashboard', actorId: dashboard.dashboardId });
    const serverDashboard = { ...dashboard, title: 'Server dashboard', version: 2 };
    vi.stubGlobal('fetch', vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ dashboardId: dashboard.dashboardId, version: 1, layout: dashboard }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ dashboardId: dashboard.dashboardId, version: 2, layout: serverDashboard }), { status: 409, headers: { 'Content-Type': 'application/json' } }))
      .mockResolvedValue(new Response(JSON.stringify({ dashboardId: dashboard.dashboardId, version: 2, layout: serverDashboard }), { status: 200, headers: { 'Content-Type': 'application/json' } })));

    const view = render(<QueryClientProvider client={client}><DashboardPage /></QueryClientProvider>);
    await screen.findByRole('heading', { name: 'Private dashboard' });
    fireEvent.click(screen.getByRole('button', { name: 'Save dashboard' }));

    expect((await screen.findByTestId('conflict-prompt')).textContent).toContain('Server dashboard');
    expect(client.getQueryData(queryKeys.dashboards.detail(dashboard.dashboardId))).toEqual(serverDashboard);
    view.unmount();
  });
});
