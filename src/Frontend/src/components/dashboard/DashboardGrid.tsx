import { Responsive, WidthProvider, type Layout } from 'react-grid-layout';
import { useEffect, useLayoutEffect, useRef, useState } from 'react';
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

function resolveColor(scope: HTMLElement, variable: string): string {
  const view = scope.ownerDocument.defaultView;
  if (!view) return '';
  const probe = scope.ownerDocument.createElement('span');
  probe.style.color = `var(${variable})`;
  probe.style.display = 'none';
  scope.appendChild(probe);
  const resolved = view.getComputedStyle(probe).color;
  probe.remove();
  return resolved && !resolved.startsWith('var(')
    ? resolved
    : view.getComputedStyle(scope).getPropertyValue(variable).trim();
}

export function resolveDashboardThemeTokens(scope: HTMLElement): ThemeTokens {
  const styles = scope.ownerDocument.defaultView?.getComputedStyle(scope);
  return {
    background: resolveColor(scope, '--surface-raised'),
    textPrimary: resolveColor(scope, '--foreground'),
    textSecondary: resolveColor(scope, '--muted-foreground'),
    gridLine: resolveColor(scope, '--border'),
    axisLine: resolveColor(scope, '--border-strong'),
    seriesPalette: [resolveColor(scope, '--accent-500'), resolveColor(scope, '--buy-500'), resolveColor(scope, '--warn-500')],
    positive: resolveColor(scope, '--buy-500'),
    negative: resolveColor(scope, '--sell-500'),
    fontFamily: styles?.fontFamily || 'system-ui',
  };
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
  const themeScope = useRef<HTMLDivElement>(null);
  const [theme, setTheme] = useState<ThemeTokens>();
  const layout: Layout[] = dashboard.gridLayout.items.map((item) => ({ i: item.widgetId, x: item.x, y: item.y, w: item.w, h: item.h, minW: item.minW, minH: item.minH, static: item.static }));
  const updateLayout = (next: Layout[]) => {
    const currentById = new Map(dashboard.gridLayout.items.map((item) => [item.widgetId, item]));
    const items = next.map((item) => ({ ...currentById.get(item.i), widgetId: item.i, x: item.x, y: item.y, w: item.w, h: item.h }));
    if (JSON.stringify(items) !== JSON.stringify(dashboard.gridLayout.items)) onChange({ ...dashboard, gridLayout: { ...dashboard.gridLayout, items } });
  };
  const themeClass = dashboard.theme === 'SYSTEM' ? undefined : dashboard.theme.toLowerCase();
  useLayoutEffect(() => {
    if (themeScope.current) setTheme(resolveDashboardThemeTokens(themeScope.current));
  }, [dashboard.theme, systemDark]);
  return <div ref={themeScope} className={themeClass} data-slot="dashboard-theme-scope" data-dashboard-theme={dashboard.theme.toLowerCase()}>
    <DashboardResponsiveGrid
      data-testid="dashboard-grid"
      className="dashboard-grid"
      layouts={{ lg: layout }}
      breakpoints={{ lg: 1200, md: 996, sm: 768 }}
      cols={{ lg: dashboard.gridLayout.columns, md: dashboard.gridLayout.columns, sm: dashboard.gridLayout.columns }}
      rowHeight={dashboard.gridLayout.rowHeight}
      margin={[12, 12]}
      containerPadding={[12, 12]}
      isDraggable={editable}
      isResizable={editable}
      onLayoutChange={updateLayout}
    >
      {dashboard.widgets.map((widget) => <div key={widget.id} data-widget-type={widget.chartType} data-editable={editable || undefined}><ChartHost widget={widget} theme={theme} refreshRateMs={dashboard.refreshRateMs} /></div>)}
    </DashboardResponsiveGrid>
  </div>;
}
