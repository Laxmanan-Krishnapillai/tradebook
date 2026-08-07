import type { SeriesData } from '../../types/visualizations';
const THRESHOLD = 5000;
export interface DownsampleWorkerPool { downsample(data: SeriesData): Promise<SeriesData>; }
interface WorkerResponse { requestId: string; x: Float64Array; y: Float64Array; }
class SharedLttbWorkerPool implements DownsampleWorkerPool {
  private readonly workers: Worker[];
  private nextWorker = 0;
  constructor() { const count = Math.max(1, (navigator.hardwareConcurrency || 2) - 1); this.workers = Array.from({ length: count }, () => new Worker(new URL('../../workers/lttbDownsample.worker.ts', import.meta.url), { type: 'module' })); }
  private downsampleSeries(series: SeriesData['series'][number]): Promise<SeriesData['series'][number]> {
    if (series.y.length <= THRESHOLD) return Promise.resolve(series);
    const worker = this.workers[this.nextWorker++ % this.workers.length]; const requestId = crypto.randomUUID(); const x = new Float64Array(series.x.map((value, index) => typeof value === 'number' ? value : index)); const y = series.y instanceof Float64Array ? series.y : new Float64Array(series.y);
    return new Promise((resolve, reject) => { const onMessage = ({ data }: MessageEvent<WorkerResponse>) => { if (data.requestId !== requestId) return; worker.removeEventListener('message', onMessage); resolve({ ...series, x: Array.from(data.x), y: data.y }); }; const onError = () => { worker.removeEventListener('message', onMessage); reject(new Error('LTTB worker failed.')); }; worker.addEventListener('message', onMessage); worker.addEventListener('error', onError, { once: true }); worker.postMessage({ requestId, x, y, threshold: THRESHOLD }, [x.buffer, y.buffer]); });
  }
  async downsample(data: SeriesData): Promise<SeriesData> { return { ...data, series: await Promise.all(data.series.map((series) => this.downsampleSeries(series))) }; }
}
export const sharedLttbWorkerPool: DownsampleWorkerPool = new SharedLttbWorkerPool();
export { THRESHOLD as LTTB_THRESHOLD };
