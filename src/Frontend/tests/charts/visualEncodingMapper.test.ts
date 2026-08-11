import { describe, expect, it } from 'vitest';
import { toEChartsOption, toSeriesData } from '../../src/lib/charts/visualEncodingMapper';

describe('visual encoding mapper', () => {
  it('builds epoch-ms OHLC data in the declared open/high/low/close order', () => {
    const result = toSeriesData({ columns: ['date', 'open', 'high', 'low', 'close'], rows: [['2026-01-02', 10, 13, 8, 12]] }, { xAxis: 'date', yAxis: ['open', 'high', 'low', 'close'] }, 'CANDLESTICK');
    expect(result.ohlc).toEqual([{ time: Date.parse('2026-01-02'), open: 10, high: 13, low: 8, close: 12 }]);
    expect(result.series[0]).toMatchObject({ name: 'close', y: [12] });
  });

  it('rejects incomplete candlestick bindings', () => {
    expect(() => toSeriesData({ columns: ['date', 'close'], rows: [] }, { xAxis: 'date', yAxis: ['close'] }, 'CANDLESTICK')).toThrow('open, high, low, close');
  });

  it('maps multiple result columns onto one shared row-aligned x domain', () => {
    const result = toSeriesData(
      { columns: ['date', 'volume', 'price'], rows: [['2026-01-01', 10, 20], ['2026-01-02', 11, 21]] },
      { xAxis: 'date', yAxis: ['volume', 'price'] },
      'LINE'
    );

    expect(result.series[0].x).toBe(result.series[1].x);
    expect(result.series[0].y).toEqual([10, 11]);
    expect(result.series[1].y).toEqual([20, 21]);
  });

  it('builds a real ECharts heatmap with coordinate triplets and categorical axes', () => {
    const data = toSeriesData(
      { columns: ['month', 'volume', 'price'], rows: [['Jan', 10, 20], ['Feb', 11, 21]] },
      { xAxis: 'month', yAxis: ['volume', 'price'] },
      'HEATMAP'
    );
    const option = toEChartsOption(
      { chartType: 'HEATMAP', encodings: { xAxis: 'month', yAxis: ['volume', 'price'] }, style: { strokeWidth: 2, opacity: 0.4 } },
      data,
      null
    ) as Record<string, unknown>;
    const xAxis = option.xAxis as { type: string; data: string[] };
    const yAxis = option.yAxis as { type: string; data: string[] };
    const series = (option.series as Array<Record<string, unknown>>)[0];

    expect(xAxis).toMatchObject({ type: 'category', data: ['Jan', 'Feb'] });
    expect(yAxis).toMatchObject({ type: 'category', data: ['volume', 'price'] });
    expect(series).toMatchObject({
      type: 'heatmap',
      data: [[0, 0, 10], [1, 0, 11], [0, 1, 20], [1, 1, 21]],
      itemStyle: { borderWidth: 2, opacity: 0.4 }
    });
    expect(option.visualMap).toMatchObject({ min: 10, max: 21 });
  });

  it('applies stroke width and opacity to ordinary ECharts series', () => {
    const option = toEChartsOption(
      { chartType: 'LINE', encodings: { xAxis: 'date', yAxis: ['volume'] }, style: { strokeWidth: 3, opacity: 0.25 } },
      { series: [{ name: 'volume', x: ['Jan'], y: [10] }] },
      null
    ) as { series: Array<{ lineStyle: { width: number; opacity: number }; itemStyle: { opacity: number } }> };

    expect(option.series[0].lineStyle).toMatchObject({ width: 3, opacity: 0.25 });
    expect(option.series[0].itemStyle).toMatchObject({ opacity: 0.25 });
  });

  it('renders dense categorical rankings as readable horizontal bars', () => {
    const categories = Array.from({ length: 12 }, (_, index) => `CTR-${index + 1}`);
    const option = toEChartsOption(
      { chartType: 'BAR', encodings: { xAxis: 'contract', yAxis: ['volume_mwh'] } },
      { series: [{ name: 'volume_mwh', x: categories, y: categories.map((_, index) => 1200 - index * 50) }] },
      null
    ) as { aria: { enabled: boolean }; xAxis: { type: string; show: boolean }; yAxis: { type: string; inverse: boolean }; series: Array<{ name: string; universalTransition: boolean }> };

    expect(option.xAxis).toMatchObject({ type: 'value', show: false });
    expect(option.yAxis).toMatchObject({ type: 'category', inverse: true });
    expect(option.series[0]).toMatchObject({ name: 'Volume', universalTransition: true });
    expect(option.aria.enabled).toBe(true);
  });

  it('formats temporal trend charts with compact axes and smooth update motion', () => {
    const option = toEChartsOption(
      { chartType: 'AREA', encodings: { xAxis: 'month', yAxis: ['revenue_eur'] }, style: { opacity: 0.14 } },
      { series: [{ name: 'revenue_eur', x: ['2026-07-01', '2026-08-01'], y: [2_600_000, 2_800_000] }] },
      null
    ) as { animationDurationUpdate: number; xAxis: { data: string[]; boundaryGap: boolean }; series: Array<{ name: string; smooth: number; showSymbol: boolean; universalTransition: boolean }> };

    expect(option.xAxis).toMatchObject({ data: ['Jul 26', 'Aug 26'], boundaryGap: false });
    expect(option.series[0]).toMatchObject({ name: 'Revenue', smooth: 0.28, showSymbol: false, universalTransition: true });
    expect(option.animationDurationUpdate).toBe(260);
  });
});
