import { Link } from '@tanstack/react-router';
import type { ReactNode } from "react";
import { useCommandStack } from "../../lib/commands/CommandStackContext";
import { endSession, type AuthenticatedRoutePath } from '../../lib/session/sessionController';
import { CommandPalette } from "../ui/CommandPalette";

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
    <div className="grid min-h-screen grid-cols-4 max-[800px]:grid-cols-1">
      <aside>
        <div>
          <p className="mb-1 text-xs font-extrabold uppercase tracking-widest text-gray-600">BioGem</p>
          <h1>Tradebook</h1>
        </div>
        <nav aria-label="Primary">
          {navigation.map(([to, label]) => (
            <Link key={to} to={to} activeProps={{ 'aria-current': 'page' }}>
              {label}
            </Link>
          ))}
        </nav>
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            className="bg-gray-200 text-gray-800"
            disabled={!commands.canUndo}
            onClick={() => void commands.undo()}
          >
            Undo
          </button>
          <button
            type="button"
            className="bg-gray-200 text-gray-800"
            disabled={!commands.canRedo}
            onClick={() => void commands.redo().catch(() => undefined)}
          >
            Redo
          </button>
        </div>
        <button type="button" className="bg-gray-200 text-gray-800" onClick={() => void endSession('logout')}>
          Sign out
        </button>
      </aside>
      <main className="col-span-3 min-w-0 p-8 max-[800px]:col-span-1 max-[800px]:p-4">{children}</main>
      <CommandPalette />
    </div>
  );
}
