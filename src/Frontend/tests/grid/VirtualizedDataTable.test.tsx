import { fireEvent, render, screen } from '@testing-library/react';
import type { ColumnDef } from '@tanstack/react-table';
import { describe, expect, it, vi } from 'vitest';
import { VirtualizedDataTable } from '../../src/lib/grid/VirtualizedDataTable';

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
});
