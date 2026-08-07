import { useEffect, useRef } from 'react';
import { chartAdapterRegistry } from '../lib/charts/adapterRegistry';
import { sharedLttbWorkerPool } from '../lib/workers/workerPool';
import type { ChartSpec, SeriesData, ThemeTokens } from '../types/visualizations';
export function useChartAdapter(type: string, spec: ChartSpec, data: SeriesData | undefined, theme: ThemeTokens): React.RefObject<HTMLDivElement | null> {
  const elRef = useRef<HTMLDivElement>(null); const adapterRef = useRef<ReturnType<typeof chartAdapterRegistry.create> | null>(null);
  useEffect(() => { if (!elRef.current) return; const adapter = chartAdapterRegistry.create(type); adapter.mount(elRef.current, spec); adapter.setTheme(theme); adapterRef.current = adapter; const observer = new ResizeObserver(() => adapter.resize()); observer.observe(elRef.current); return () => { observer.disconnect(); adapter.destroy(); adapterRef.current = null; }; }, [type, spec, theme]);
  useEffect(() => { let active = true; if (data && adapterRef.current) void sharedLttbWorkerPool.downsample(data).then((downsampled) => { if (active) adapterRef.current?.update(downsampled); }); return () => { active = false; }; }, [data]);
  return elRef;
}
