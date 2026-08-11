import { Link, useRouterState } from '@tanstack/react-router';
import { ChevronDown, LogOut, Redo2, Search, Settings2, Undo2 } from 'lucide-react';
import { LayoutGroup, m, useReducedMotion } from 'motion/react';
import type { ReactNode } from "react";
import { useCommandStack } from "../../lib/commands/CommandStackContext";
import { endSession, type AuthenticatedRoutePath } from '../../lib/session/sessionController';
import { CommandPalette } from "../ui/CommandPalette";
import { Button } from '../ui/button';
import { DensityToggle } from '../ui/density-toggle';

const recordNavigation = [
  ["/deliveries", "Deliveries"],
  ["/contracts", "Contracts"],
  ["/capacity-bookings", "Capacity bookings"],
  ["/transfers", "Transfers"],
  ["/biotickets", "Biotickets"],
  ["/goo-certificates", "GoO transactions"],
  ["/market-prices", "Market prices"],
  ["/tax-tariffs", "Taxes and tariffs"],
  ["/hedges", "Hedges"],
] as const satisfies readonly (readonly [AuthenticatedRoutePath, string])[];

const workspaceNavigation = [
  ["/dashboard", "Dashboard"],
  ['/workflow', 'Workflow'],
] as const satisfies readonly (readonly [AuthenticatedRoutePath, string])[];

const selectorSpring = { bounce: 0.05, duration: 0.25, type: 'spring' } as const;

/** Vertical adaptation of SmoothUI Animated Tabs for route-backed sidebar selection. */
function NavigationGroup({ activePath, label, items }: { activePath: string; label: string; items: readonly (readonly [AuthenticatedRoutePath, string])[] }) {
  const reduceMotion = useReducedMotion();
  return (
    <section data-slot="sidebar-group">
      <h2>{label}</h2>
      <nav aria-label={label}>
        {items.map(([to, itemLabel]) => {
          const active = activePath === to;
          return (
            <Link data-slot="sidebar-link" key={to} to={to} activeProps={{ 'aria-current': 'page' }}>
              {active ? (
                <m.span
                  aria-hidden="true"
                  data-slot="sidebar-selection-indicator"
                  layoutId="tradebook-sidebar-panel-selector"
                  transition={reduceMotion ? { duration: 0 } : selectorSpring}
                />
              ) : null}
              <span data-slot="sidebar-link-label">{itemLabel}</span>
            </Link>
          );
        })}
      </nav>
    </section>
  );
}

export function AppShell({ children }: { children: ReactNode }) {
  const commands = useCommandStack();
  const activePath = useRouterState({ select: (state) => state.location.pathname });
  const openCommandPalette = () => window.dispatchEvent(new Event('tradebook:open-command-palette'));
  return (
    <div className="app-shell">
      <aside data-slot="app-sidebar">
        <div data-slot="sidebar-brand">
          <span data-slot="brand-mark" aria-hidden="true">B</span>
          <div><p>BioGem</p><span>Tradebook</span></div>
        </div>
        <Button data-slot="sidebar-search" size="sm" intent="ghost" type="button" onClick={openCommandPalette} aria-label="Open command search">
          <Search aria-hidden="true" size={15} /><span>Search</span><kbd>⌘K</kbd>
        </Button>
        <LayoutGroup id="tradebook-sidebar-navigation">
          <div data-slot="sidebar-navigation">
            <NavigationGroup activePath={activePath} label="Records" items={recordNavigation} />
            <NavigationGroup activePath={activePath} label="Workspace" items={workspaceNavigation} />
          </div>
        </LayoutGroup>
        <details data-slot="sidebar-settings">
          <summary><Settings2 aria-hidden="true" size={15} /><span>Preferences</span><ChevronDown aria-hidden="true" size={14} /></summary>
          <DensityToggle />
        </details>
        <div data-slot="sidebar-footer">
          <Button aria-label="Undo" intent="ghost" size="icon" disabled={!commands.canUndo} onClick={() => void commands.undo()}><Undo2 aria-hidden="true" size={15} /></Button>
          <Button aria-label="Redo" intent="ghost" size="icon" disabled={!commands.canRedo} onClick={() => void commands.redo().catch(() => undefined)}><Redo2 aria-hidden="true" size={15} /></Button>
          <Button data-slot="sidebar-signout" intent="ghost" type="button" onClick={() => void endSession('logout')}><LogOut aria-hidden="true" size={15} /><span>Sign out</span></Button>
        </div>
      </aside>
      <div data-slot="workspace-frame">
        <main className="workspace">{children}</main>
        <footer data-slot="workspace-shortcuts" aria-label="Keyboard shortcuts">
          <span><kbd>⌘K</kbd> commands</span>
          <span><kbd>⌘Z</kbd> undo</span>
          <span><kbd>g</kbd> then route key</span>
        </footer>
      </div>
      <CommandPalette />
    </div>
  );
}
