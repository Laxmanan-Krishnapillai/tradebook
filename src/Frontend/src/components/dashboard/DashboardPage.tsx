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
import { QueryBindingConfigurator } from '../visualizations/QueryBindingConfigurator';
import { DashboardGrid } from './DashboardGrid';
import 'react-grid-layout/css/styles.css';
import 'react-resizable/css/styles.css';

function defaultDashboard(dashboardId: string): DashboardSpecification {
  return {
    dashboardId,
    title: 'Delivery performance',
    description: 'Revenue and volume from the canonical delivery P&L semantic model.',
    version: 0,
    theme: 'LIGHT',
    refreshRateMs: 60_000,
    gridLayout: {
      columns: 12,
      rowHeight: 56,
      items: [
        { widgetId: 'monthly-revenue', x: 0, y: 0, w: 7, h: 6, minW: 4, minH: 4 },
        { widgetId: 'delivery-volume', x: 7, y: 0, w: 5, h: 6, minW: 4, minH: 4 }
      ]
    },
    widgets: [
      {
        id: 'monthly-revenue',
        title: 'Monthly revenue',
        chartType: 'AREA',
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
  const accessToken = useAuthStore((state) => state.accessToken);
  const lastEvent = useLastRealtimeEvent();
  const queryClient = useQueryClient();
  const fallback = useMemo(() => defaultDashboard(dashboardId), [dashboardId]);
  const dashboardQueryKey = useMemo(() => queryKeys.dashboards.detail(dashboardId), [dashboardId]);
  const sessionIdentity = `${dashboardId}\u0000${accessToken}`;
  const [draft, setDraft] = useState<DashboardSpecification>(fallback);
  const [selectedWidgetId, setSelectedWidgetId] = useState<string | undefined>(fallback.widgets[0]?.id);
  const [conflict, setConflict] = useState<{ serverState?: DashboardSpecification; attempted: DashboardSpecification }>();
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
      if (`${auth.actorId}\u0000${auth.accessToken}` !== mutationSessionIdentity) {
        throw new Error('The authenticated session changed before the dashboard save started.');
      }
      const snapshot = queryClient.getQueryData<DashboardSpecification>(dashboardQueryKey);
      queryClient.setQueryData(dashboardQueryKey, next);
      return { snapshot, sessionIdentity: mutationSessionIdentity };
    },
    onSuccess: (saved, _attempted, context) => {
      const auth = useAuthStore.getState();
      if (context?.sessionIdentity !== `${auth.actorId}\u0000${auth.accessToken}`) return;
      queryClient.setQueryData(dashboardQueryKey, saved);
      setDraft(saved);
      setConflict(undefined);
    },
    onError: async (error, attempted, context) => {
      const auth = useAuthStore.getState();
      if (context?.sessionIdentity !== `${auth.actorId}\u0000${auth.accessToken}`) return;
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

  return <section>
    <header className="page-header">
      <div><p className="eyebrow">Analytics</p><h2>{draft.title}</h2><p>{draft.description}</p></div>
      <button type="button" onClick={() => save.mutate(draft)} disabled={save.isPending}>{save.isPending ? 'Saving…' : 'Save dashboard'}</button>
    </header>
    <div className="toolbar" aria-label="Dashboard settings">
      <label>Title<input value={draft.title} onChange={(event) => setDraft((current) => ({ ...current, title: event.target.value }))} /></label>
      <label>Theme<select value={draft.theme} onChange={(event) => setDraft((current) => ({ ...current, theme: event.target.value as DashboardSpecification['theme'] }))}><option>LIGHT</option><option>DARK</option><option>SYSTEM</option></select></label>
      <label>Refresh (seconds)<input type="number" min="5" value={Math.round(draft.refreshRateMs / 1000)} onChange={(event) => setDraft((current) => ({ ...current, refreshRateMs: Math.max(5, Number(event.target.value) || 5) * 1000 }))} /></label>
      {selectedWidget ? <label>Widget<select value={selectedWidget.id} onChange={(event) => setSelectedWidgetId(event.target.value)}>{draft.widgets.map((widget) => <option key={widget.id} value={widget.id}>{widget.title}</option>)}</select></label> : null}
    </div>
    {selectedWidget
      ? <QueryBindingConfigurator
          widget={selectedWidget}
          semanticMembers={getSemanticValueMembers(selectedWidget.queryAst.modelName)}
          onChange={updateWidget}
        />
      : <div data-testid="dashboard-empty-state" role="status"><h3>No widgets configured</h3><p>This dashboard is empty. You can still edit its settings and save it.</p></div>}
    {dashboard.isError && <p role="alert">Unable to load the persisted dashboard. Showing the default workspace.</p>}
    {save.isError && !conflict && <p role="alert" className="error-banner">Unable to save the dashboard.</p>}
    <p data-testid="dashboard-last-entity-change" className="live-status">{lastEvent ? `Live: ${lastEvent.aggregateType} ${lastEvent.eventType} (#${lastEvent.sequenceId})` : 'Waiting for live entity changes…'}</p>
    <DashboardGrid dashboard={draft} onChange={setDraft} />
    {conflict && <div className="modal"><ConflictDialog entityId={dashboardId} serverState={conflict.serverState} attemptedChanges={conflict.attempted} onClose={() => setConflict(undefined)} /></div>}
  </section>;
}
