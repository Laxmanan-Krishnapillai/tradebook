import type { EChartsOption } from 'echarts';
import type { ChartSpec, ChartType, SeriesData, ThemeTokens, VisualEncodingSpec } from '../../types/visualizations';

export interface AnalyticsResult {
  columns: string[];
  rows: unknown[][];
}

function columnIndex(columns: string[], name: string): number {
  const index = columns.indexOf(name);
  if (index < 0) throw new Error(`Unknown result column '${name}'.`);
  return index;
}

function numeric(value: unknown, column: string): number {
  if (value === null || value === undefined || value === '') throw new Error(`Column '${column}' contains a missing numeric value.`);
  const result = Number(value);
  if (!Number.isFinite(result)) throw new Error(`Column '${column}' contains a non-numeric value.`);
  return result;
}

function timestamp(value: unknown, column: string): number {
  if (typeof value === 'number' && Number.isFinite(value)) return value;
  const result = Date.parse(String(value));
  if (!Number.isFinite(result)) throw new Error(`Column '${column}' contains an invalid timestamp.`);
  return result;
}

export function toSeriesData(result: AnalyticsResult, encodings: VisualEncodingSpec, chartType?: ChartType): SeriesData {
  const xIndex = columnIndex(result.columns, encodings.xAxis);
  if (chartType === 'CANDLESTICK') {
    if (encodings.yAxis.length !== 4) throw new Error('Candlestick widgets require yAxis columns in open, high, low, close order.');
    const [openName, highName, lowName, closeName] = encodings.yAxis;
    const [openIndex, highIndex, lowIndex, closeIndex] = encodings.yAxis.map((name) => columnIndex(result.columns, name));
    const ohlc = result.rows.map((row) => ({
      time: timestamp(row[xIndex], encodings.xAxis),
      open: numeric(row[openIndex], openName),
      high: numeric(row[highIndex], highName),
      low: numeric(row[lowIndex], lowName),
      close: numeric(row[closeIndex], closeName)
    }));
    return { series: [{ name: closeName, x: ohlc.map((row) => row.time), y: ohlc.map((row) => row.close) }], ohlc };
  }

  const x = result.rows.map((row) => {
    const value = row[xIndex];
    return typeof value === 'number' ? value : String(value ?? '');
  });
  return {
    series: encodings.yAxis.map((name) => {
      const yIndex = columnIndex(result.columns, name);
      return { name, x, y: result.rows.map((row) => numeric(row[yIndex], name)) };
    })
  };
}

const chartKind: Record<string, string> = { AREA: 'line', SPARK_LINE: 'line', STACKED_BAR: 'bar' };
const compactNumber = new Intl.NumberFormat('en', { maximumFractionDigits: 1, notation: 'compact' });
const monthLabel = new Intl.DateTimeFormat('en', { month: 'short', year: '2-digit', timeZone: 'UTC' });

function formatCompact(value: unknown): string {
  const numericValue = Number(value);
  return Number.isFinite(numericValue) ? compactNumber.format(numericValue) : String(value ?? '');
}

function isTemporalCategory(value: string | number): boolean {
  return typeof value === 'string' && /^\d{4}-\d{2}(?:-\d{2})?/.test(value) && Number.isFinite(Date.parse(value));
}

function formatCategory(value: string | number): string {
  return isTemporalCategory(value) ? monthLabel.format(new Date(value)) : String(value);
}

function readableSeriesName(name: string): string {
  const withoutUnit = name.replace(/_(eur|mwh|count)$/i, '');
  return withoutUnit.replaceAll('_', ' ').replace(/\b\w/g, (letter) => letter.toUpperCase());
}

const motion = {
  animation: true,
  animationDuration: 420,
  animationDurationUpdate: 260,
  animationEasing: 'cubicOut' as const,
  animationEasingUpdate: 'cubicOut' as const,
};

function axisLine(tokens: ThemeTokens | null) {
  return { lineStyle: { color: tokens?.axisLine } };
}

function splitLine(spec: ChartSpec, tokens: ThemeTokens | null) {
  return {
    show: spec.style?.showGridlines ?? true,
    lineStyle: { color: tokens?.gridLine }
  };
}

function heatmapOption(spec: ChartSpec, data: SeriesData, tokens: ThemeTokens | null): EChartsOption {
  const x = data.series[0]?.x ?? [];
  const values: Array<[number, number, number]> = [];
  let minimum = Number.POSITIVE_INFINITY;
  let maximum = Number.NEGATIVE_INFINITY;

  data.series.forEach((series, seriesIndex) => {
    if (series.x.length !== x.length || series.y.length !== x.length) {
      throw new Error('Heatmap series must share one aligned x domain.');
    }
    for (let xIndex = 0; xIndex < x.length; xIndex++) {
      if (series.x[xIndex] !== x[xIndex]) throw new Error('Heatmap series must share one aligned x domain.');
      const value = Number(series.y[xIndex]);
      if (!Number.isFinite(value)) throw new Error(`Heatmap series '${series.name}' contains a non-numeric value.`);
      values.push([xIndex, seriesIndex, value]);
      minimum = Math.min(minimum, value);
      maximum = Math.max(maximum, value);
    }
  });

  if (values.length === 0) {
    minimum = 0;
    maximum = 0;
  }
  return {
    ...motion,
    aria: { enabled: true, decal: { show: false } },
    backgroundColor: tokens?.background,
    textStyle: { color: tokens?.textPrimary, fontFamily: tokens?.fontFamily },
    tooltip: { trigger: 'item' },
    legend: { show: spec.style?.showLegend ?? false },
    grid: { containLabel: true, bottom: 64 },
    xAxis: { type: 'category', data: x, axisLine: axisLine(tokens), splitLine: splitLine(spec, tokens) },
    yAxis: {
      type: 'category',
      data: data.series.map((series) => series.name),
      axisLine: axisLine(tokens),
      splitLine: splitLine(spec, tokens)
    },
    visualMap: {
      min: minimum,
      max: maximum,
      calculable: true,
      orient: 'horizontal',
      left: 'center',
      bottom: 0,
      inRange: tokens?.seriesPalette.length ? { color: tokens.seriesPalette } : undefined
    },
    series: [{
      name: 'Heatmap',
      type: 'heatmap',
      data: values,
      itemStyle: {
        borderWidth: spec.style?.strokeWidth,
        borderColor: tokens?.background,
        opacity: spec.style?.opacity
      }
    }]
  };
}

