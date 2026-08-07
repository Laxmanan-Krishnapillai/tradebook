import { describe, expect, it } from 'vitest';
import { LttbWorkerPool, LTTB_THRESHOLD } from '../../src/lib/workers/workerPool';

class FakeWorker {
  request?: { requestId: string; x: Float64Array; y: Float64Array; seriesCount: number; threshold: number };
  private message?: (event: MessageEvent) => void;
  addEventListener(type: string, listener: EventListener) { if (type === 'message') this.message = listener as (event: MessageEvent) => void; }
  removeEventListener() { }
  postMessage(message: { requestId: string; x: Float64Array; y: Float64Array; seriesCount: number; threshold: number }) {
    this.request = message;
    const selectedIndices = Array.from({ length: LTTB_THRESHOLD }, (_, index) => index === LTTB_THRESHOLD - 1 ? message.x.length - 1 : index);
    const sampledX = Float64Array.from(selectedIndices, (index) => message.x[index]);
    const sampledY = new Float64Array(LTTB_THRESHOLD * message.seriesCount);
    for (let seriesIndex = 0; seriesIndex < message.seriesCount; seriesIndex++) {
      for (let outputIndex = 0; outputIndex < selectedIndices.length; outputIndex++) {
        sampledY[(seriesIndex * LTTB_THRESHOLD) + outputIndex] =
          message.y[(seriesIndex * message.x.length) + selectedIndices[outputIndex]];
      }
    }
    this.message?.({ data: { requestId: message.requestId, x: sampledX, y: sampledY, seriesCount: message.seriesCount } } as MessageEvent);
  }
}

describe('shared LTTB worker pool', () => {
  it('preserves category labels and transfers a clone instead of detaching caller data', async () => {
    const worker = new FakeWorker();
    const pool = new LttbWorkerPool(() => worker as unknown as Worker);
    const original = Float64Array.from({ length: LTTB_THRESHOLD + 1 }, (_, index) => index);
    const labels = Array.from({ length: LTTB_THRESHOLD + 1 }, (_, index) => `day-${index}`);
    const result = await pool.downsample({ series: [{ name: 'value', x: labels, y: original }] });
    expect(result.series[0].x[0]).toBe('day-0');
    expect(result.series[0].x.at(-1)).toBe(`day-${LTTB_THRESHOLD}`);
    expect(original.byteLength).toBeGreaterThan(0);
    expect(worker.request?.y).not.toBe(original);
  });

  it('uses one common sample index for every aligned series', async () => {
    const worker = new FakeWorker();
    const pool = new LttbWorkerPool(() => worker as unknown as Worker);
    const labels = Array.from({ length: LTTB_THRESHOLD + 1 }, (_, index) => `day-${index}`);
    const first = Float64Array.from({ length: labels.length }, (_, index) => index * 2);
    const second = Float64Array.from({ length: labels.length }, (_, index) => (index * 10) + 7);

    const result = await pool.downsample({
      series: [
        { name: 'first', x: labels, y: first },
        { name: 'second', x: labels, y: second }
      ]
    });

    expect(worker.request?.seriesCount).toBe(2);
    expect(worker.request?.y).toHaveLength(labels.length * 2);
    expect(result.series[0].x).toBe(result.series[1].x);
    expect(result.series[0].x.at(-1)).toBe(`day-${LTTB_THRESHOLD}`);
    expect(result.series[0].y.at(-1)).toBe(LTTB_THRESHOLD * 2);
    expect(result.series[1].y.at(-1)).toBe((LTTB_THRESHOLD * 10) + 7);
  });

  it('rejects misaligned series before dispatching a worker request', async () => {
    const worker = new FakeWorker();
    const pool = new LttbWorkerPool(() => worker as unknown as Worker);
    const x = Array.from({ length: LTTB_THRESHOLD + 1 }, (_, index) => index);

    await expect(pool.downsample({
      series: [
        { name: 'first', x, y: x },
        { name: 'second', x: x.map((value) => value + 1), y: x }
      ]
    })).rejects.toThrow('aligned x values');
    expect(worker.request).toBeUndefined();
  });
});
