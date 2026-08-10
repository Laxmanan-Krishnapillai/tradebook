import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { z } from 'zod';
import { ValidatedForm } from '../../src/components/ui/validated-form';
import { moneyInputField } from '../../src/lib/validation/money-input';

interface UpsertValues {
  priceDate: string;
  ttfEurMwh?: string | null;
  version: number;
}

const schema: z.ZodType<UpsertValues> = z.object({
  priceDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, { error: 'Enter the market date as a full day (YYYY-MM-DD).' }),
  ttfEurMwh: moneyInputField({ label: 'TTF EUR/MWh', required: true }),
  version: z.int().min(0),
});

function Harness({ onValid }: { onValid: (values: UpsertValues) => void }) {
  const [values, setValues] = useState<UpsertValues>({ priceDate: '2026-01-05', ttfEurMwh: null, version: 0 });
  return (
    <ValidatedForm schema={schema} values={values} onValid={onValid}>
      <label>
        TTF EUR/MWh
        <input
          value={values.ttfEurMwh ?? ''}
          onChange={(event) => setValues((value) => ({ ...value, ttfEurMwh: event.target.value === '' ? null : event.target.value }))}
        />
      </label>
      <button type="submit">Save</button>
    </ValidatedForm>
  );
}

const rootSchema = z.custom<{ name: string }>(
  (candidate) => typeof (candidate as { name?: unknown }).name === 'string' && (candidate as { name: string }).name.length > 0,
  { error: 'Complete the required contract fields.' },
);
const rootValues = { name: '' };

describe('ValidatedForm', () => {
  it('renders a role=alert summary instead of silently swallowing an invalid submit', async () => {
    const onValid = vi.fn();
    render(<Harness onValid={onValid} />);
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('TTF EUR/MWh is required.');
    expect(onValid).not.toHaveBeenCalled();
  });

  it('revalidates on change after a failed submit and clears the summary', async () => {
    const onValid = vi.fn();
    render(<Harness onValid={onValid} />);
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await screen.findByRole('alert');
    fireEvent.change(screen.getByLabelText('TTF EUR/MWh'), { target: { value: '31.5' } });
    await waitFor(() => expect(screen.queryByRole('alert')).toBeNull());
  });

  it('accepts market-price style string input and passes the schema output to onValid', async () => {
    const onValid = vi.fn();
    render(<Harness onValid={onValid} />);
    fireEvent.change(screen.getByLabelText('TTF EUR/MWh'), { target: { value: '31.5' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(onValid).toHaveBeenCalledWith({ priceDate: '2026-01-05', ttfEurMwh: '31.5', version: 0 }));
  });

  it('surfaces path-less failures from whole-object z.custom schemas', async () => {
    const onValid = vi.fn();
    render(
      <ValidatedForm schema={rootSchema} values={rootValues} onValid={onValid}>
        <button type="submit">Save</button>
      </ValidatedForm>,
    );
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('Complete the required contract fields.');
    expect(onValid).not.toHaveBeenCalled();
  });
});
