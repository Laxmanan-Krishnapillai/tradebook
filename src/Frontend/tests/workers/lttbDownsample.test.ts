import { describe, expect, it } from 'vitest';
import { lttbDownsample } from '../../src/workers/lttbDownsample.worker';
describe('LTTB downsampling', () => {
  it('retains endpoints, output length, and input order', () => { const x = Float64Array.from({ length: 6001 }, (_, index) => index); const y = Float64Array.from({ length: 6001 }, (_, index) => Math.sin(index / 100)); const result = lttbDownsample(x, y, 5000); expect(result.x).toHaveLength(5000); expect(result.x[0]).toBe(0); expect(result.x.at(-1)).toBe(6000); for (let index = 1; index < result.x.length; index++) expect(result.x[index]).toBeGreaterThan(result.x[index - 1]); });
});
