import { fireEvent, render, screen } from '@testing-library/react';
import type { ColumnDef } from '@tanstack/react-table';
import { describe, expect, it, vi } from 'vitest';
import { VirtualizedDataTable } from '../../src/lib/grid/VirtualizedDataTable';
import { TableEditableCell } from '../../src/components/ui/table-editable-cell';

interface RecordRow {
  id: string;
  name: string;
}

const rows: RecordRow[] = [
  { id: 'contract-1', name: 'BioGem supply' },
  { id: 'contract-2', name: 'Nordic transport' },
];
const columns: ColumnDef<RecordRow>[] = [{ accessorKey: 'name', header: 'Contract' }];

describe('VirtualizedDataTable record workspace behavior', () => {
  it('opens a record with pointer and keyboard activation', () => {
    const onOpen = vi.fn();
    render(<VirtualizedDataTable ariaLabel="Contracts" columns={columns} data={rows} getRowId={(row) => row.id} onRowOpen={onOpen} />);

    const firstRow = screen.getByText('BioGem supply').closest('tr');
    expect(firstRow).toBeTruthy();
    fireEvent.click(firstRow!);
    fireEvent.keyDown(firstRow!, { key: 'Enter' });
    expect(onOpen).toHaveBeenCalledTimes(2);
    expect(onOpen).toHaveBeenLastCalledWith(rows[0]);
  });

  it('supports individual and page selection without opening the record', () => {
    const onOpen = vi.fn();
    const onSelectionChange = vi.fn();
    const { rerender } = render(
      <VirtualizedDataTable columns={columns} data={rows} getRowId={(row) => row.id} onRowOpen={onOpen} onSelectedRowIdsChange={onSelectionChange} selectedRowIds={new Set()} />,
    );

    fireEvent.click(screen.getByRole('checkbox', { name: 'Select record contract-1' }));
    expect(onSelectionChange).toHaveBeenCalledWith(new Set(['contract-1']));
    expect(onOpen).not.toHaveBeenCalled();

    rerender(<VirtualizedDataTable columns={columns} data={rows} getRowId={(row) => row.id} onSelectedRowIdsChange={onSelectionChange} selectedRowIds={new Set(['contract-1'])} />);
    fireEvent.click(screen.getByRole('checkbox', { name: 'Select all records' }));
    expect(onSelectionChange).toHaveBeenLastCalledWith(new Set(['contract-1', 'contract-2']));
  });

  it('does not open a record while selecting or dragging text', () => {
    const onOpen = vi.fn();
    render(<VirtualizedDataTable columns={columns} data={rows} getRowId={(row) => row.id} onRowOpen={onOpen} />);

    const firstRow = screen.getByText('BioGem supply').closest('tr');
    expect(firstRow).toBeTruthy();

    const selection = vi.spyOn(window, 'getSelection').mockReturnValue({
      isCollapsed: false,
      toString: () => 'BioGem supply',
    } as Selection);
    fireEvent.click(firstRow!);
    expect(onOpen).not.toHaveBeenCalled();

    selection.mockReturnValue({ isCollapsed: true, toString: () => '' } as Selection);
    fireEvent.mouseDown(firstRow!, { button: 0, clientX: 12, clientY: 12 });
    fireEvent.mouseMove(firstRow!, { clientX: 26, clientY: 12 });
    fireEvent.click(firstRow!);
    expect(onOpen).not.toHaveBeenCalled();

    fireEvent.click(firstRow!);
    expect(onOpen).toHaveBeenCalledOnce();
  });

  it('keeps one inline editor active and does not leak edit gestures into row opening', async () => {
    const onOpen = vi.fn();
    const editableColumns: ColumnDef<RecordRow>[] = [
      {
        accessorKey: 'name',
        header: 'Contract',
        cell: ({ row }) => <TableEditableCell label={`Name ${row.original.id}`} onCommit={vi.fn()} value={row.original.name} />,
      },
    ];
    render(<VirtualizedDataTable columns={editableColumns} data={rows} getRowId={(row) => row.id} onRowOpen={onOpen} />);

    const firstEditor = screen.getByRole('textbox', { name: 'Name contract-1' });
    const secondEditor = screen.getByRole('textbox', { name: 'Name contract-2' });
    fireEvent.click(firstEditor);
    await vi.waitFor(() => expect(firstEditor.hasAttribute('readonly')).toBe(false));

    fireEvent.click(secondEditor);
    await vi.waitFor(() => expect(secondEditor.hasAttribute('readonly')).toBe(false));
    expect(firstEditor.hasAttribute('readonly')).toBe(true);
    expect(onOpen).not.toHaveBeenCalled();

    const secondRow = secondEditor.closest('tr');
    expect(secondRow).toBeTruthy();
    fireEvent.click(secondRow!);
    expect(secondEditor.hasAttribute('readonly')).toBe(true);
    expect(onOpen).not.toHaveBeenCalled();

    fireEvent.click(secondRow!);
    expect(onOpen).toHaveBeenCalledOnce();
    expect(onOpen).toHaveBeenCalledWith(rows[1]);
  });

  it('switches open selects in one click and consumes the outside row click that closes editing', async () => {
    const onOpen = vi.fn();
    const editableColumns: ColumnDef<RecordRow>[] = [
      {
        accessorKey: 'name',
        header: 'Product',
        cell: ({ row }) => (
          <TableEditableCell
            label={`Product ${row.original.id}`}
            onCommit={vi.fn()}
            options={['Gas', 'Power']}
            value="Gas"
          />
        ),
      },
    ];
    render(<VirtualizedDataTable columns={editableColumns} data={rows} getRowId={(row) => row.id} onRowOpen={onOpen} />);

    fireEvent.click(screen.getByRole('combobox', { name: 'Product contract-1' }));
    expect(await screen.findByRole('listbox')).toBeTruthy();

    const secondEdit = screen.getByRole('combobox', { name: 'Product contract-2' });
    fireEvent.pointerDown(secondEdit, { button: 0, pointerType: 'mouse' });
    fireEvent.click(secondEdit);
    expect(await screen.findByRole('combobox', { name: 'Product contract-2' })).toBeTruthy();
    expect(screen.getByRole('combobox', { name: 'Product contract-1' }).getAttribute('aria-expanded')).toBe('false');

    const secondRow = screen.getByRole('combobox', { name: 'Product contract-2' }).closest('tr');
    expect(secondRow).toBeTruthy();
    fireEvent.pointerDown(secondRow!, { button: 0, pointerType: 'mouse' });
    fireEvent.click(secondRow!);
    expect(onOpen).not.toHaveBeenCalled();
    await vi.waitFor(() => expect(secondEdit.getAttribute('aria-expanded')).toBe('false'));

    fireEvent.click(secondRow!);
    expect(onOpen).toHaveBeenCalledOnce();
    expect(onOpen).toHaveBeenCalledWith(rows[1]);
  });

  it('filters each column with the styled header filter', async () => {
    render(<VirtualizedDataTable ariaLabel="Contracts" columns={columns} data={rows} getRowId={(row) => row.id} />);

    fireEvent.click(screen.getByRole('button', { name: 'Filter Contract' }));
    const filter = await screen.findByRole('textbox', { name: 'Filter Contract values' });
    fireEvent.change(filter, { target: { value: 'Nordic' } });

    expect(screen.getByText('Nordic transport')).toBeTruthy();
    expect(screen.queryByText('BioGem supply')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Clear Contract filter' }));
    expect(screen.getByText('BioGem supply')).toBeTruthy();
  });

  it('rerenders only the row whose record identity changed', () => {
    const renderCount = new Map<string, number>();
    const countingColumns: ColumnDef<RecordRow>[] = [{
      accessorKey: 'name',
      header: 'Contract',
      cell: ({ row }) => {
        const id = row.original.id;
        renderCount.set(id, (renderCount.get(id) ?? 0) + 1);
        return row.original.name;
      },
    }];
    const { rerender } = render(
      <VirtualizedDataTable columns={countingColumns} data={rows} getRowId={(row) => row.id} />,
    );
    expect(renderCount).toEqual(new Map([['contract-1', 1], ['contract-2', 1]]));

    const updatedRows = [{ ...rows[0], name: 'BioGem supply updated' }, rows[1]];
    rerender(<VirtualizedDataTable columns={countingColumns} data={updatedRows} getRowId={(row) => row.id} />);

    expect(screen.getByText('BioGem supply updated')).toBeTruthy();
    expect(renderCount).toEqual(new Map([['contract-1', 2], ['contract-2', 1]]));
  });
});
