import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

const css = readFileSync(resolve(import.meta.dirname, '../../src/styles.css'), 'utf8');
const drawerStyles = css.slice(css.indexOf('[data-slot="drawer-backdrop"]'), css.indexOf('.ui-tabs'));

describe('drawer motion', () => {
  it('uses Base UI transition and swipe state for reversible motion', () => {
    expect(drawerStyles).toContain('[data-starting-style]');
    expect(drawerStyles).toContain('[data-ending-style]');
    expect(drawerStyles).toContain('[data-swiping]');
    expect(drawerStyles).toContain('--drawer-swipe-progress');
    expect(drawerStyles).toContain('--drawer-swipe-strength');
    expect(drawerStyles).toContain('var(--duration-moderate)');
    expect(drawerStyles).not.toContain('260ms');
  });

  it.each([
    ['right', 'translate3d(100%, 0, 0)'],
    ['left', 'translate3d(-100%, 0, 0)'],
    ['top', 'translate3d(0, -100%, 0)'],
    ['bottom', 'translate3d(0, 100%, 0)'],
  ])('animates the %s drawer from its own edge', (position, transform) => {
    expect(drawerStyles).toContain(`[data-position="${position}"][data-starting-style]`);
    expect(drawerStyles).toContain(transform);
  });
});
