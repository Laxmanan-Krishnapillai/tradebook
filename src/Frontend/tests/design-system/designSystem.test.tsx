import { readdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MotionProvider } from '../../src/components/providers/motion-provider';
import { DensityToggle } from '../../src/components/ui/density-toggle';
import { EmptyState } from '../../src/components/ui/empty-state';
import { NumericCell } from '../../src/components/ui/numeric-cell';
import { Skeleton, TableSkeleton } from '../../src/components/ui/skeleton';
import { Table, TableCell, TableRow } from '../../src/components/ui/table';
import { usePreferences } from '../../src/stores/preferences';

vi.mock('@number-flow/react', () => ({
  default: ({ value, animated, respectMotionPreference, ...props }: { value: number; animated: boolean; respectMotionPreference: boolean }) => (
    <span data-animated={animated} data-respect-motion={respectMotionPreference} {...props}>{value}</span>
  ),
}));

const frontendRoot = resolve(import.meta.dirname, '../..');
const source = (path: string) => readFileSync(resolve(frontendRoot, path), 'utf8');

afterEach(() => {
  cleanup();
  usePreferences.setState({ density: 'regular', reduceMotion: false, theme: 'system' });
  document.documentElement.className = '';
});

describe('Task 23 design system', () => {
  it('standardizes ShadCN generation and owned primitives on stable Base UI', () => {
    const config = JSON.parse(source('components.json')) as { style?: string; iconLibrary?: string };
    const packageManifest = JSON.parse(source('package.json')) as { dependencies?: Record<string, string> };

    expect(config.style).toBe('base-nova');
    expect(config.iconLibrary).toBe('lucide');
    expect(packageManifest.dependencies?.['@base-ui/react']).toBe('1.7.0');
    expect(packageManifest.dependencies?.['@base-ui-components/react']).toBeUndefined();

    const uiSource = readdirSync(resolve(frontendRoot, 'src/components/ui'))
      .filter((file) => file.endsWith('.tsx'))
      .map((file) => source(`src/components/ui/${file}`))
      .join('\n');
    expect(uiSource).not.toContain('@base-ui-components/react');
  });

  it('DS-02 and DS-05 render live numbers with stable OpenType numerals', () => {
    const { container, rerender } = render(<NumericCell animate value={1200} />);
    expect(container.firstElementChild?.classList.contains('tabular-nums')).toBe(true);
    expect(container.firstElementChild?.classList.contains('slashed-zero')).toBe(true);
    expect(container.firstElementChild?.classList.contains('lining-nums')).toBe(true);
    rerender(<NumericCell animate flashOnChange value={1201} />);
    expect(container.firstElementChild?.getAttribute('data-animated')).toBe('true');
    expect(container.firstElementChild?.getAttribute('data-respect-motion')).toBe('true');
  });

  it('DS-04 keeps dark neutral hue stable while swapping lightness', () => {
    const css = source('src/styles.css');
    expect(css).toMatch(/:root[\s\S]*--neutral-50: oklch\(0\.985 0\.002 285\)/);
    expect(css).toMatch(/:root[\s\S]*--neutral-900: oklch\(0\.18 0\.007 285\)/);
    expect(css).toMatch(/\.dark[\s\S]*--neutral-50: oklch\(0\.145 0\.006 285\)/);
    expect(css).toMatch(/\.dark[\s\S]*--neutral-900: oklch\(0\.94 0\.003 285\)/);
    expect(css).not.toContain('filter: invert');
  });

  it('DS-14 loads redesigned typography tokens and font imports', () => {
    const css = source('src/styles.css');
    const fontsCss = source('src/styles/fonts.css');
    expect(css).toContain('--font-sans: "Instrument Sans Variable", ui-sans-serif, system-ui, sans-serif;');
    expect(css).toContain('--font-mono: "IBM Plex Mono", ui-monospace, monospace;');
    expect(fontsCss).toContain('@import "@fontsource-variable/instrument-sans";');
    expect(fontsCss).toContain('@import "@fontsource/ibm-plex-mono/400.css";');
    expect(fontsCss).toContain('@import "@fontsource/ibm-plex-mono/500.css";');
  });

  it('DS-03, DS-07, DS-08, DS-09 and DS-12 preserve static design and performance contracts', () => {
    const uiSource = readdirSync(resolve(frontendRoot, 'src/components/ui'))
      .filter((file) => file.endsWith('.tsx'))
      .map((file) => source(`src/components/ui/${file}`))
      .join('\n');
    const hotSurfaceSource = [
      source('src/components/canvas/WorkflowCanvas.css'),
      source('src/components/canvas/WorkflowCanvas.tsx'),
      source('src/components/grid/VirtualizedDataTable.tsx'),
    ].join('\n');
    const applicationSource = readdirSync(resolve(frontendRoot, 'src/components/ui'))
      .filter((file) => file.endsWith('.tsx'))
      .map((file) => source(`src/components/ui/${file}`))
      .join('\n');

    expect(uiSource).not.toMatch(/#[0-9a-fA-F]{3,8}\b|\b[0-9]+px\b/);
    expect(hotSurfaceSource).not.toMatch(/box-shadow|backdrop-blur|AnimatePresence|\blayout\b/);
    expect(applicationSource).not.toMatch(/\bmotion\.[a-z]/);
    expect(source('src/components/providers/motion-provider.tsx')).toMatch(/LazyMotion[\s\S]*strict/);
    expect(uiSource).not.toMatch(/Spinner|animate-spin/);
    expect(source('src/lib/streaming/eventBatcher.ts')).toContain('bufferTime(this.windowTimeMs)');
  });

  it('DS-06 collapses motion for the in-app preference', () => {
    vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: false })));
    usePreferences.setState({ reduceMotion: true });
    render(<MotionProvider><span>Content</span></MotionProvider>);
    expect(document.documentElement.classList.contains('reduce-motion')).toBe(true);
    expect(source('src/styles.css')).toContain('@media (prefers-reduced-motion: reduce)');
  });

  it('DS-09 supplies layout-matched skeleton and designed empty treatments', () => {
    render(<><Skeleton data-testid="skeleton" /><TableSkeleton rows={2} columns={3} /><EmptyState title="No trades" description="Create a trade to begin." /></>);
    expect(screen.getByTestId('skeleton').getAttribute('aria-hidden')).toBe('true');
    expect(screen.getByRole('status', { name: 'Loading table' }).children).toHaveLength(2);
    expect(screen.getByRole('heading', { name: 'No trades' })).toBeTruthy();
  });

  it('DS-10 exposes all interactive states through one focus-ring token', () => {
    const css = source('src/styles.css');
    for (const state of [':hover', ':focus-visible', ':active', ':disabled', '[aria-pressed="true"]']) expect(css).toContain(state);
    expect(css.match(/--ring-focus:/g)).toHaveLength(1);
    expect(css).toContain('button:focus-visible');
    expect(source('src/components/ui/skeleton.tsx')).toContain('animate-pulse');
  });

  it('DS-11 toggles condensed regular and relaxed table density', () => {
    const gridSource = source('src/components/grid/VirtualizedDataTable.tsx');
    expect(gridSource).toContain('condensed: 34');
    expect(gridSource).toContain('regular: 42');
    expect(gridSource).toContain('relaxed: 48');

    render(<><DensityToggle /><Table><tbody><TableRow><TableCell>Trade</TableCell></TableRow></tbody></Table></>);
    fireEvent.click(screen.getByRole('button', { name: 'condensed' }));
    expect(screen.getByRole('table').getAttribute('data-density')).toBe('condensed');
    expect(screen.getByRole('cell').classList.contains('py-1')).toBe(true);
    fireEvent.click(screen.getByRole('button', { name: 'relaxed' }));
    expect(screen.getByRole('table').getAttribute('data-density')).toBe('relaxed');
    expect(screen.getByRole('cell').classList.contains('py-3')).toBe(true);
  });
});
