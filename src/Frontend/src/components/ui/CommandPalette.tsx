import { useNavigate } from '@tanstack/react-router';
import { Command } from "cmdk";
import { useEffect, useState } from "react";
import { isEditableTarget } from "../../lib/dom/keyboard";
import type { AuthenticatedRoutePath } from '../../lib/session/sessionController';
import { useUiStore } from '../../lib/state/useUiStore';

const shortcutRoutes: Readonly<Partial<Record<string, AuthenticatedRoutePath>>> = {
  d: '/deliveries',
  c: '/contracts',
  m: '/market-prices',
  b: '/capacity-bookings',
  t: '/transfers',
  h: '/hedges',
  w: '/workflow',
};

export function CommandPalette() {
  const navigate = useNavigate();
  const openModal = useUiStore((state) => state.openModal);
  const [open, setOpen] = useState(false);
  const [prefix, setPrefix] = useState(false);
  useEffect(() => {
    if (!prefix) return;
    const timeout = window.setTimeout(() => setPrefix(false), 750);
    return () => window.clearTimeout(timeout);
  }, [prefix]);
  useEffect(() => {
    const listener = (event: KeyboardEvent) => {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setOpen((value) => !value);
        return;
      }
      if (isEditableTarget(event.target)) {
        setPrefix(false);
        return;
      }
      if (event.key === "Escape") {
        setPrefix(false);
        return;
      }
      if (prefix) {
        const path = shortcutRoutes[event.key.toLowerCase()];
        setPrefix(false);
        if (path) {
          event.preventDefault();
          void navigate({ to: path });
        }
        return;
      }
      if (
        !event.metaKey &&
        !event.ctrlKey &&
        !event.altKey &&
        event.key.toLowerCase() === "g"
      )
        setPrefix(true);
    };
    document.addEventListener("keydown", listener);
    return () => document.removeEventListener("keydown", listener);
  }, [navigate, prefix]);
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
