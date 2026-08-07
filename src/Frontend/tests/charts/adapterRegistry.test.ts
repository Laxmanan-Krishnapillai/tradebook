import { describe, expect, it } from 'vitest';
import { chartAdapterRegistry } from '../../src/lib/charts/adapterRegistry';
import type { ChartAdapter } from '../../src/types/visualizations';
const adapter = (): ChartAdapter => ({ mount: () => {}, update: () => {}, resize: () => {}, setTheme: () => {}, destroy: () => {} });
describe('chart adapter registry', () => {
  it('creates registered adapters and rejects unknown keys', () => { chartAdapterRegistry.register('LINE', adapter); expect(chartAdapterRegistry.create('LINE')).toBeDefined(); expect(() => chartAdapterRegistry.create('UNKNOWN')).toThrow("Unknown chart adapter key 'UNKNOWN'."); });
});
