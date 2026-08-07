import { flexRender, getCoreRowModel, useReactTable, type ColumnDef } from '@tanstack/react-table';
import { useState } from 'react';

export interface VirtualizedDataTableProps<T extends object> {
  data: T[];
  columns: ColumnDef<T>[];
  rowHeight?: number;
  height?: number;
  getRowId?: (row: T) => string;
  testId?: string;
}

export function VirtualizedDataTable<T extends object>({ data, columns, rowHeight = 44, height = 480, getRowId, testId }: VirtualizedDataTableProps<T>) {
  const table = useReactTable({ data, columns, getCoreRowModel: getCoreRowModel(), getRowId });
  const [scrollTop, setScrollTop] = useState(0);
  const allRows = table.getRowModel().rows;
  const start = Math.max(0, Math.floor(scrollTop / rowHeight) - 3);
  const visible = allRows.slice(start, start + Math.ceil(height / rowHeight) + 6);
  const columnCount = Math.max(1, table.getVisibleLeafColumns().length);
  const topHeight = start * rowHeight;
  const bottomHeight = Math.max(0, (allRows.length - start - visible.length) * rowHeight);
  return <div data-testid={testId} style={{ height, overflow: 'auto' }} onScroll={(event) => setScrollTop(event.currentTarget.scrollTop)} role="region" aria-label="Data grid"><table><thead>{table.getHeaderGroups().map((group) => <tr key={group.id}>{group.headers.map((header) => <th key={header.id}>{header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}</th>)}</tr>)}</thead><tbody>{topHeight > 0 && <tr aria-hidden="true"><td colSpan={columnCount} style={{ height: topHeight, padding: 0 }} /></tr>}{visible.map((row) => <tr key={row.id} style={{ height: rowHeight }}>{row.getVisibleCells().map((cell) => <td key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</td>)}</tr>)}{bottomHeight > 0 && <tr aria-hidden="true"><td colSpan={columnCount} style={{ height: bottomHeight, padding: 0 }} /></tr>}</tbody></table></div>;
}
