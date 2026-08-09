import type { ReactNode } from 'react';
import { Inbox } from 'lucide-react';

export function EmptyState({ title, description, action }: { title: string; description: string; action?: ReactNode }) {
  return (
    <section className="grid justify-items-center gap-3 rounded-lg border border-border bg-background p-8 text-center">
      <Inbox aria-hidden="true" size={20} strokeWidth={1.5} />
      <div><h2 className="text-lg font-semibold">{title}</h2><p className="text-sm text-muted-foreground">{description}</p></div>
      {action}
    </section>
  );
}
