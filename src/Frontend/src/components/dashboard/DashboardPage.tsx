import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { useLastRealtimeEvent } from '../../hooks/useRealtimeQuerySync';
import { getSemanticValueMembers } from '../../lib/analytics/semanticModelCatalog';
import { ApiError } from '../../lib/api/client';
import { getDashboard, saveDashboard } from '../../lib/api/dashboardClient';
import { queryKeys } from '../../lib/query/queryKeys';
import { useAuthStore } from '../../lib/state/useAuthStore';
import type { DashboardSpecification } from '../../types/visualizations';
import { ConflictDialog } from '../ui/ConflictDialog';
import { Button } from '../ui/button';
import { Input } from '../ui/input';
import { NumberInput } from '../ui/number-input';
import { Select } from '../ui/select';
import { QueryBindingConfigurator } from '../visualizations/QueryBindingConfigurator';
import { DashboardGrid } from './DashboardGrid';
import 'react-grid-layout/css/styles.css';
import 'react-resizable/css/styles.css';

export function createDefaultDashboard(dashboardId: string): DashboardSpecification {
  return {
    dashboardId,
    title: 'Delivery performance',
    description: 'Revenue and volume from the canonical delivery P&L semantic model.',
    version: 0,
    theme: 'SYSTEM',
    refreshRateMs: 60_000,
    gridLayout: {
      columns: 24,
      rowHeight: 38,
      items: [
        { widgetId: 'revenue-total', x: 0, y: 0, w: 6, h: 2, minW: 4, minH: 2 },
        { widgetId: 'volume-total', x: 6, y: 0, w: 6, h: 2, minW: 4, minH: 2 },
        { widgetId: 'invoice-total', x: 12, y: 0, w: 6, h: 2, minW: 4, minH: 2 },
        { widgetId: 'delivery-count', x: 18, y: 0, w: 6, h: 2, minW: 4, minH: 2 },
        { widgetId: 'monthly-revenue', x: 0, y: 2, w: 15, h: 16, minW: 8, minH: 6 },
        { widgetId: 'delivery-volume', x: 15, y: 2, w: 9, h: 16, minW: 8, minH: 6 }
      ]
    },
    widgets: [
      {
        id: 'revenue-total',
        title: 'Revenue, month to date',
        chartType: 'KPI_CARD',
        semanticModelRef: 'delivery_pnl_analytics',
        queryAst: { modelName: 'delivery_pnl_analytics', measures: ['revenue_eur'], limit: 1 },
        visualEncodings: { xAxis: 'revenue_eur', yAxis: ['revenue_eur'] }
      },
      {
        id: 'volume-total',
        title: 'Volume delivered MWh',
        chartType: 'KPI_CARD',
        semanticModelRef: 'delivery_pnl_analytics',
        queryAst: { modelName: 'delivery_pnl_analytics', measures: ['volume_mwh'], limit: 1 },
        visualEncodings: { xAxis: 'volume_mwh', yAxis: ['volume_mwh'] }
      },
      {
        id: 'invoice-total',
        title: 'Invoiced value',
        chartType: 'KPI_CARD',
        semanticModelRef: 'delivery_pnl_analytics',
        queryAst: { modelName: 'delivery_pnl_analytics', measures: ['invoice_amount_eur'], limit: 1 },
        visualEncodings: { xAxis: 'invoice_amount_eur', yAxis: ['invoice_amount_eur'] }
      },
      {
        id: 'delivery-count',
        title: 'Deliveries',
        chartType: 'KPI_CARD',
        semanticModelRef: 'delivery_pnl_analytics',
        queryAst: { modelName: 'delivery_pnl_analytics', measures: ['delivery_count'], limit: 1 },
        visualEncodings: { xAxis: 'delivery_count', yAxis: ['delivery_count'] }
      },
      {
        id: 'monthly-revenue',
        title: 'Monthly revenue',
        chartType: 'BAR',
        semanticModelRef: 'delivery_pnl_analytics',
        queryAst: { modelName: 'delivery_pnl_analytics', measures: ['revenue_eur'], timeDimensions: [{ dimension: 'supply_month', granularity: 'month' }], sorts: [{ member: 'supply_month_month', direction: 'asc' }], limit: 120 },
        visualEncodings: { xAxis: 'supply_month_month', yAxis: ['revenue_eur'] },
        styleOverrides: { showLegend: false, showGridlines: true, opacity: 0.25 }
      },
      {
        id: 'delivery-volume',
        title: 'Volume by contract',
        chartType: 'BAR',
        semanticModelRef: 'delivery_pnl_analytics',
        queryAst: { modelName: 'delivery_pnl_analytics', dimensions: ['contract_instance_id'], measures: ['volume_mwh'], sorts: [{ member: 'volume_mwh', direction: 'desc' }], limit: 20 },
        visualEncodings: { xAxis: 'contract_instance_id', yAxis: ['volume_mwh'] },
        styleOverrides: { showLegend: false, showGridlines: true }
      }
    ]
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function isStringArray(value: unknown): value is string[] {
  return Array.isArray(value) && value.every((item) => typeof item === 'string');
}

function isDashboardSpecification(value: unknown): value is DashboardSpecification {
  if (!isRecord(value) || !isRecord(value.gridLayout) || !Array.isArray(value.widgets)) return false;
  const theme = value.theme;
  return typeof value.dashboardId === 'string'
    && typeof value.title === 'string'
    && (value.description === undefined || typeof value.description === 'string')
    && typeof value.version === 'number'
    && (theme === 'DARK' || theme === 'LIGHT' || theme === 'SYSTEM')
    && typeof value.refreshRateMs === 'number'
    && typeof value.gridLayout.columns === 'number'
    && typeof value.gridLayout.rowHeight === 'number'
    && Array.isArray(value.gridLayout.items)
    && value.widgets.every((widget) => isRecord(widget)
      && typeof widget.id === 'string'
      && typeof widget.title === 'string'
      && typeof widget.chartType === 'string'
      && typeof widget.semanticModelRef === 'string'
      && isRecord(widget.queryAst)
      && typeof widget.queryAst.modelName === 'string'
      && isRecord(widget.visualEncodings)
      && typeof widget.visualEncodings.xAxis === 'string'
      && isStringArray(widget.visualEncodings.yAxis));
}

function conflictDashboard(error: unknown): DashboardSpecification | undefined {
  if (!(error instanceof ApiError) || error.status !== 409 || !isRecord(error.problem)) return undefined;
  return isDashboardSpecification(error.problem.layout) ? error.problem.layout : undefined;
}

export function DashboardPage() {
  const dashboardId = useAuthStore((state) => state.actorId)!;
  const accountKey = useAuthStore((state) => state.accountKey);
  const lastEvent = useLastRealtimeEvent();
  const queryClient = useQueryClient();
  const fallback = useMemo(() => createDefaultDashboard(dashboardId), [dashboardId]);
  const dashboardQueryKey = useMemo(() => queryKeys.dashboards.detail(dashboardId), [dashboardId]);
  const sessionIdentity = `${dashboardId}\u0000${accountKey}`;
  const [draft, setDraft] = useState<DashboardSpecification>(fallback);
  const [selectedWidgetId, setSelectedWidgetId] = useState<string | undefined>(fallback.widgets[0]?.id);
  const [conflict, setConflict] = useState<{ serverState?: DashboardSpecification; attempted: DashboardSpecification }>();
  const [isEditing, setIsEditing] = useState(false);
  const dashboard = useQuery({
    queryKey: dashboardQueryKey,
    queryFn: async ({ signal }) => {
      try { return await getDashboard(dashboardId, signal); }
      catch (error) { if (error instanceof ApiError && error.status === 404) return fallback; throw error; }
    },
  });

  useEffect(() => { if (dashboard.data) setDraft(dashboard.data); }, [dashboard.data]);
  const save = useMutation({
    mutationFn: saveDashboard,
    retry: false,
    onMutate: async (next) => {
      const mutationSessionIdentity = sessionIdentity;
      await queryClient.cancelQueries({ queryKey: dashboardQueryKey });
      const auth = useAuthStore.getState();
      if (`${auth.actorId}\u0000${auth.accountKey}` !== mutationSessionIdentity) {
        throw new Error('The authenticated session changed before the dashboard save started.');
      }
      const snapshot = queryClient.getQueryData<DashboardSpecification>(dashboardQueryKey);
      queryClient.setQueryData(dashboardQueryKey, next);
      return { snapshot, sessionIdentity: mutationSessionIdentity };
    },
    onSuccess: (saved, _attempted, context) => {
      const auth = useAuthStore.getState();
      if (context?.sessionIdentity !== `${auth.actorId}\u0000${auth.accountKey}`) return;
      queryClient.setQueryData(dashboardQueryKey, saved);
      setDraft(saved);
      setConflict(undefined);
    },
    onError: async (error, attempted, context) => {
      const auth = useAuthStore.getState();
      if (context?.sessionIdentity !== `${auth.actorId}\u0000${auth.accountKey}`) return;
      const current = conflictDashboard(error);
      if (error instanceof ApiError && error.status === 409) {
        if (current) {
          queryClient.setQueryData(dashboardQueryKey, current);
          setDraft(current);
        } else if (context?.snapshot === undefined) {
          queryClient.removeQueries({ queryKey: dashboardQueryKey, exact: true });
        } else {
          queryClient.setQueryData(dashboardQueryKey, context.snapshot);
        }
        setConflict({ serverState: current, attempted });
        await queryClient.invalidateQueries({ queryKey: dashboardQueryKey });
        return;
      }
      if (context?.snapshot === undefined) queryClient.removeQueries({ queryKey: dashboardQueryKey, exact: true });
      else queryClient.setQueryData(dashboardQueryKey, context.snapshot);
    }
  });
  const selectedWidget = draft.widgets.find((widget) => widget.id === selectedWidgetId) ?? draft.widgets[0];
  const updateWidget = (updatedWidget: DashboardSpecification['widgets'][number]) => setDraft((current) => ({
    ...current,
    widgets: current.widgets.map((widget) => widget.id === updatedWidget.id ? updatedWidget : widget)
  }));

  const refreshSeconds = Math.round(draft.refreshRateMs / 1000);
  const dashboardPeriod = new Intl.DateTimeFormat('en', { month: 'short', year: 'numeric' }).format(new Date());
  const liveCopy = lastEvent
    ? `live ${lastEvent.aggregateType} #${lastEvent.sequenceId}`
    : `saved v${draft.version}`;

  return <section data-slot="dashboard-page" data-editing={isEditing || undefined}>
    <h1 className="sr-only">Dashboard</h1>
    <header data-slot="dashboard-header">
      <div>
        <h2>{draft.title}</h2>
        <p data-testid="dashboard-last-entity-change" title={lastEvent ? `${lastEvent.aggregateType} ${lastEvent.eventType} (#${lastEvent.sequenceId})` : 'Waiting for live entity changes'}>
          refresh {refreshSeconds}s · {liveCopy}
        </p>
      </div>
      <div data-slot="dashboard-actions">
        <span>{dashboardPeriod}</span>
        <Button intent="secondary" size="sm" type="button" onClick={() => setIsEditing((current) => !current)} aria-expanded={isEditing}>
          {isEditing ? 'Close editor' : 'Edit layout'}
        </Button>
      </div>
    </header>
    {isEditing ? <aside data-slot="dashboard-editor" aria-label="Dashboard settings">
      <div data-slot="dashboard-editor-toolbar">
        <label>Title<Input value={draft.title} onChange={(event) => setDraft((current) => ({ ...current, title: event.target.value }))} /></label>
        <div>
          <p>Theme</p>
          <Select
            label="Theme"
            options={['LIGHT', 'DARK', 'SYSTEM']}
            value={draft.theme}
            onValueChange={(value) => setDraft((current) => ({ ...current, theme: value as DashboardSpecification['theme'] }))}
          />
        </div>
        <label>Refresh (seconds)<NumberInput aria-label="Refresh seconds" min={5} value={refreshSeconds} onValueChange={(value) => setDraft((current) => ({ ...current, refreshRateMs: Math.max(5, Number(value) || 5) * 1000 }))} /></label>
        {selectedWidget ? <div data-slot="dashboard-control"><span>Widget</span><Select label="Widget" options={draft.widgets.map((widget) => ({ label: widget.title, value: widget.id }))} value={selectedWidget.id} onValueChange={setSelectedWidgetId} /></div> : null}
        <Button type="button" onClick={() => save.mutate(draft)} disabled={save.isPending}>{save.isPending ? 'Saving…' : 'Save dashboard'}</Button>
      </div>
      {selectedWidget
        ? <QueryBindingConfigurator
            widget={selectedWidget}
            semanticMembers={getSemanticValueMembers(selectedWidget.queryAst.modelName)}
            onChange={updateWidget}
          />
        : <div data-testid="dashboard-empty-state" role="status"><h3>No widgets configured</h3><p>This dashboard is empty. You can still edit its settings and save it.</p></div>}
    </aside> : null}
    {!selectedWidget && !isEditing ? <div data-slot="dashboard-empty" data-testid="dashboard-empty-state" role="status"><h3>No widgets configured</h3><p>Use Edit layout to configure and save this dashboard.</p></div> : null}
    {dashboard.isError && <p role="alert">Unable to load the persisted dashboard. Showing the default workspace.</p>}
    {save.isError && !conflict && <p role="alert" className="error-banner">Unable to save the dashboard.</p>}
    <DashboardGrid dashboard={draft} onChange={setDraft} editable={isEditing} />
    {conflict && <div className="modal"><ConflictDialog entityId={dashboardId} serverState={conflict.serverState} attemptedChanges={conflict.attempted} onClose={() => setConflict(undefined)} /></div>}
  </section>;
}