export function toEChartsOption(spec: ChartSpec, data: SeriesData, tokens: ThemeTokens | null): EChartsOption {
  if (spec.chartType === 'HEATMAP') return heatmapOption(spec, data, tokens);

  const type = chartKind[spec.chartType] ?? spec.chartType.toLowerCase();
  const first = data.series[0];
  const opacity = spec.style?.opacity;
  const temporalCategory = first?.x.length ? first.x.every(isTemporalCategory) : false;
  const compactHorizontalBar = spec.chartType === 'BAR'
    && (first?.x.length ?? 0) <= 20
    && first?.x.every((value) => typeof value === 'string') === true;
  const rankedHorizontalBar = compactHorizontalBar && !temporalCategory;
  if (rankedHorizontalBar) {
    return {
      ...motion,
      aria: { enabled: true, decal: { show: false } },
      backgroundColor: tokens?.background,
      textStyle: { color: tokens?.textPrimary, fontFamily: tokens?.fontFamily },
      tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' }, valueFormatter: formatCompact },
      legend: { show: spec.style?.showLegend ?? false },
      grid: { containLabel: true, left: 8, right: 48, top: 8, bottom: 8 },
      xAxis: { type: 'value', show: false },
      yAxis: {
        type: 'category',
        data: first?.x,
        inverse: true,
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: tokens?.textSecondary, fontSize: 11, overflow: 'truncate', width: 104 }
      },
      series: data.series.map((series, index) => ({
        id: series.name,
        name: readableSeriesName(series.name),
        type: 'bar',
        data: Array.from(series.y),
        barMaxWidth: 7,
        showBackground: true,
        backgroundStyle: { color: tokens?.gridLine, borderRadius: 4 },
        itemStyle: { color: tokens?.seriesPalette[index], borderRadius: 4 },
        label: { show: true, position: 'right', color: tokens?.textPrimary, fontFamily: 'IBM Plex Mono', fontSize: 10, formatter: ({ value }) => formatCompact(value) },
        universalTransition: true,
      })) as EChartsOption['series']
    };
  }
  const quietVerticalBar = spec.chartType === 'BAR' && data.series.length === 1;
  const trendChart = spec.chartType === 'AREA' || spec.chartType === 'LINE' || spec.chartType === 'SPARK_LINE';
  const xValues = temporalCategory ? first?.x.map(formatCategory) : first?.x;
  return {
    ...motion,
    aria: { enabled: true, decal: { show: false } },
    backgroundColor: tokens?.background,
    textStyle: { color: tokens?.textPrimary, fontFamily: tokens?.fontFamily },
    tooltip: { trigger: 'axis', axisPointer: { type: trendChart ? 'line' : 'shadow' }, valueFormatter: formatCompact },
    legend: { show: spec.style?.showLegend ?? true },
    grid: quietVerticalBar || trendChart ? { containLabel: true, left: 2, right: 10, top: 16, bottom: 4 } : undefined,
    xAxis: quietVerticalBar
      ? { type: 'category', data: xValues, axisLine: axisLine(tokens), axisTick: { show: false }, axisLabel: { color: tokens?.textSecondary, fontSize: 10, hideOverlap: true } }
      : { type: 'category', data: xValues, boundaryGap: !trendChart, axisLine: axisLine(tokens), axisTick: { show: false }, axisLabel: { alignMaxLabel: 'right', alignMinLabel: 'left', color: tokens?.textSecondary, fontSize: 10, hideOverlap: true } },
    yAxis: quietVerticalBar
      ? { type: 'value', show: false, splitLine: splitLine(spec, tokens) }
      : { type: 'value', axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: tokens?.textSecondary, fontSize: 10, formatter: formatCompact }, splitLine: splitLine(spec, tokens), splitNumber: 4 },
    series: data.series.map((series, index) => ({
      id: series.name,
      name: readableSeriesName(series.name),
      type,
      data: quietVerticalBar
        ? Array.from(series.y, (value, valueIndex) => ({
            value,
            itemStyle: { color: tokens?.seriesPalette[0], opacity: valueIndex === series.y.length - 1 ? 1 : 0.32 }
          }))
        : Array.from(series.y),
      areaStyle: spec.chartType === 'AREA' ? { opacity: opacity ?? 0.14 } : undefined,
      stack: spec.chartType === 'STACKED_BAR' ? 'total' : undefined,
      lineStyle: { width: spec.style?.strokeWidth ?? (trendChart ? 2 : undefined), opacity: spec.chartType === 'AREA' ? 1 : opacity ?? 1 },
      itemStyle: { color: tokens?.seriesPalette[index], opacity, borderWidth: spec.style?.strokeWidth },
      showSymbol: trendChart ? false : undefined,
      smooth: trendChart ? 0.28 : undefined,
      universalTransition: true,
    })) as EChartsOption['series']
  };
}
