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
  return {
    backgroundColor: tokens?.background,
    textStyle: { color: tokens?.textPrimary, fontFamily: tokens?.fontFamily },
    tooltip: { trigger: 'axis' },
    legend: { show: spec.style?.showLegend ?? true },
    xAxis: { type: 'category', data: first?.x, axisLine: axisLine(tokens) },
    yAxis: { type: 'value', splitLine: splitLine(spec, tokens) },
    series: data.series.map((series, index) => ({
      name: series.name,
      type,
      data: Array.from(series.y),
      areaStyle: spec.chartType === 'AREA' ? { opacity } : undefined,
      stack: spec.chartType === 'STACKED_BAR' ? 'total' : undefined,
      lineStyle: { width: spec.style?.strokeWidth, opacity },
      itemStyle: { color: tokens?.seriesPalette[index], opacity, borderWidth: spec.style?.strokeWidth }
    })) as EChartsOption['series']
  };
}
