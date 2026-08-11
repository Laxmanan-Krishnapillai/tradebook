import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { resolveDashboardThemeTokens } from '../../src/components/dashboard/DashboardGrid';
import { createDefaultDashboard, DashboardPage } from '../../src/components/dashboard/DashboardPage';
import { useAuthStore } from '../../src/lib/state/useAuthStore';
import type { DashboardSpecification } from '../../src/types/visualizations';

vi.mock('../../src/components/dashboard/DashboardGrid', async (importOriginal) => ({
  ...await importOriginal<typeof import('../../src/components/dashboard/DashboardGrid')>(),
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
  it('derives chart colors from the dashboard design-token scope', () => {
    const scope = document.createElement('div');
    scope.style.cssText = [
      '--surface-raised: rgb(1, 2, 3)',
      '--foreground: rgb(4, 5, 6)',
      '--muted-foreground: rgb(7, 8, 9)',
      '--border: rgb(10, 11, 12)',
      '--border-strong: rgb(13, 14, 15)',
      '--accent-500: rgb(16, 17, 18)',
      '--buy-500: rgb(19, 20, 21)',
      '--warn-500: rgb(22, 23, 24)',
      '--sell-500: rgb(25, 26, 27)',
      'font-family: Test Sans',
    ].join(';');
    document.body.appendChild(scope);

    expect(resolveDashboardThemeTokens(scope)).toEqual({
      background: 'rgb(1, 2, 3)',
      textPrimary: 'rgb(4, 5, 6)',
      textSecondary: 'rgb(7, 8, 9)',
      gridLine: 'rgb(10, 11, 12)',
      axisLine: 'rgb(13, 14, 15)',
      seriesPalette: ['rgb(16, 17, 18)', 'rgb(19, 20, 21)', 'rgb(22, 23, 24)'],
      positive: 'rgb(19, 20, 21)',
      negative: 'rgb(25, 26, 27)',
      fontFamily: 'Test Sans',
    });
    scope.remove();
  });

  it('uses a trend chart and a concise contract ranking in the default dashboard', () => {
    const dashboard = createDefaultDashboard('dashboard-user');
    expect(dashboard.theme).toBe('SYSTEM');
    expect(dashboard.widgets.find((widget) => widget.id === 'monthly-revenue')).toMatchObject({ chartType: 'AREA' });
    expect(dashboard.widgets.find((widget) => widget.id === 'delivery-volume')?.queryAst.limit).toBe(8);
  });

  it('renders a persisted dashboard with no widgets and exposes its editor on demand', async () => {
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
    const dashboardPage = screen.getByRole('region', { name: 'Dashboard' });
    expect(dashboardPage?.classList.contains('light')).toBe(true);
    expect(dashboardPage?.getAttribute('data-dashboard-theme')).toBe('light');
    expect(screen.getByTestId('dashboard-empty-state').textContent).toContain('No widgets configured');
    expect(screen.queryByTestId('query-binding-configurator')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Edit layout' }));
    expect((screen.getByRole('button', { name: 'Save dashboard' }) as HTMLButtonElement).disabled).toBe(false);

    view.unmount();
    queryClient.clear();
  });
});
