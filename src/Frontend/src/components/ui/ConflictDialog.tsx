import { AlertTriangle, X } from 'lucide-react';
import { Button } from './button';

export interface ConflictDialogProps { entityId: string; serverState?: unknown; attemptedChanges: object; onClose: () => void; }
export function ConflictDialog({ entityId, serverState, attemptedChanges, onClose }: ConflictDialogProps) {
  return (
    <section data-slot="conflict-dialog" data-testid="conflict-prompt" role="dialog" aria-modal="true" aria-label="Version conflict">
      <header>
        <span data-slot="conflict-dialog-icon"><AlertTriangle aria-hidden="true" size={16} /></span>
        <div><h2>Version conflict</h2><p>This record changed on the server. Review both versions and re-apply selected fields manually.</p></div>
        <Button aria-label="Close" intent="ghost" size="icon" type="button" onClick={onClose}><X aria-hidden="true" size={16} /></Button>
      </header>
      <dl>
        <div><dt>Record</dt><dd className="font-mono">{entityId}</dd></div>
        <div><dt>Server state</dt><dd><pre>{JSON.stringify(serverState, null, 2)}</pre></dd></div>
        <div><dt>Your attempted edit</dt><dd><pre>{JSON.stringify(attemptedChanges, null, 2)}</pre></dd></div>
      </dl>
      <footer><Button intent="secondary" type="button" onClick={onClose}>Close</Button></footer>
    </section>
  );
}
