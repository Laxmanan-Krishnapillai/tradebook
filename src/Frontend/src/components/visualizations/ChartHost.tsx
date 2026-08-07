import { useQuery } from '@tanstack/react-query';
import { useChartAdapter } from '../../hooks/useChartAdapter';
import { apiFetch } from '../../lib/api/client';
import type { ChartWidgetConfig, ThemeTokens } from '../../types/visualizations';
import { toSeriesData, type AnalyticsResult } from './VisualEncodingMapper';
const defaultTheme: ThemeTokens = { background: '#fff', textPrimary: '#111827', textSecondary: '#6b7280', gridLine: '#e5e7eb', axisLine: '#9ca3af', seriesPalette: ['#2563eb', '#059669', '#d97706'], positive: '#059669', negative: '#dc2626', fontFamily: 'system-ui' };
export function ChartHost({ widget, theme = defaultTheme }: { widget: ChartWidgetConfig; theme?: ThemeTokens }) { const query = useQuery({ queryKey: ['analytics', widget.id, widget.queryAst], queryFn: () => apiFetch<AnalyticsResult>('/api/v1/analytics/query', { method: 'POST', body: JSON.stringify(widget.queryAst) }) }); const data = query.data ? toSeriesData(query.data, widget.visualEncodings) : undefined; const ref = useChartAdapter(widget.chartType, { chartType: widget.chartType, encodings: widget.visualEncodings, style: widget.styleOverrides }, data, theme); if (query.isError) return <p role="alert">Unable to load {widget.title}.</p>; return <section aria-label={widget.title}><h2>{widget.title}</h2><div ref={ref} style={{ minHeight: 200 }} /></section>; }
