import type { ChartAdapter, ChartType } from '../../types/visualizations';
type ChartAdapterFactory = () => ChartAdapter;
class ChartAdapterRegistry {
  private readonly factories = new Map<ChartType, ChartAdapterFactory>();
  register(type: ChartType, factory: ChartAdapterFactory): void { this.factories.set(type, factory); }
  create(type: string): ChartAdapter { const factory = this.factories.get(type as ChartType); if (!factory) throw new Error(`Unknown chart adapter key '${type}'.`); return factory(); }
  has(type: string): boolean { return this.factories.has(type as ChartType); }
  registeredTypes(): ChartType[] { return [...this.factories.keys()]; }
}
export const chartAdapterRegistry = new ChartAdapterRegistry();
