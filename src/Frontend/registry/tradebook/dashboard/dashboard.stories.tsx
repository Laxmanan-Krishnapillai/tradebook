import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { Meta, StoryObj } from '@storybook/react-vite';
import type { CSSProperties } from 'react';
import { useEffect, useMemo } from 'react';
import { createDefaultDashboard, DashboardPage } from '../../../src/components/dashboard/DashboardPage';
import { registerDefaultAdapters } from '../../../src/lib/charts/registerDefaultAdapters';
import { queryKeys } from '../../../src/lib/query/queryKeys';
import { useAuthStore } from '../../../src/lib/state/useAuthStore';

const dashboardId = '11111111-1111-1111-1111-111111111111';
registerDefaultAdapters();

function DashboardReferenceStory() {
  const client = useMemo(() => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false, staleTime: Infinity } }
    });
    const dashboard = createDefaultDashboard(dashboardId);
    queryClient.setQueryData(queryKeys.dashboards.detail(dashboardId), dashboard);
    const results = new Map<string, { columns: string[]; rows: unknown[][] }>([
      ['revenue-total', { columns: ['revenue_eur'], rows: [[6_440_000]] }],
      ['volume-total', { columns: ['volume_mwh'], rows: [[118_420]] }],
      ['invoice-total', { columns: ['invoice_amount_eur'], rows: [[5_872_400]] }],
      ['delivery-count', { columns: ['delivery_count'], rows: [[1_284]] }],
      ['monthly-revenue', {
        columns: ['supply_month_month', 'revenue_eur'],
        rows: [
          ['Sep', 3_200_000], ['Oct', 3_800_000], ['Nov', 4_700_000], ['Dec', 5_200_000],
          ['Jan', 5_600_000], ['Feb', 4_500_000], ['Mar', 4_200_000], ['Apr', 3_700_000],
          ['May', 3_100_000], ['Jun', 3_500_000], ['Jul', 4_400_000], ['Aug', 6_440_000]
        ]
      }],
      ['delivery-volume', {
        columns: ['contract_instance_id', 'volume_mwh'],
        rows: [['Vattenfall', 24_800], ['Equinor', 21_150], ['Gasum', 18_990], ['Shell Energy', 14_200], ['RWE', 12_600], ['Fortum', 9_600]]
      }]
    ]);
    dashboard.widgets.forEach((widget) => {
      queryClient.setQueryData(queryKeys.analytics.result(widget.id, widget.queryAst), results.get(widget.id));
    });
    return queryClient;
  }, []);

  useEffect(() => {
    const root = document.documentElement;
    const wasDark = root.classList.contains('dark');
    const previousZoom = root.style.zoom;
    root.classList.add('dark');
    root.style.zoom = '0.875';
    return () => {
      if (!wasDark) root.classList.remove('dark');
      root.style.zoom = previousZoom;
    };
  }, []);

  useAuthStore.getState().setSession({ accountKey: 'dashboard-story', actorId: dashboardId });
  return <div className="dark workspace" style={{ '--workspace-inline-size': '100vw', height: '100dvh', width: '100%' } as CSSProperties}>
    <QueryClientProvider client={client}><DashboardPage /></QueryClientProvider>
  </div>;
}

const meta = {
  title: 'Tradebook/Dashboard',
  component: DashboardReferenceStory,
  parameters: { layout: 'fullscreen' }
} satisfies Meta<typeof DashboardReferenceStory>;

export default meta;
type Story = StoryObj<typeof meta>;
export const DeliveryPerformance: Story = {};
