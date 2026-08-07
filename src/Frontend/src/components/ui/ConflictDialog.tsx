export interface ConflictDialogProps { entityId: string; serverState?: unknown; attemptedChanges: object; onClose: () => void; }
export function ConflictDialog({ entityId, serverState, attemptedChanges, onClose }: ConflictDialogProps) {
  return <section data-testid="conflict-prompt" role="dialog" aria-modal="true" aria-label="Version conflict"><h2>Version conflict</h2><p>This record changed on the server. Review it and re-apply selected fields manually.</p><dl><dt>Record</dt><dd>{entityId}</dd><dt>Server state</dt><dd><pre>{JSON.stringify(serverState, null, 2)}</pre></dd><dt>Your attempted edit</dt><dd><pre>{JSON.stringify(attemptedChanges, null, 2)}</pre></dd></dl><button type="button" onClick={onClose}>Close</button></section>;
}
