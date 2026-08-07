declare module 'react-grid-layout' {
  export interface Layout { i: string; x: number; y: number; w: number; h: number; minW?: number; minH?: number; static?: boolean; }
  export const Responsive: any;
  export function WidthProvider(component: any): any;
}
