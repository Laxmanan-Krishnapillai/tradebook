import { Responsive, WidthProvider, type Layout } from 'react-grid-layout';
import type { DashboardSpecification } from '../../types/visualizations';
import { ChartHost } from '../visualizations/ChartHost';
const ResponsiveGrid = WidthProvider(Responsive);
export function DashboardGrid({ dashboard }: { dashboard: DashboardSpecification }) { const layout: Layout[] = dashboard.gridLayout.items.map((item) => ({ i: item.widgetId, x: item.x, y: item.y, w: item.w, h: item.h, minW: item.minW, minH: item.minH, static: item.static })); return <ResponsiveGrid className="dashboard-grid" layouts={{ lg: layout }} breakpoints={{ lg: 1200, md: 996, sm: 768 }} cols={{ lg: 12, md: 12, sm: 12 }} rowHeight={dashboard.gridLayout.rowHeight}>{dashboard.widgets.map((widget) => <div key={widget.id}><ChartHost widget={widget} /></div>)}</ResponsiveGrid>; }
