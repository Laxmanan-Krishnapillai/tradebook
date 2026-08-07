import type { SeriesData } from '../../types/visualizations';

const THRESHOLD = 5000;

export interface DownsampleWorkerPool {
  downsample(data: SeriesData): Promise<SeriesData>;
}

interface WorkerResponse {
  requestId: string;
  x: Float64Array;
  y: Float64Array;
  seriesCount: number;
}

export class LttbWorkerPool implements DownsampleWorkerPool {
  private workers?: Worker[];
  private nextWorker = 0;

  constructor(
    private readonly createWorker: () => Worker = () => new Worker(
      new URL('../../workers/lttbDownsample.worker.ts', import.meta.url),
      { type: 'module' }
    )
  ) { }

  private getWorkers(): Worker[] {
    if (!this.workers) {
      const concurrency = globalThis.navigator?.hardwareConcurrency || 2;
      this.workers = Array.from({ length: Math.max(1, concurrency - 1) }, this.createWorker);
    }
    return this.workers;
  }

  async downsample(data: SeriesData): Promise<SeriesData> {
    const first = data.series[0];
    if (!first) return data;

    const pointCount = first.x.length;
    for (const series of data.series) {
      if (series.x.length !== pointCount || series.y.length !== pointCount) {
        throw new Error('LTTB requires aligned x and y lengths for every series.');
      }
      for (let pointIndex = 0; pointIndex < pointCount; pointIndex++) {
        if (series.x[pointIndex] !== first.x[pointIndex]) {
          throw new Error('LTTB requires aligned x values for every series.');
        }
      }
    }
    if (data.ohlc && data.ohlc.length !== pointCount) {
      throw new Error('LTTB requires OHLC rows to align with the chart x values.');
    }
    if (pointCount <= THRESHOLD) return data;

    const hasLabels = first.x.some((value) => typeof value !== 'number');
    const workerX = hasLabels
      ? Float64Array.from({ length: pointCount }, (_, index) => index)
      : Float64Array.from(first.x as number[]);
    for (const value of workerX) if (!Number.isFinite(value)) throw new Error('LTTB x values must be finite numbers or category labels.');

    const seriesCount = data.series.length;
    const workerY = new Float64Array(pointCount * seriesCount);
    for (let seriesIndex = 0; seriesIndex < seriesCount; seriesIndex++) {
      const values = Float64Array.from(data.series[seriesIndex].y);
      for (const value of values) if (!Number.isFinite(value)) throw new Error('LTTB y values must be finite numbers.');
      workerY.set(values, seriesIndex * pointCount);
    }

    const response = await this.dispatch(workerX, workerY, seriesCount);
    if (response.seriesCount !== seriesCount || response.x.length !== THRESHOLD || response.y.length !== THRESHOLD * seriesCount) {
      throw new Error('LTTB worker returned a malformed aligned-series response.');
    }

    const sourceIndices = hasLabels
      ? Array.from(response.x, (value) => {
          const index = Math.round(value);
          if (index !== value || index < 0 || index >= pointCount) throw new Error('LTTB worker returned an invalid category index.');
          return index;
        })
      : this.locateNumericSourceIndices(first.x as number[], response.x);
    const sampledX = sourceIndices.map((index) => first.x[index]);
    const series = data.series.map((item, seriesIndex) => ({
      ...item,
      x: sampledX,
      y: response.y.slice(seriesIndex * THRESHOLD, (seriesIndex + 1) * THRESHOLD)
    }));
    const ohlc = data.ohlc ? sourceIndices.map((index) => data.ohlc![index]) : undefined;
    return ohlc ? { ...data, series, ohlc } : { ...data, series };
  }

  private locateNumericSourceIndices(sourceX: number[], sampledX: Float64Array): number[] {
    const indices: number[] = [];
    let searchFrom = 0;
    for (const value of sampledX) {
      let found = -1;
      for (let sourceIndex = searchFrom; sourceIndex < sourceX.length; sourceIndex++) {
        if (sourceX[sourceIndex] === value) {
          found = sourceIndex;
          break;
        }
      }
      if (found < 0) throw new Error('LTTB worker returned an x value outside the source domain.');
      indices.push(found);
      searchFrom = found + 1;
    }
    return indices;
  }

  private dispatch(x: Float64Array, y: Float64Array, seriesCount: number): Promise<WorkerResponse> {
    const workers = this.getWorkers();
    const worker = workers[this.nextWorker++ % workers.length];
    const requestId = crypto.randomUUID();
    return new Promise((resolve, reject) => {
      const cleanup = () => {
        worker.removeEventListener('message', onMessage);
        worker.removeEventListener('error', onError);
      };
      const onMessage = ({ data }: MessageEvent<WorkerResponse>) => {
        if (data.requestId !== requestId) return;
        cleanup();
        resolve(data);
      };
      const onError = () => {
        cleanup();
        reject(new Error('LTTB worker failed.'));
      };
      worker.addEventListener('message', onMessage);
      worker.addEventListener('error', onError);
      worker.postMessage({ requestId, x, y, seriesCount, threshold: THRESHOLD }, [x.buffer, y.buffer]);
    });
  }
}

export const sharedLttbWorkerPool: DownsampleWorkerPool = new LttbWorkerPool();
export { THRESHOLD as LTTB_THRESHOLD };
