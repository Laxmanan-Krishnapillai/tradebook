import { useNavigate } from '@tanstack/react-router';
import { useHotkey, useHotkeySequences, type HotkeySequence } from '@tanstack/react-hotkeys';
import { Command } from "cmdk";
import { useEffect, useState } from "react";
import type { AuthenticatedRoutePath } from '../../lib/session/sessionController';
import { useUiStore } from '../../lib/state/useUiStore';

const shortcutRoutes = {
  d: '/deliveries',
  c: '/contracts',
  m: '/market-prices',
  b: '/capacity-bookings',
  t: '/transfers',
  h: '/hedges',
  w: '/workflow',
} as const satisfies Readonly<Record<string, AuthenticatedRoutePath>>;

export function CommandPalette() {
  const navigate = useNavigate();
  const openModal = useUiStore((state) => state.openModal);
  const [open, setOpen] = useState(false);
  useEffect(() => {
    const openPalette = () => setOpen(true);
    window.addEventListener('tradebook:open-command-palette', openPalette);
    return () => window.removeEventListener('tradebook:open-command-palette', openPalette);
  }, []);
  useHotkey('Mod+K', () => setOpen((value) => !value), {
    ignoreInputs: false,
    preventDefault: true,
  });
  useHotkey('Escape', () => setOpen(false), { enabled: open });
  useHotkeySequences(
    Object.entries(shortcutRoutes).map(([key, path]) => ({
      sequence: ['G', key.toUpperCase()] as HotkeySequence,
      callback: () => void navigate({ to: path }),
    })),
    {
      ignoreInputs: true,
      preventDefault: true,
      timeout: 750,
    },
  );
  const select = (path: AuthenticatedRoutePath) => {
    void navigate({ to: path });
    setOpen(false);
  };
  const createDelivery = () => {
    void navigate({ to: '/deliveries' }).then(() => openModal('create-delivery'));
    setOpen(false);
  };
  return (
    <Command.Dialog open={open} onOpenChange={setOpen} label="Command palette">
      <Command.Input placeholder="Type a command or search..." />
      <Command.List>
        <Command.Empty>No results.</Command.Empty>
        <Command.Group heading="Navigate">
          <Command.Item onSelect={() => select("/deliveries")}>
            Go to Deliveries
          </Command.Item>
          <Command.Item onSelect={() => select("/contracts")}>
            Go to Contracts
          </Command.Item>
          <Command.Item onSelect={() => select("/capacity-bookings")}>
            Go to Capacity Bookings
          </Command.Item>
          <Command.Item onSelect={() => select("/transfers")}>
            Go to Transfers
          </Command.Item>
          <Command.Item onSelect={() => select("/biotickets")}>
            Go to Biotickets
          </Command.Item>
          <Command.Item onSelect={() => select("/goo-certificates")}>
            Go to GoO Transactions
          </Command.Item>
          <Command.Item onSelect={() => select("/market-prices")}>
            Go to Market Prices
          </Command.Item>
          <Command.Item onSelect={() => select("/tax-tariffs")}>
            Go to Taxes and Tariffs
          </Command.Item>
          <Command.Item onSelect={() => select("/hedges")}>
            Go to Hedges
          </Command.Item>
          <Command.Item onSelect={() => select("/dashboard")}>
            Go to Dashboard
          </Command.Item>
          <Command.Item onSelect={() => select('/workflow')}>
            Go to Workflow
          </Command.Item>
        </Command.Group>
        <Command.Group heading="Create">
          <Command.Item onSelect={createDelivery}>
            Create New Physical Delivery
          </Command.Item>
        </Command.Group>
      </Command.List>
    </Command.Dialog>
  );
}
