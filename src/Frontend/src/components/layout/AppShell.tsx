import { Link } from '@tanstack/react-router';
import type { ReactNode } from "react";
import { useCommandStack } from "../../lib/commands/CommandStackContext";
import { endSession, type AuthenticatedRoutePath } from '../../lib/session/sessionController';
import { CommandPalette } from "../ui/CommandPalette";
import { DensityToggle } from '../ui/density-toggle';

const navigation = [
  ["/deliveries", "Deliveries"],
  ["/contracts", "Contracts"],
  ["/capacity-bookings", "Capacity bookings"],
  ["/transfers", "Transfers"],
  ["/biotickets", "Biotickets"],
  ["/goo-certificates", "GoO transactions"],
  ["/market-prices", "Market prices"],
  ["/tax-tariffs", "Taxes and tariffs"],
  ["/hedges", "Hedges"],
  ["/dashboard", "Dashboard"],
  ['/workflow', 'Workflow'],
] as const satisfies readonly (readonly [AuthenticatedRoutePath, string])[];

export function AppShell({ children }: { children: ReactNode }) {
  const commands = useCommandStack();
  return (
    <div className="app-shell">
      <aside>
        <div>
          <p className="eyebrow">BioGem</p>
          <h1>Tradebook</h1>
        </div>
        <nav aria-label="Primary">
          {navigation.map(([to, label]) => (
            <Link key={to} to={to} activeProps={{ 'aria-current': 'page' }}>
              {label}
            </Link>
          ))}
        </nav>
        <DensityToggle />
        <div className="toolbar">
          <button
            type="button"
            className="secondary"
            disabled={!commands.canUndo}
            onClick={() => void commands.undo()}
          >
            Undo
          </button>
          <button
            type="button"
            className="secondary"
            disabled={!commands.canRedo}
            onClick={() => void commands.redo().catch(() => undefined)}
          >
            Redo
          </button>
        </div>
        <button type="button" className="secondary" onClick={() => void endSession('logout')}>
          Sign out
        </button>
      </aside>
      <main className="workspace">{children}</main>
      <CommandPalette />
    </div>
  );
}
