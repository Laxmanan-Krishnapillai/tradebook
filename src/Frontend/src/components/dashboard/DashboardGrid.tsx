import { Responsive, WidthProvider, type Layout } from 'react-grid-layout';
import { useEffect, useState } from 'react';
import type { ComponentProps, ComponentType } from 'react';
import type { DashboardSpecification, ThemeTokens } from '../../types/visualizations';
import { ChartHost } from '../visualizations/ChartHost';

const ResponsiveGrid = WidthProvider(Responsive);
const DashboardResponsiveGrid = ResponsiveGrid as ComponentType<ComponentProps<typeof ResponsiveGrid> & {
  containerPadding?: [number, number];
  isDraggable?: boolean;
  isResizable?: boolean;
  margin?: [number, number];
}>;
const light: ThemeTokens = { background: '#ffffff', textPrimary: '#29272d', textSecondary: '#7b7880', gridLine: '#efedf0', axisLine: '#d8d5db', seriesPalette: ['#6c63c8', '#8c84d6', '#b2ade3'], positive: '#3f9b70', negative: '#d26052', fontFamily: 'Instrument Sans Variable, system-ui' };
const dark: ThemeTokens = { background: '#141416', textPrimary: '#f0eef1', textSecondary: '#96929b', gridLine: '#29282d', axisLine: '#3d3a42', seriesPalette: ['#9188df', '#68c695', '#dfa84f'], positive: '#68c695', negative: '#ed7569', fontFamily: 'Instrument Sans Variable, system-ui' };

function dashboardTheme(theme: DashboardSpecification['theme'], systemDark: boolean): ThemeTokens {
  const darkMode = theme === 'DARK' || (theme === 'SYSTEM' && systemDark);
  return darkMode ? dark : light;
}

function useSystemDarkMode(enabled: boolean): boolean {
  const resolveDarkMode = () => typeof window !== 'undefined'
    && (document.documentElement.classList.contains('dark') || window.matchMedia?.('(prefers-color-scheme: dark)').matches === true);
  const [matches, setMatches] = useState(resolveDarkMode);
  useEffect(() => {
    if (!enabled || typeof window === 'undefined' || !window.matchMedia) return;
    const media = window.matchMedia('(prefers-color-scheme: dark)');
    const update = () => setMatches(resolveDarkMode());
    const rootObserver = new MutationObserver(update);
    update();
    media.addEventListener('change', update);
    rootObserver.observe(document.documentElement, { attributeFilter: ['class'], attributes: true });
    return () => {
      media.removeEventListener('change', update);
      rootObserver.disconnect();
    };
  }, [enabled]);
  return matches;
}

export function DashboardGrid({ dashboard, onChange, editable = false }: { dashboard: DashboardSpecification; onChange: (dashboard: DashboardSpecification) => void; editable?: boolean }) {
  const systemDark = useSystemDarkMode(dashboard.theme === 'SYSTEM');
  const layout: Layout[] = dashboard.gridLayout.items.map((item) => ({ i: item.widgetId, x: item.x, y: item.y, w: item.w, h: item.h, minW: item.minW, minH: item.minH, static: item.static }));
  const updateLayout = (next: Layout[]) => {
    const currentById = new Map(dashboard.gridLayout.items.map((item) => [item.widgetId, item]));
    const items = next.map((item) => ({ ...currentById.get(item.i), widgetId: item.i, x: item.x, y: item.y, w: item.w, h: item.h }));
    if (JSON.stringify(items) !== JSON.stringify(dashboard.gridLayout.items)) onChange({ ...dashboard, gridLayout: { ...dashboard.gridLayout, items } });
  };
  const theme = dashboardTheme(dashboard.theme, systemDark);
  return <DashboardResponsiveGrid
    data-testid="dashboard-grid"
    className="grid"
    layouts={{ lg: layout }}
    breakpoints={{ lg: 1200, md: 996, sm: 768 }}
    cols={{ lg: dashboard.gridLayout.columns, md: dashboard.gridLayout.columns, sm: dashboard.gridLayout.columns }}
    rowHeight={dashboard.gridLayout.rowHeight}
    margin={[0, 0]}
    containerPadding={[0, 0]}
    isDraggable={editable}
    isResizable={editable}
    onLayoutChange={updateLayout}
  >
    {dashboard.widgets.map((widget) => <div key={widget.id} data-widget-type={widget.chartType} data-editable={editable || undefined}><ChartHost widget={widget} theme={theme} refreshRateMs={dashboard.refreshRateMs} /></div>)}
  </DashboardResponsiveGrid>;
}
