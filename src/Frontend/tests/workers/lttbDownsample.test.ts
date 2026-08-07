import { describe, expect, it } from 'vitest';
import { lttbDownsample, lttbDownsampleAligned } from '../../src/workers/lttbDownsample.worker';
describe('LTTB downsampling', () => {
  it('retains endpoints, output length, and input order', () => { const x = Float64Array.from({ length: 6001 }, (_, index) => index); const y = Float64Array.from({ length: 6001 }, (_, index) => Math.sin(index / 100)); const result = lttbDownsample(x, y, 5000); expect(result.x).toHaveLength(5000); expect(result.x[0]).toBe(0); expect(result.x.at(-1)).toBe(6000); for (let index = 1; index < result.x.length; index++) expect(result.x[index]).toBeGreaterThan(result.x[index - 1]); });

  it('returns every series on the same selected x coordinates', () => {
    const pointCount = 6001;
    const threshold = 5000;
    const x = Float64Array.from({ length: pointCount }, (_, index) => index);
    const y = new Float64Array(pointCount * 2);
    for (let index = 0; index < pointCount; index++) {
      y[index] = index * 2;
      y[pointCount + index] = (index * 10) + 7;
    }

    const result = lttbDownsampleAligned(x, y, 2, threshold);

    expect(result.x).toHaveLength(threshold);
    expect(result.y).toHaveLength(threshold * 2);
    for (let outputIndex = 0; outputIndex < result.x.length; outputIndex++) {
      const sourceIndex = result.x[outputIndex];
      expect(result.y[outputIndex]).toBe(sourceIndex * 2);
      expect(result.y[threshold + outputIndex]).toBe((sourceIndex * 10) + 7);
    }
  });
});
