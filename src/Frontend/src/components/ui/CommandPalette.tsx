import { Command } from 'cmdk';
import { useEffect, useState } from 'react';
export function CommandPalette({ onNavigate }: { onNavigate: (path: string) => void }) {
  const [open, setOpen] = useState(false); const [prefix, setPrefix] = useState(false);
  useEffect(() => { const listener = (event: KeyboardEvent) => { if ((event.metaKey || event.ctrlKey) && event.key === 'k') { event.preventDefault(); setOpen((value) => !value); return; } if (prefix) { const path = ({ d: '/deliveries', c: '/contracts', m: '/market-prices' } as Record<string, string>)[event.key]; setPrefix(false); if (path) onNavigate(path); return; } if (event.key === 'g') setPrefix(true); }; document.addEventListener('keydown', listener); return () => document.removeEventListener('keydown', listener); }, [onNavigate, prefix]);
  const select = (path: string) => { onNavigate(path); setOpen(false); };
  return <Command.Dialog open={open} onOpenChange={setOpen} label="Command palette"><Command.Input placeholder="Type a command or search..." /><Command.List><Command.Empty>No results.</Command.Empty><Command.Group heading="Navigate"><Command.Item onSelect={() => select('/deliveries')}>Go to Deliveries</Command.Item><Command.Item onSelect={() => select('/contracts')}>Go to Contracts</Command.Item><Command.Item onSelect={() => select('/market-prices')}>Go to Market Prices</Command.Item></Command.Group></Command.List></Command.Dialog>;
}
