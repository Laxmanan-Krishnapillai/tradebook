import { useQuery } from '@tanstack/react-query';
import { useCallback, useMemo, useState } from 'react';
import { useChartAdapter } from '../../hooks/useChartAdapter';
import { apiFetch } from '../../lib/api/client';
import { toSeriesData, type AnalyticsResult } from '../../lib/charts/visualEncodingMapper';
import { queryKeys } from '../../lib/query/queryKeys';
import type { ChartWidgetConfig, SeriesData, ThemeTokens } from '../../types/visualizations';

const defaultTheme: ThemeTokens = { background: '#fff', textPrimary: '#111827', textSecondary: '#6b7280', gridLine: '#e5e7eb', axisLine: '#9ca3af', seriesPalette: ['#2563eb', '#059669', '#d97706'], positive: '#059669', negative: '#dc2626', fontFamily: 'system-ui' };

export const MIN_CHART_REFRESH_INTERVAL_MS = 5_000;

export function resolveChartRefreshInterval(refreshRateMs: number | undefined): number | false {
  if (refreshRateMs === undefined || !Number.isFinite(refreshRateMs) || refreshRateMs <= 0) return false;
  return Math.max(MIN_CHART_REFRESH_INTERVAL_MS, Math.floor(refreshRateMs));
}

interface ChartHostProps {
  widget: ChartWidgetConfig;
  theme?: ThemeTokens;
  refreshRateMs?: number;
}

function metricUnit(member: string | undefined): string | undefined {
  if (member?.endsWith('_eur')) return 'EUR';
  if (member?.endsWith('_mwh')) return 'MWh';
  return undefined;
}

export function ChartHost({ widget, theme = defaultTheme, refreshRateMs }: ChartHostProps) {
  const [renderReceipt, setRenderReceipt] = useState<{
    source?: SeriesData;
    sequence: number;
    completedAtMs?: number;
  }>({ sequence: 0 });
  const query = useQuery({
    queryKey: queryKeys.analytics.result(widget.id, widget.queryAst),
    queryFn: ({ signal }) => apiFetch<AnalyticsResult>('/api/v1/analytics/query', {
      method: 'POST',
      body: JSON.stringify(widget.queryAst),
      signal,
    }),
    refetchInterval: resolveChartRefreshInterval(refreshRateMs)
  });
  const mapped = useMemo(() => {
    if (!query.data) return {};
    try { return { data: toSeriesData(query.data, widget.visualEncodings, widget.chartType) }; }
    catch (error) { return { mappingError: error instanceof Error ? error.message : 'Invalid visualization binding.' }; }
  }, [query.data, widget.chartType, widget.visualEncodings]);
  const spec = useMemo(() => ({ chartType: widget.chartType, encodings: widget.visualEncodings, style: widget.styleOverrides }), [widget.chartType, widget.visualEncodings, widget.styleOverrides]);
  const primaryUnit = metricUnit(widget.visualEncodings.yAxis[0]);
  const pointCount = mapped.data?.series[0]?.x.length;
  const chartContext = widget.queryAst.timeDimensions?.length
    ? pointCount === undefined ? primaryUnit : [primaryUnit, `${pointCount} periods`].filter(Boolean).join(' · ')
    : widget.queryAst.dimensions?.length && pointCount !== undefined
      ? [primaryUnit, `Top ${pointCount}`].filter(Boolean).join(' · ')
      : primaryUnit;
  const errorMessage = query.isError ? `Unable to load ${widget.title}.` : mapped.mappingError;
  const markRenderReady = useCallback((source: SeriesData) => {
    setRenderReceipt((current) => ({
      source,
      sequence: current.sequence + 1,
      completedAtMs: performance.now()
    }));
  }, []);
  const ref = useChartAdapter(widget.chartType, spec, mapped.data, theme, {
    enabled: errorMessage === undefined,
    onRenderReady: markRenderReady
  });
  if (errorMessage) return <section data-slot="dashboard-widget" data-chart-type={widget.chartType} aria-label={widget.title}>
    <header data-slot="dashboard-widget-header"><h2>{widget.title}</h2></header>
    <p data-slot="dashboard-widget-error" role="alert">{errorMessage}</p>
  </section>;
  const renderState = !mapped.data
    ? 'loading'
    : renderReceipt.source === mapped.data ? 'ready' : 'rendering';
  return <section data-slot="dashboard-widget" data-chart-type={widget.chartType} aria-label={widget.title}>
    <header data-slot="dashboard-widget-header">
      <h2>{widget.title}</h2>
      {widget.chartType !== 'KPI_CARD' && chartContext ? <p>{chartContext}</p> : null}
    </header>
    <div data-slot="dashboard-chart-frame">
      <div
        ref={ref}
        data-slot="dashboard-chart-canvas"
        style={{ minHeight: widget.chartType === 'KPI_CARD' ? 44 : 200 }}
        aria-busy={renderState !== 'ready'}
        data-chart-widget-id={widget.id}
        data-chart-render-state={renderState}
        data-chart-render-sequence={renderReceipt.sequence}
        data-chart-render-completed-at-ms={renderState === 'ready' ? renderReceipt.completedAtMs : undefined}
      />
      {renderState === 'loading' ? <div data-slot="dashboard-chart-skeleton" role="status" aria-label={`Loading ${widget.title}`}><span /></div> : null}
    </div>
  </section>;
}
