import { flexRender, getCoreRowModel, getFilteredRowModel, useReactTable, type ColumnDef, type ColumnFiltersState, type Row } from '@tanstack/react-table';
import { useActorRef } from '@xstate/react';
import { memo, useCallback, useRef, useState, type RefObject } from 'react';
import { Checkbox } from '../ui/checkbox';
import { ColumnFilter } from '../ui/column-filter';
import { GridInteractionActorContext, gridInteractionMachine, type GridInteractionActor } from './gridInteractionMachine';

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

function isInteractiveTarget(target: EventTarget | null) {
  return target instanceof Element && Boolean(target.closest('a,button,input,select,textarea,[contenteditable="true"],[data-grid-interactive="true"],[role="button"],[role="checkbox"],[role="combobox"],[role="listbox"],[role="menuitem"],[role="option"]'));
}

function hasActiveTextSelection() {
  const selection = window.getSelection();
  return Boolean(selection && !selection.isCollapsed && selection.toString().length > 0);
}

interface PointerIntent {
  moved: boolean;
  rowId: string;
  x: number;
  y: number;
}

interface DataGridRowProps<T extends object> {
  canOpen: boolean;
  gridTemplateColumns: string;
  interactionActor: GridInteractionActor;
  pointerIntent: RefObject<PointerIntent | null>;
  row: Row<T>;
  rowHeight: number;
  selectable: boolean;
  selected: boolean;
  toggleRow: (rowId: string) => void;
}

function DataGridRow<T extends object>({
  canOpen,
  gridTemplateColumns,
  interactionActor,
  pointerIntent,
  row,
  rowHeight,
  selectable,
  selected,
  toggleRow,
}: DataGridRowProps<T>) {
  const interactive = selectable || canOpen;
  return (
    <tr
      aria-selected={selectable ? selected : undefined}
      data-optimistic={row.id.startsWith('optimistic-') ? 'true' : undefined}
      data-row-id={row.id}
      data-state={selected ? 'selected' : undefined}
      onClick={(event) => {
        const intent = pointerIntent.current;
        pointerIntent.current = null;
        if (event.defaultPrevented || isInteractiveTarget(event.target) || intent?.moved || hasActiveTextSelection()) return;
        interactionActor.send({ type: 'row.open', row: row.original });
      }}
      onKeyDown={(event) => {
        if (isInteractiveTarget(event.target)) return;
        if (event.key === 'Enter' && canOpen) {
          event.preventDefault();
          interactionActor.send({ type: 'row.open', row: row.original });
        }
        if ((event.key === ' ' || event.key.toLowerCase() === 'x') && selectable) {
          event.preventDefault();
          toggleRow(row.id);
        }
      }}
      onMouseDown={(event) => {
        if (event.button !== 0 || isInteractiveTarget(event.target)) {
          pointerIntent.current = null;
          return;
        }
        pointerIntent.current = { moved: false, rowId: row.id, x: event.clientX, y: event.clientY };
      }}
      onMouseMove={(event) => {
        const intent = pointerIntent.current;
        if (!intent || intent.rowId !== row.id || intent.moved) return;
        if (Math.hypot(event.clientX - intent.x, event.clientY - intent.y) > 4) intent.moved = true;
      }}
      style={{ display: 'grid', gridTemplateColumns, height: rowHeight, width: '100%' }}
      tabIndex={interactive ? 0 : undefined}
    >
      {selectable && <td data-slot="selection-column"><Checkbox aria-label={`Select record ${row.id}`} checked={selected} onCheckedChange={() => toggleRow(row.id)} /></td>}
      {row.getVisibleCells().map((cell) => <td key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</td>)}
    </tr>
  );
}

const MemoizedDataGridRow = memo(DataGridRow, (previous, next) => (
  previous.row.id === next.row.id
  && previous.row.index === next.row.index
  && previous.row.original === next.row.original
  && previous.selected === next.selected
  && previous.selectable === next.selectable
  && previous.canOpen === next.canOpen
  && previous.rowHeight === next.rowHeight
  && previous.gridTemplateColumns === next.gridTemplateColumns
)) as typeof DataGridRow;

