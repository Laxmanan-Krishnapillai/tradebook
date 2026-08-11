import { useSelector } from '@xstate/react';
import { useContext, useEffect, useId, useRef, useState } from 'react';
import { GridInteractionActorContext, type GridInteractionActor } from '../../lib/grid/gridInteractionMachine';
import { Input } from './input';
import { Select } from './select';

interface TableEditableCellProps {
  kind?: 'date' | 'number' | 'text';
  label: string;
  onCommit?: (value: string) => Promise<void> | void;
  options?: readonly string[];
  readOnly?: boolean;
  value: string;
}

interface EditableCellProps extends Omit<TableEditableCellProps, 'onCommit' | 'readOnly'> {
  editing: boolean;
  onCommit: NonNullable<TableEditableCellProps['onCommit']>;
  startEditing: () => void;
  stopEditing: () => void;
}

function EditableCell({ editing, kind = 'text', label, onCommit, options, startEditing, stopEditing, value }: EditableCellProps) {
  const [draft, setDraft] = useState(value);
  const [pending, setPending] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!editing || options) return;
    inputRef.current?.focus({ preventScroll: true });
    if (kind !== 'date') inputRef.current?.select();
  }, [editing, kind, options]);

  useEffect(() => {
    if (!editing && !pending) setDraft(value);
  }, [editing, pending, value]);

  const cancel = () => {
    setDraft(value);
    stopEditing();
  };
  const commit = async (next = draft) => {
    if (next === value) {
      stopEditing();
      return;
    }
    setPending(true);
    stopEditing();
    try {
      await onCommit(next);
    } finally {
      setPending(false);
    }
  };

  return (
    <div
      aria-busy={pending}
      data-editing={editing ? 'true' : undefined}
      data-grid-interactive="true"
      data-pending={pending ? 'true' : undefined}
      data-slot="table-editable-cell"
    >
      {options ? (
        <Select
          appearance="cell"
          disabled={pending}
          label={label}
          modal={false}
          options={options}
          value={draft}
          onOpenChange={(open, reason) => {
            if (open) {
              setDraft(value);
              startEditing();
            } else if (reason === 'outside-press' || reason === 'focus-out') {
              window.setTimeout(stopEditing, 0);
            } else {
              stopEditing();
            }
          }}
          onValueChange={(next) => {
            setDraft(next);
            void commit(next);
          }}
        />
      ) : (
        <Input
          appearance="cell"
          aria-label={label}
          inputMode={kind === 'number' ? 'decimal' : undefined}
          onBlur={() => {
            if (editing) void commit();
          }}
          onChange={(event) => setDraft(event.target.value)}
          onClick={(event) => {
            event.stopPropagation();
            if (!editing) {
              setDraft(value);
              startEditing();
            }
          }}
          onFocus={() => {
            if (!editing) {
              setDraft(value);
              startEditing();
            }
          }}
          onKeyDown={(event) => {
            event.stopPropagation();
            if (event.key === 'Escape') {
              event.preventDefault();
              cancel();
            } else if (event.key === 'Enter') {
              event.preventDefault();
              void commit();
            }
          }}
          readOnly={!editing || pending}
          ref={inputRef}
          type={kind === 'number' ? 'text' : kind}
          value={draft}
        />
      )}
    </div>
  );
}

function LocalEditableCell(props: Omit<EditableCellProps, 'editing' | 'startEditing' | 'stopEditing'>) {
  const [editing, setEditing] = useState(false);
  return <EditableCell {...props} editing={editing} startEditing={() => setEditing(true)} stopEditing={() => setEditing(false)} />;
}

function CoordinatedEditableCell({ actor, ...props }: Omit<EditableCellProps, 'editing' | 'startEditing' | 'stopEditing'> & { actor: GridInteractionActor }) {
  const cellId = useId();
  const editing = useSelector(actor, (snapshot) => snapshot.matches('editing')
    && snapshot.context.activeCellId === cellId);

  useEffect(() => () => actor.send({ type: 'cell.close', cellId }), [actor, cellId]);

  return <EditableCell
    {...props}
    editing={editing}
    startEditing={() => actor.send({ type: 'cell.edit', cellId })}
    stopEditing={() => actor.send({ type: 'cell.close', cellId })}
  />;
}

export function TableEditableCell({ kind = 'text', label, onCommit, options, readOnly, value }: TableEditableCellProps) {
  const actor = useContext(GridInteractionActorContext);
  if (readOnly || !onCommit) {
    return <span data-readonly="true" data-slot="table-readonly-cell" title={`${label} is read-only`}>{value || '—'}</span>;
  }
  const props = { kind, label, onCommit, options, value };
  return actor ? <CoordinatedEditableCell actor={actor} {...props} /> : <LocalEditableCell {...props} />;
}
