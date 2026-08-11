import { fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { axe } from 'vitest-axe';
import { describe, expect, it, vi } from 'vitest';
import { KpiTile } from '../../src/components/kpi/kpi-tile';
import { Button } from '../../src/components/ui/button';
import { Checkbox } from '../../src/components/ui/checkbox';
import { Dialog, DialogClose, DialogContent, DialogDescription, DialogTitle, DialogTrigger } from '../../src/components/ui/dialog';
import { Frame, FrameHeader, FramePanel, FrameTitle } from '../../src/components/ui/frame';
import { Input } from '../../src/components/ui/input';
import { NumberInput } from '../../src/components/ui/number-input';
import { Popover, PopoverContent, PopoverPositioner, PopoverPortal, PopoverTrigger } from '../../src/components/ui/popover';
import { RecordDetailPanel } from '../../src/components/ui/record-detail-panel';
import { Select } from '../../src/components/ui/select';
import { Tabs, TabsList, TabsPanel, TabsTab } from '../../src/components/ui/tabs';
import { replaceAnimatedNumber } from '../helpers/animatedNumberInput';

describe('owned UI primitives', () => {
  const jsdomAxeOptions = { rules: { 'color-contrast': { enabled: false } } };
  it('renders a KPI tile without accessibility violations', async () => {
    const { container, getByText } = render(<KpiTile label="Position" value="€42" delta={-2.5} spark={<svg aria-label="Position trend" />} />);
    expect(getByText('€42')).toBeTruthy();
    expect((await axe(container, jsdomAxeOptions)).violations).toEqual([]);
  });

  it('renders the Base UI button accessibly', async () => {
    const { container, getByRole, rerender } = render(<Button>Save</Button>);
    expect(getByRole('button', { name: 'Save' })).toBeTruthy();
    expect(getByRole('button', { name: 'Save' }).className).toContain('ui-button');
    expect(getByRole('button', { name: 'Save' }).className).toContain('ui-button-primary');
    expect(getByRole('button', { name: 'Save' }).className).toContain('ui-button-md');

    rerender(<Button intent="secondary" size="sm">Save</Button>);
    expect(getByRole('button', { name: 'Save' }).className).toContain('ui-button-secondary');
    expect(getByRole('button', { name: 'Save' }).className).toContain('ui-button-sm');

    rerender(<Button intent="ghost" size="icon" aria-label="Action">A</Button>);
    const iconButton = getByRole('button', { name: 'Action' });
    expect(iconButton.className).toContain('ui-button-ghost');
    expect(iconButton.className).toContain('ui-button-icon');
    expect((await axe(container, jsdomAxeOptions)).violations).toEqual([]);
  });

  it('uses the SmoothUI-derived animated Base UI checkbox for checked and mixed states', () => {
    const onCheckedChange = vi.fn();
    const { container, rerender } = render(<Checkbox aria-label="Select record" checked={false} onCheckedChange={onCheckedChange} />);
    const checkbox = screen.getByRole('checkbox', { name: 'Select record' });
    expect(checkbox.className).toContain('ui-checkbox');

    fireEvent.click(checkbox);
    expect(onCheckedChange).toHaveBeenCalledWith(true);

    rerender(<Checkbox aria-label="Select record" checked onCheckedChange={onCheckedChange} />);
    expect(container.querySelector('[data-slot="checkbox-indicator"] svg')).toBeTruthy();

    rerender(<Checkbox aria-label="Select record" indeterminate onCheckedChange={onCheckedChange} />);
    expect(screen.getByRole('checkbox', { name: 'Select record' }).getAttribute('aria-checked')).toBe('mixed');
    expect(container.querySelector('[data-slot="checkbox-indicator"] svg')).toBeTruthy();
  });

  it('uses animated library inputs for text and numeric values', async () => {
    const onChange = vi.fn();
    const { container } = render(<Input aria-label="Name" value="BioGem" onChange={onChange} />);
    const input = screen.getByRole('textbox', { name: 'Name' });
    expect(input.getAttribute('data-slot')).toBe('input');
    expect(input.closest('[data-slot="input-control"]')).toBeTruthy();
    fireEvent.change(input, { target: { value: 'Tradebook' } });
    expect(onChange).toHaveBeenCalled();

    const onValueChange = vi.fn();
    function AnimatedNumberHarness() {
      const [value, setValue] = useState('123.456');
      return <NumberInput aria-label="Volume" value={value} onValueChange={(nextValue) => { onValueChange(nextValue); setValue(nextValue); }} />;
    }
    const { container: numberContainer } = render(<AnimatedNumberHarness />);
    const numberEditor = screen.getByRole('textbox', { name: 'Volume' });
    expect(numberEditor.textContent).toBe('123.456');
    await replaceAnimatedNumber(numberEditor, '124.5');
    expect(onValueChange).toHaveBeenCalledWith('124.5');
    expect(numberContainer.querySelector('input[type="number"]')).toBeNull();
    expect((await axe(container, jsdomAxeOptions)).violations).toEqual([]);
    expect((await axe(numberContainer, jsdomAxeOptions)).violations).toEqual([]);
  });

  it('renders the Base UI dialog, popover, and select primitives accessibly', async () => {
    const { container } = render(<><Dialog><DialogTrigger>Details</DialogTrigger><DialogContent><DialogTitle>Details</DialogTitle><DialogDescription>Position details</DialogDescription><DialogClose>Close</DialogClose></DialogContent></Dialog><Popover><PopoverTrigger>Filters</PopoverTrigger><PopoverPortal><PopoverPositioner><PopoverContent>Filter controls</PopoverContent></PopoverPositioner></PopoverPortal></Popover><Select label="Side" value="Buy" options={['Buy', 'Sell']} onValueChange={vi.fn()} /></>);
    expect((await axe(container, jsdomAxeOptions)).violations).toEqual([]);
  });

  it('composes the sourced frame and tabs primitives into the record workspace', async () => {
    const onOpenChange = vi.fn();
    render(
      <RecordDetailPanel
        open
        onOpenChange={onOpenChange}
        eyebrow="Delivery"
        title="CI-2026-0412"
        description="Sourcing delivery for April"
        recordId="delivery-1"
        version={3}
        properties={<label>Volume<input aria-label="Volume" /></label>}
        context={<Frame><FrameHeader><FrameTitle>Contract</FrameTitle></FrameHeader><FramePanel>Contract facts</FramePanel></Frame>}
        actions={<Button>Save changes</Button>}
      />,
    );

    const drawer = screen.getByRole('dialog', { name: 'CI-2026-0412' });
    expect(drawer).toBeTruthy();
    expect(drawer.closest('[data-slot="drawer-popup"]')?.getAttribute('data-position')).toBe('right');
    const backdrop = document.querySelector('[data-slot="drawer-backdrop"]');
    expect(backdrop).toBeTruthy();
    fireEvent.click(screen.getByRole('tab', { name: 'Activity' }));
    expect(screen.getByText('Current revision')).toBeTruthy();
    fireEvent.pointerDown(backdrop!, { button: 0, pointerType: 'mouse' });
    fireEvent.pointerUp(backdrop!, { button: 0, pointerType: 'mouse' });
    fireEvent.click(backdrop!);
    expect(onOpenChange.mock.calls[0]?.[0]).toBe(false);
    expect(onOpenChange.mock.calls[0]?.[1]).toEqual(expect.objectContaining({ reason: 'outside-press' }));
  });

  it('switches the coss-derived tabs without custom state', () => {
    render(<Tabs defaultValue="one"><TabsList><TabsTab value="one">One</TabsTab><TabsTab value="two">Two</TabsTab></TabsList><TabsPanel value="one">First panel</TabsPanel><TabsPanel value="two">Second panel</TabsPanel></Tabs>);
    expect(screen.getByText('First panel')).toBeTruthy();
    fireEvent.click(screen.getByRole('tab', { name: 'Two' }));
    expect(screen.getByText('Second panel')).toBeTruthy();
  });
});
