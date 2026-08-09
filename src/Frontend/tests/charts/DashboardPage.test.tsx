import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DashboardPage } from '../../src/components/dashboard/DashboardPage';
import { useAuthStore } from '../../src/lib/state/useAuthStore';
import type { DashboardSpecification } from '../../src/types/visualizations';

vi.mock('../../src/components/dashboard/DashboardGrid', () => ({
  DashboardGrid: () => <div data-testid="dashboard-grid" />
}));
vi.mock('../../src/components/visualizations/QueryBindingConfigurator', () => ({
  QueryBindingConfigurator: () => <div data-testid="query-binding-configurator" />
}));

afterEach(() => {
  vi.unstubAllGlobals();
  useAuthStore.getState().clearSession();
});

describe('DashboardPage', () => {
  it('renders a persisted dashboard with no widgets as an editable empty state', async () => {
    const dashboardId = '11111111-1111-1111-1111-111111111111';
    const emptyDashboard: DashboardSpecification = {
      dashboardId,
      title: 'Empty workspace',
      version: 3,
      theme: 'LIGHT',
      refreshRateMs: 60_000,
      gridLayout: { columns: 12, rowHeight: 56, items: [] },
      widgets: []
    };
    useAuthStore.getState().setSession({ accountKey: 'account-dashboard', actorId: dashboardId });
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      dashboardId,
      version: emptyDashboard.version,
      layout: emptyDashboard
    }), { status: 200, headers: { 'Content-Type': 'application/json' } })));
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    const view = render(<QueryClientProvider client={queryClient}><DashboardPage /></QueryClientProvider>);

    await screen.findByRole('heading', { name: 'Empty workspace' });
    expect(screen.getByTestId('dashboard-empty-state').textContent).toContain('No widgets configured');
    expect(screen.queryByTestId('query-binding-configurator')).toBeNull();
    expect((screen.getByRole('button', { name: 'Save dashboard' }) as HTMLButtonElement).disabled).toBe(false);

    view.unmount();
    queryClient.clear();
  });
});
