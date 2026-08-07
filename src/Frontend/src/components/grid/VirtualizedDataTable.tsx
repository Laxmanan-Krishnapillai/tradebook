import { flexRender, getCoreRowModel, useReactTable, type ColumnDef } from '@tanstack/react-table';
export function VirtualizedDataTable<T extends object>({ data, columns, rowHeight = 36, height = 360 }: { data: T[]; columns: ColumnDef<T>[]; rowHeight?: number; height?: number }) {
  const table = useReactTable({ data, columns, getCoreRowModel: getCoreRowModel() });
  return <div style={{ height, overflow: 'auto' }} role="region" aria-label="Data grid"><table><thead>{table.getHeaderGroups().map((group) => <tr key={group.id}>{group.headers.map((header) => <th key={header.id}>{header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}</th>)}</tr>)}</thead><tbody>{table.getRowModel().rows.map((row) => <tr key={row.id} style={{ height: rowHeight }}>{row.getVisibleCells().map((cell) => <td key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</td>)}</tr>)}</tbody></table></div>;
}
