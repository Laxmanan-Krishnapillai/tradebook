import type { ColumnDef } from '@tanstack/react-table';
import { useEffect, useState } from 'react';
import { VirtualizedDataTable as BaseVirtualizedDataTable } from '../../lib/grid/VirtualizedDataTable';
import { usePreferences, type Density } from '../../stores/preferences';

const rowHeights: Record<Density, number> = { condensed: 34, regular: 42, relaxed: 48 };

export interface VirtualizedDataTableProps<T extends object> {
  data: T[];
  columns: ColumnDef<T>[];
  rowHeight?: number;
  height?: number;
  getRowId?: (row: T) => string;
  testId?: string;
  ariaLabel?: string;
  onRowOpen?: (row: T) => void;
  selectedRowIds?: ReadonlySet<string>;
  onSelectedRowIdsChange?: (selectedRowIds: Set<string>) => void;
}

function useAvailableGridHeight() {
  const measure = () => Math.max(320, window.innerHeight - 164);
  const [height, setHeight] = useState(() => typeof window === 'undefined' ? 480 : measure());
  useEffect(() => {
    const update = () => setHeight(measure());
    window.addEventListener('resize', update);
    return () => window.removeEventListener('resize', update);
  }, []);
  return height;
}

export function VirtualizedDataTable<T extends object>(props: VirtualizedDataTableProps<T>) {
  const density = usePreferences((state) => state.density);
  const availableHeight = useAvailableGridHeight();
  return <BaseVirtualizedDataTable {...props} height={props.height ?? availableHeight} rowHeight={props.rowHeight ?? rowHeights[density]} />;
}
