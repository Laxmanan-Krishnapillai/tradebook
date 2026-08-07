export interface LttbRequest {
  requestId: string;
  x: Float64Array;
  /** Series-major values: [series0 points..., series1 points..., ...]. */
  y: Float64Array;
  seriesCount: number;
  threshold: number;
}

export interface LttbResult {
  x: Float64Array;
  /** Series-major values sampled at the common x indices. */
  y: Float64Array;
}

/**
 * LTTB selection for aligned series. Every series contributes a normalized
 * triangle area, then all series are sampled at the one winning source index.
 */
export function lttbDownsampleAligned(
  x: Float64Array,
  y: Float64Array,
  seriesCount: number,
  threshold: number
): LttbResult {
  const pointCount = x.length;
  if (!Number.isInteger(seriesCount) || seriesCount < 1) throw new Error('LTTB seriesCount must be a positive integer.');
  if (y.length !== pointCount * seriesCount) throw new Error('LTTB y data must contain one aligned value per x coordinate and series.');
  if (threshold >= pointCount || threshold < 3) return { x, y };

  const scales = new Float64Array(seriesCount);
  for (let seriesIndex = 0; seriesIndex < seriesCount; seriesIndex++) {
    const offset = seriesIndex * pointCount;
    let minimum = Number.POSITIVE_INFINITY;
    let maximum = Number.NEGATIVE_INFINITY;
    for (let pointIndex = 0; pointIndex < pointCount; pointIndex++) {
      const value = y[offset + pointIndex];
      if (!Number.isFinite(value)) throw new Error('LTTB y data must contain only finite numbers.');
      minimum = Math.min(minimum, value);
      maximum = Math.max(maximum, value);
    }
    scales[seriesIndex] = maximum > minimum ? maximum - minimum : 1;
  }

  const selectedIndices = new Uint32Array(threshold);
  const every = (pointCount - 2) / (threshold - 2);
  const averageY = new Float64Array(seriesCount);
  let selected = 0;
  selectedIndices[0] = selected;

  for (let bucketIndex = 0; bucketIndex < threshold - 2; bucketIndex++) {
    const averageStart = Math.floor((bucketIndex + 1) * every) + 1;
    const averageEnd = Math.min(Math.floor((bucketIndex + 2) * every) + 1, pointCount);
    const averageLength = Math.max(1, averageEnd - averageStart);
    let averageX = 0;
    for (let pointIndex = averageStart; pointIndex < averageEnd; pointIndex++) averageX += x[pointIndex];
    averageX /= averageLength;

    averageY.fill(0);
    for (let seriesIndex = 0; seriesIndex < seriesCount; seriesIndex++) {
      const offset = seriesIndex * pointCount;
      for (let pointIndex = averageStart; pointIndex < averageEnd; pointIndex++) averageY[seriesIndex] += y[offset + pointIndex];
      averageY[seriesIndex] /= averageLength;
    }

    const rangeStart = Math.floor(bucketIndex * every) + 1;
    const rangeEnd = Math.min(Math.floor((bucketIndex + 1) * every) + 1, pointCount - 1);
    let maximumArea = -1;
    let nextSelected = rangeStart;

    for (let candidate = rangeStart; candidate < rangeEnd; candidate++) {
      let combinedArea = 0;
      for (let seriesIndex = 0; seriesIndex < seriesCount; seriesIndex++) {
        const offset = seriesIndex * pointCount;
        const selectedY = y[offset + selected];
        const area = Math.abs(
          (x[selected] - averageX) * (y[offset + candidate] - selectedY) -
          (x[selected] - x[candidate]) * (averageY[seriesIndex] - selectedY)
        ) * 0.5;
        combinedArea += area / scales[seriesIndex];
      }
      if (combinedArea > maximumArea) {
        maximumArea = combinedArea;
        nextSelected = candidate;
      }
    }

    selected = nextSelected;
    selectedIndices[bucketIndex + 1] = selected;
  }
  selectedIndices[threshold - 1] = pointCount - 1;

  const sampledX = new Float64Array(threshold);
  const sampledY = new Float64Array(threshold * seriesCount);
  for (let outputIndex = 0; outputIndex < threshold; outputIndex++) {
    const sourceIndex = selectedIndices[outputIndex];
    sampledX[outputIndex] = x[sourceIndex];
    for (let seriesIndex = 0; seriesIndex < seriesCount; seriesIndex++) {
      sampledY[(seriesIndex * threshold) + outputIndex] = y[(seriesIndex * pointCount) + sourceIndex];
    }
  }
  return { x: sampledX, y: sampledY };
}

export function lttbDownsample(x: Float64Array, y: Float64Array, threshold: number): LttbResult {
  if (y.length !== x.length) throw new Error('LTTB x and y arrays must have the same length.');
  return lttbDownsampleAligned(x, y, 1, threshold);
}

const workerScope = self as unknown as {
  postMessage(message: unknown, transfer: Transferable[]): void;
  onmessage: ((event: MessageEvent<LttbRequest>) => void) | null;
};

if (typeof workerScope.postMessage === 'function') {
  workerScope.onmessage = ({ data }: MessageEvent<LttbRequest>) => {
    const result = lttbDownsampleAligned(data.x, data.y, data.seriesCount, data.threshold);
    workerScope.postMessage(
      { requestId: data.requestId, seriesCount: data.seriesCount, ...result },
      [result.x.buffer, result.y.buffer]
    );
  };
}
