import { Responsive, WidthProvider, type Layout } from 'react-grid-layout';
import { useEffect, useState } from 'react';
import type { DashboardSpecification, ThemeTokens } from '../../types/visualizations';
import { ChartHost } from '../visualizations/ChartHost';

const ResponsiveGrid = WidthProvider(Responsive);
const light: ThemeTokens = { background: '#ffffff', textPrimary: '#111827', textSecondary: '#6b7280', gridLine: '#e5e7eb', axisLine: '#9ca3af', seriesPalette: ['#2563eb', '#059669', '#d97706'], positive: '#059669', negative: '#dc2626', fontFamily: 'system-ui' };
const dark: ThemeTokens = { background: '#111827', textPrimary: '#f9fafb', textSecondary: '#9ca3af', gridLine: '#374151', axisLine: '#6b7280', seriesPalette: ['#60a5fa', '#34d399', '#fbbf24'], positive: '#34d399', negative: '#f87171', fontFamily: 'system-ui' };

function dashboardTheme(theme: DashboardSpecification['theme'], systemDark: boolean): ThemeTokens {
  const darkMode = theme === 'DARK' || (theme === 'SYSTEM' && systemDark);
  return darkMode ? dark : light;
}

function useSystemDarkMode(enabled: boolean): boolean {
  const [matches, setMatches] = useState(() => typeof window !== 'undefined' && window.matchMedia?.('(prefers-color-scheme: dark)').matches === true);
  useEffect(() => {
    if (!enabled || typeof window === 'undefined' || !window.matchMedia) return;
    const media = window.matchMedia('(prefers-color-scheme: dark)');
    const update = (event: MediaQueryListEvent) => setMatches(event.matches);
    setMatches(media.matches);
    media.addEventListener('change', update);
    return () => media.removeEventListener('change', update);
  }, [enabled]);
  return matches;
}

export function DashboardGrid({ dashboard, onChange }: { dashboard: DashboardSpecification; onChange: (dashboard: DashboardSpecification) => void }) {
  const systemDark = useSystemDarkMode(dashboard.theme === 'SYSTEM');
  const layout: Layout[] = dashboard.gridLayout.items.map((item) => ({ i: item.widgetId, x: item.x, y: item.y, w: item.w, h: item.h, minW: item.minW, minH: item.minH, static: item.static }));
  const updateLayout = (next: Layout[]) => {
    const currentById = new Map(dashboard.gridLayout.items.map((item) => [item.widgetId, item]));
    const items = next.map((item) => ({ ...currentById.get(item.i), widgetId: item.i, x: item.x, y: item.y, w: item.w, h: item.h }));
    if (JSON.stringify(items) !== JSON.stringify(dashboard.gridLayout.items)) onChange({ ...dashboard, gridLayout: { ...dashboard.gridLayout, items } });
  };
  const theme = dashboardTheme(dashboard.theme, systemDark);
  return <ResponsiveGrid
    data-testid="dashboard-grid"
    className="grid"
    layouts={{ lg: layout }}
    breakpoints={{ lg: 1200, md: 996, sm: 768 }}
    cols={{ lg: dashboard.gridLayout.columns, md: dashboard.gridLayout.columns, sm: dashboard.gridLayout.columns }}
    rowHeight={dashboard.gridLayout.rowHeight}
    onLayoutChange={updateLayout}
  >
    {dashboard.widgets.map((widget) => <div key={widget.id}><ChartHost widget={widget} theme={theme} refreshRateMs={dashboard.refreshRateMs} /></div>)}
  </ResponsiveGrid>;
}
