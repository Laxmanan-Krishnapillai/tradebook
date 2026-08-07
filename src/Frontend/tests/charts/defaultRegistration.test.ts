import { describe, expect, it } from 'vitest';
import { chartAdapterRegistry } from '../../src/lib/charts/adapterRegistry';
import { registerDefaultAdapters } from '../../src/lib/charts/registerDefaultAdapters';

describe('default chart registration', () => {
  it('registers every allowed chart type idempotently', () => {
    registerDefaultAdapters(); registerDefaultAdapters();
    expect(chartAdapterRegistry.registeredTypes().sort()).toEqual(['AREA', 'BAR', 'CANDLESTICK', 'HEATMAP', 'KPI_CARD', 'LINE', 'SCATTER', 'SPARK_LINE', 'STACKED_BAR', 'TABLE'].sort());
  });
});
