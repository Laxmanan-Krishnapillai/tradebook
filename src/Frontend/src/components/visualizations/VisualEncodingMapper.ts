import type { EChartsOption } from 'echarts';
import type { ChartSpec, SeriesData, ThemeTokens, VisualEncodingSpec } from '../../types/visualizations';
export interface AnalyticsResult { columns: string[]; rows: unknown[][]; }
export function toSeriesData(result: AnalyticsResult, encodings: VisualEncodingSpec): SeriesData {
  const xIndex = result.columns.indexOf(encodings.xAxis); if (xIndex < 0) throw new Error(`Unknown x-axis column '${encodings.xAxis}'.`);
  return { series: encodings.yAxis.map((name) => { const yIndex = result.columns.indexOf(name); if (yIndex < 0) throw new Error(`Unknown y-axis column '${name}'.`); return { name, x: result.rows.map((row) => String(row[xIndex] ?? '')), y: result.rows.map((row) => Number(row[yIndex])) }; }) };
}
const chartKind: Record<string, string> = { AREA: 'line', SPARK_LINE: 'line', STACKED_BAR: 'bar' };
export function toEChartsOption(spec: ChartSpec, data: SeriesData, tokens: ThemeTokens | null): EChartsOption {
  const type = chartKind[spec.chartType] ?? spec.chartType.toLowerCase(); const first = data.series[0];
  return { backgroundColor: tokens?.background, textStyle: { color: tokens?.textPrimary, fontFamily: tokens?.fontFamily }, tooltip: { trigger: 'axis' }, legend: { show: spec.style?.showLegend ?? true }, xAxis: { type: 'category', data: first?.x, axisLine: { lineStyle: { color: tokens?.axisLine } } }, yAxis: { type: 'value', splitLine: { show: spec.style?.showGridlines ?? true, lineStyle: { color: tokens?.gridLine } } }, series: data.series.map((series, index) => ({ name: series.name, type, data: Array.from(series.y), areaStyle: spec.chartType === 'AREA' ? {} : undefined, stack: spec.chartType === 'STACKED_BAR' ? 'total' : undefined, itemStyle: { color: tokens?.seriesPalette[index] } })) as EChartsOption['series'] };
}
