export interface LttbRequest { requestId: string; x: Float64Array; y: Float64Array; threshold: number; }
export function lttbDownsample(x: Float64Array, y: Float64Array, threshold: number): { x: Float64Array; y: Float64Array } {
  const n = x.length;
  if (y.length !== n) throw new Error('LTTB x and y arrays must have the same length.');
  if (threshold >= n || threshold < 3) return { x, y };
  const outX = new Float64Array(threshold); const outY = new Float64Array(threshold); const every = (n - 2) / (threshold - 2);
  let a = 0; outX[0] = x[0]; outY[0] = y[0];
  for (let i = 0; i < threshold - 2; i++) {
    const avgStart = Math.floor((i + 1) * every) + 1; const avgEnd = Math.min(Math.floor((i + 2) * every) + 1, n);
    let avgX = 0; let avgY = 0; const avgLength = Math.max(1, avgEnd - avgStart);
    for (let j = avgStart; j < avgEnd; j++) { avgX += x[j]; avgY += y[j]; } avgX /= avgLength; avgY /= avgLength;
    const start = Math.floor(i * every) + 1; const end = Math.min(Math.floor((i + 1) * every) + 1, n - 1);
    let maxArea = -1; let nextA = start;
    for (let j = start; j < end; j++) { const area = Math.abs((x[a] - avgX) * (y[j] - y[a]) - (x[a] - x[j]) * (avgY - y[a])) * 0.5; if (area > maxArea) { maxArea = area; nextA = j; } }
    outX[i + 1] = x[nextA]; outY[i + 1] = y[nextA]; a = nextA;
  }
  outX[threshold - 1] = x[n - 1]; outY[threshold - 1] = y[n - 1]; return { x: outX, y: outY };
}
const workerScope = self as unknown as { postMessage(message: unknown, transfer: Transferable[]): void; onmessage: ((event: MessageEvent<LttbRequest>) => void) | null };
if (typeof workerScope.postMessage === 'function') workerScope.onmessage = ({ data }: MessageEvent<LttbRequest>) => { const result = lttbDownsample(data.x, data.y, data.threshold); workerScope.postMessage({ requestId: data.requestId, ...result }, [result.x.buffer, result.y.buffer]); };
