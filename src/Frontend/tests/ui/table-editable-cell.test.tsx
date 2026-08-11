import { act, fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { TableEditableCell } from '../../src/components/ui/table-editable-cell';

describe('TableEditableCell', () => {
  it('edits in the existing cell surface without forwarding the click to the table row', async () => {
    const onCommit = vi.fn();
    const onRowClick = vi.fn();

    render(
      <div onClick={onRowClick}>
        <TableEditableCell label="Contract name" onCommit={onCommit} value="Original" />
      </div>,
    );

    const input = screen.getByRole('textbox', { name: 'Contract name' });
    expect(input.hasAttribute('readonly')).toBe(true);
    fireEvent.click(input);

    await vi.waitFor(() => expect(document.activeElement).toBe(input));
    expect(input.hasAttribute('readonly')).toBe(false);
    expect(screen.getByRole('textbox', { name: 'Contract name' })).toBe(input);
    expect(onRowClick).not.toHaveBeenCalled();

    fireEvent.change(input, { target: { value: 'Updated' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    expect(onCommit).toHaveBeenCalledWith('Updated');
    await vi.waitFor(() => expect(input.hasAttribute('readonly')).toBe(true));
    expect(screen.getByRole('textbox', { name: 'Contract name' })).toBe(input);
    expect(screen.queryByRole('button', { name: /save|cancel|edit/i })).toBeNull();
    expect(onRowClick).not.toHaveBeenCalled();
  });

  it('keeps the optimistic draft in the same input while the mutation is pending', async () => {
    let resolveCommit: () => void = () => undefined;
    const onCommit = vi.fn(() => new Promise<void>((resolve) => { resolveCommit = resolve; }));

    render(<TableEditableCell label="Contract name" onCommit={onCommit} value="Original" />);
    const input = screen.getByRole('textbox', { name: 'Contract name' });
    const cell = input.closest('[data-slot="table-editable-cell"]');
    fireEvent.click(input);
    fireEvent.change(input, { target: { value: 'Updated now' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    expect((input as HTMLInputElement).value).toBe('Updated now');
    expect(screen.getByRole('textbox', { name: 'Contract name' })).toBe(input);
    expect(cell?.getAttribute('aria-busy')).toBe('true');

    await act(async () => resolveCommit());
    await vi.waitFor(() => expect(cell?.getAttribute('aria-busy')).toBe('false'));
    expect(screen.getByRole('textbox', { name: 'Contract name' })).toBe(input);
  });

  it('focuses a cell-sized decimal input on the first click', async () => {
    render(<TableEditableCell kind="number" label="Quantity" onCommit={vi.fn()} value="12.5" />);

    const input = screen.getByRole('textbox', { name: 'Quantity' });
    expect(input.getAttribute('inputmode')).toBe('decimal');
    fireEvent.click(input);

    await vi.waitFor(() => expect(document.activeElement).toBe(input));
    expect(input.hasAttribute('readonly')).toBe(false);
    expect((input as HTMLInputElement).selectionStart).toBe(0);
    expect((input as HTMLInputElement).selectionEnd).toBe(4);
    expect(screen.getByRole('textbox', { name: 'Quantity' })).toBe(input);
  });

  it('opens the existing select trigger on the first click and keeps it mounted after commit', async () => {
    const onCommit = vi.fn();
    render(<TableEditableCell label="Product" onCommit={onCommit} options={['Gas', 'Power']} value="Gas" />);

    const trigger = screen.getByRole('combobox', { name: 'Product' });
    fireEvent.click(trigger);

    expect(await screen.findByRole('listbox')).toBeTruthy();
    const option = screen.getByRole('option', { name: 'Power' });
    fireEvent.pointerDown(option, { button: 0, pointerType: 'mouse' });
    fireEvent.click(option);

    expect(onCommit).toHaveBeenCalledOnce();
    expect(onCommit).toHaveBeenCalledWith('Power');
    await vi.waitFor(() => expect(screen.queryByRole('listbox')).toBeNull());
    expect(screen.getByRole('combobox', { name: 'Product' })).toBe(trigger);
  });

  it('closes select editing without committing when Escape closes the popup', async () => {
    const onCommit = vi.fn();
    render(<TableEditableCell label="Product" onCommit={onCommit} options={['Gas', 'Power']} value="Gas" />);

    const trigger = screen.getByRole('combobox', { name: 'Product' });
    fireEvent.click(trigger);
    const listbox = await screen.findByRole('listbox');
    fireEvent.keyDown(listbox, { key: 'Escape' });

    await vi.waitFor(() => expect(screen.queryByRole('listbox')).toBeNull());
    expect(screen.getByRole('combobox', { name: 'Product' })).toBe(trigger);
    expect(onCommit).not.toHaveBeenCalled();
  });
});
