import { describe, expect, it } from 'vitest';
import schemaDocument from '../../src/types/dashboardSchema.json';

describe('dashboard JSON schema', () => {
  it('declares every required root property and the exact chart registry keys', () => {
    const schema = schemaDocument as { required: string[]; properties: Record<string, unknown> & { widgets: { items: { properties: { chartType: { enum: string[] } }; allOf: Array<{ then: { properties: { visualEncodings: { properties: { yAxis: { minItems: number; maxItems: number } } } } } }> } } } };
    expect(schema.required.every((name) => Object.hasOwn(schema.properties, name))).toBe(true);
    expect(schema.properties.widgets.items.properties.chartType.enum).toEqual(['KPI_CARD', 'SPARK_LINE', 'LINE', 'AREA', 'BAR', 'STACKED_BAR', 'SCATTER', 'HEATMAP', 'CANDLESTICK', 'TABLE']);
    expect(schema.properties.widgets.items.allOf[0].then.properties.visualEncodings.properties.yAxis).toEqual({ minItems: 4, maxItems: 4 });
  });
});
