import { useEffect, useRef } from 'react';
import { chartAdapterRegistry } from '../lib/charts/adapterRegistry';
import { sharedLttbWorkerPool, type DownsampleWorkerPool } from '../lib/workers/workerPool';
import type { ChartSpec, SeriesData, ThemeTokens } from '../types/visualizations';

export interface UseChartAdapterOptions {
  enabled?: boolean;
  workerPool?: DownsampleWorkerPool;
  onRenderStart?: (data: SeriesData) => void;
  onRenderReady?: (data: SeriesData) => void;
}

export function useChartAdapter(
  type: string,
  spec: ChartSpec,
  data: SeriesData | undefined,
  theme: ThemeTokens,
  {
    enabled = true,
    workerPool = sharedLttbWorkerPool,
    onRenderStart,
    onRenderReady
  }: UseChartAdapterOptions = {}
): React.RefObject<HTMLDivElement | null> {
  const elRef = useRef<HTMLDivElement>(null);
  const adapterRef = useRef<ReturnType<typeof chartAdapterRegistry.create> | null>(null);
  const specRef = useRef(spec);
  const themeRef = useRef(theme);
  const renderCallbacksRef = useRef({ onRenderStart, onRenderReady });
  specRef.current = spec;
  themeRef.current = theme;
  renderCallbacksRef.current = { onRenderStart, onRenderReady };
  const encodingsKey = `${spec.encodings.xAxis}\u0000${spec.encodings.yAxis.join('\u0000')}\u0000${spec.encodings.colorBy ?? ''}\u0000${spec.encodings.sizeBy ?? ''}\u0000${spec.encodings.tooltipFields?.join('\u0000') ?? ''}`;
  const styleKey = `${spec.style?.showLegend ?? ''}\u0000${spec.style?.showGridlines ?? ''}\u0000${spec.style?.strokeWidth ?? ''}\u0000${spec.style?.opacity ?? ''}`;
  const themeKey = `${theme.background}\u0000${theme.textPrimary}\u0000${theme.textSecondary}\u0000${theme.gridLine}\u0000${theme.axisLine}\u0000${theme.seriesPalette.join('\u0000')}\u0000${theme.positive}\u0000${theme.negative}\u0000${theme.fontFamily}`;

  useEffect(() => {
    if (!enabled || !elRef.current) return;
    const adapter = chartAdapterRegistry.create(type);
    adapter.mount(elRef.current, specRef.current);
    adapter.setTheme(themeRef.current);
    adapterRef.current = adapter;
    const observer = typeof ResizeObserver === 'undefined' ? undefined : new ResizeObserver(() => adapter.resize());
    observer?.observe(elRef.current);
    return () => { observer?.disconnect(); adapter.destroy(); adapterRef.current = null; };
  }, [enabled, type, encodingsKey, styleKey]);

  useEffect(() => { adapterRef.current?.setTheme(themeRef.current); }, [themeKey]);
  useEffect(() => {
    let active = true;
    if (enabled && data && adapterRef.current) {
      renderCallbacksRef.current.onRenderStart?.(data);
      void workerPool.downsample(data).then((downsampled) => {
        if (!active || !adapterRef.current) return;
        adapterRef.current.update(downsampled);
        renderCallbacksRef.current.onRenderReady?.(data);
      });
    }
    return () => { active = false; };
  }, [data, enabled, workerPool, type, encodingsKey, styleKey]);
  return elRef;
}
