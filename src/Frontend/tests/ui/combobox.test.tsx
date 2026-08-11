import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { Combobox } from '../../src/components/ui/combobox';

describe('Tradebook Combobox', () => {
  it('filters human-readable records and returns the authoritative id', async () => {
    const onChange = vi.fn();
    render(
      <Combobox
        label="Contract"
        onChange={onChange}
        options={[
          { label: 'BioGem supply', value: 'contract-1', description: 'Vattenfall Energy Trading' },
          { label: 'Nordic transport', value: 'contract-2', description: 'Gasunie Transport' },
        ]}
      />,
    );

    const input = screen.getByRole('combobox', { name: 'Contract' });
    fireEvent.click(screen.getByRole('button', { name: 'Show Contract options' }));
    fireEvent.change(input, { target: { value: 'Nordic' } });
    fireEvent.click(await screen.findByText('Nordic transport'));
    expect(onChange).toHaveBeenCalledWith('contract-2');
  });
});
