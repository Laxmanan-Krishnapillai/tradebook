import { render } from '@testing-library/react';
import { axe } from 'vitest-axe';
import { describe, expect, it, vi } from 'vitest';
import { KpiTile } from '../../src/components/kpi/kpi-tile';
import { Button } from '../../src/components/ui/button';
import { Dialog, DialogClose, DialogContent, DialogDescription, DialogTitle, DialogTrigger } from '../../src/components/ui/dialog';
import { Popover, PopoverContent, PopoverPositioner, PopoverPortal, PopoverTrigger } from '../../src/components/ui/popover';
import { Select } from '../../src/components/ui/select';

describe('owned UI primitives', () => {
  const jsdomAxeOptions = { rules: { 'color-contrast': { enabled: false } } };
  it('renders a KPI tile without accessibility violations', async () => {
    const { container, getByText } = render(<KpiTile label="Position" value="€42" delta={-2.5} spark={<svg aria-label="Position trend" />} />);
    expect(getByText('€42')).toBeTruthy();
    expect((await axe(container, jsdomAxeOptions)).violations).toEqual([]);
  });

  it('renders the Base UI button accessibly', async () => {
    const { container, getByRole } = render(<Button>Save</Button>);
    expect(getByRole('button', { name: 'Save' })).toBeTruthy();
    expect((await axe(container, jsdomAxeOptions)).violations).toEqual([]);
  });

  it('renders the Base UI dialog, popover, and select primitives accessibly', async () => {
    const { container } = render(<><Dialog><DialogTrigger>Details</DialogTrigger><DialogContent><DialogTitle>Details</DialogTitle><DialogDescription>Position details</DialogDescription><DialogClose>Close</DialogClose></DialogContent></Dialog><Popover><PopoverTrigger>Filters</PopoverTrigger><PopoverPortal><PopoverPositioner><PopoverContent>Filter controls</PopoverContent></PopoverPositioner></PopoverPortal></Popover><Select label="Side" value="Buy" options={['Buy', 'Sell']} onValueChange={vi.fn()} /></>);
    expect((await axe(container, jsdomAxeOptions)).violations).toEqual([]);
  });
});
