declare module 'react-grid-layout' {
  import type { ComponentType, ReactNode } from 'react';
  export interface Layout { i: string; x: number; y: number; w: number; h: number; minW?: number; minH?: number; static?: boolean; }
  export interface ResponsiveProps {
    children?: ReactNode;
    className?: string;
    layouts: Record<string, Layout[]>;
    breakpoints: Record<string, number>;
    cols: Record<string, number>;
    rowHeight: number;
    onLayoutChange?: (layout: Layout[]) => void;
    'data-testid'?: string;
  }
  export const Responsive: ComponentType<ResponsiveProps>;
  export function WidthProvider(component: ComponentType<ResponsiveProps>): ComponentType<ResponsiveProps>;
}
