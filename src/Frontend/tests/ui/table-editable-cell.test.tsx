import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { TableEditableCell } from '../../src/components/ui/table-editable-cell';

describe('TableEditableCell', () => {
  it('opens its editor without forwarding the click to the table row', async () => {
    const onCommit = vi.fn();
    const onRowClick = vi.fn();

    render(
      <div onClick={onRowClick}>
        <TableEditableCell label="Contract name" onCommit={onCommit} value="Original" />
      </div>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Edit Contract name' }));

    const input = await screen.findByRole('textbox', { name: 'Contract name' });
    expect(onRowClick).not.toHaveBeenCalled();
    fireEvent.change(input, { target: { value: 'Updated' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save Contract name' }));

    expect(onCommit).toHaveBeenCalledWith('Updated');
    expect(onRowClick).not.toHaveBeenCalled();
  });
});