export function VirtualizedDataTable<T extends object>({
  data,
  columns,
  rowHeight,
  height = 480,
  getRowId,
  testId,
  ariaLabel = 'Data grid',
  onRowOpen,
  selectedRowIds = new Set<string>(),
  onSelectedRowIdsChange,
}: VirtualizedDataTableProps<T>) {
  const resolvedRowHeight = rowHeight ?? 42;
  const [columnFilters, setColumnFilters] = useState<ColumnFiltersState>([]);
  const onRowOpenRef = useRef(onRowOpen);
  onRowOpenRef.current = onRowOpen;
  const interactionActor = useActorRef(gridInteractionMachine.provide({
    actions: {
      openRow: ({ event }) => {
        if (event.type === 'row.open') onRowOpenRef.current?.(event.row as T);
      },
    },
  }));
  const table = useReactTable({
    data,
    columns,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getRowId,
    onColumnFiltersChange: setColumnFilters,
    state: { columnFilters },
  });
  const [scrollTop, setScrollTop] = useState(0);
  const pointerIntent = useRef<PointerIntent | null>(null);
  const allRows = table.getRowModel().rows;
  const start = Math.max(0, Math.floor(scrollTop / resolvedRowHeight) - 3);
  const visible = allRows.slice(start, start + Math.ceil(height / resolvedRowHeight) + 6);
  const selectable = Boolean(onSelectedRowIdsChange);
  const columnCount = Math.max(1, table.getVisibleLeafColumns().length + (selectable ? 1 : 0));
  const gridTemplateColumns = selectable
    ? `3rem repeat(${table.getVisibleLeafColumns().length}, minmax(0, calc((var(--workspace-inline-size) - 3rem) / ${table.getVisibleLeafColumns().length})))`
    : `repeat(${table.getVisibleLeafColumns().length}, minmax(0, calc(var(--workspace-inline-size) / ${table.getVisibleLeafColumns().length})))`;
  const topHeight = start * resolvedRowHeight;
  const bottomHeight = Math.max(0, (allRows.length - start - visible.length) * resolvedRowHeight);
  const allSelected = allRows.length > 0 && allRows.every((row) => selectedRowIds.has(row.id));
  const someSelected = allRows.some((row) => selectedRowIds.has(row.id));
  const selectedRowIdsRef = useRef(selectedRowIds);
  const onSelectedRowIdsChangeRef = useRef(onSelectedRowIdsChange);
  selectedRowIdsRef.current = selectedRowIds;
  onSelectedRowIdsChangeRef.current = onSelectedRowIdsChange;

  const toggleRow = useCallback((rowId: string) => {
    const onChange = onSelectedRowIdsChangeRef.current;
    if (!onChange) return;
    const next = new Set(selectedRowIdsRef.current);
    if (next.has(rowId)) next.delete(rowId);
    else next.add(rowId);
    onChange(next);
  }, []);

  const toggleAll = () => {
    if (!onSelectedRowIdsChange) return;
    const next = new Set(selectedRowIds);
    if (allSelected) allRows.forEach((row) => next.delete(row.id));
    else allRows.forEach((row) => next.add(row.id));
    onSelectedRowIdsChange(next);
  };

  return (
    <GridInteractionActorContext.Provider value={interactionActor}>
    <div data-slot="data-grid-viewport" data-testid={testId} style={{ height, overflow: 'auto' }} onScroll={(event) => setScrollTop(event.currentTarget.scrollTop)} role="region" aria-label={ariaLabel}>
      <table data-slot="record-data-grid" style={{ display: 'block', maxWidth: '100%', overflow: 'hidden', width: '100%' }}>
        <thead style={{ display: 'block', width: '100%' }}>{table.getHeaderGroups().map((group) => <tr key={group.id} style={{ display: 'grid', gridTemplateColumns, width: '100%' }}>{selectable && <th data-slot="selection-column" scope="col"><Checkbox aria-label="Select all records" checked={allSelected} indeterminate={someSelected && !allSelected} onCheckedChange={toggleAll} /></th>}{group.headers.map((header) => <th scope="col" key={header.id}><span data-slot="column-heading">{header.isPlaceholder ? null : flexRender(header.column.columnDef.header, header.getContext())}</span>{header.column.getCanFilter() && <ColumnFilter label={String(header.column.columnDef.header ?? header.id)} value={String(header.column.getFilterValue() ?? '')} onChange={(value) => header.column.setFilterValue(value || undefined)} />}</th>)}</tr>)}</thead>
        <tbody style={{ display: 'block', width: '100%' }}>
          {topHeight > 0 && <tr aria-hidden="true" style={{ gridTemplateColumns }}><td colSpan={columnCount} style={{ gridColumn: '1 / -1', height: topHeight, padding: 0 }} /></tr>}
          {visible.map((row) => <MemoizedDataGridRow
            canOpen={Boolean(onRowOpen)}
            gridTemplateColumns={gridTemplateColumns}
            interactionActor={interactionActor}
            key={row.id}
            pointerIntent={pointerIntent}
            row={row}
            rowHeight={resolvedRowHeight}
            selectable={selectable}
            selected={selectedRowIds.has(row.id)}
            toggleRow={toggleRow}
          />)}
          {bottomHeight > 0 && <tr aria-hidden="true" style={{ gridTemplateColumns }}><td colSpan={columnCount} style={{ gridColumn: '1 / -1', height: bottomHeight, padding: 0 }} /></tr>}
        </tbody>
      </table>
    </div>
    </GridInteractionActorContext.Provider>
  );
}
