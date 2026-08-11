import { Check, Pencil, X } from 'lucide-react';
import { AnimatePresence, m, useReducedMotion } from 'motion/react';
import { useEffect, useRef, useState } from 'react';
import { Input } from './input';
import { NumberInput } from './number-input';
import { Select } from './select';

interface TableEditableCellProps {
  kind?: 'date' | 'number' | 'text';
  label: string;
  onCommit?: (value: string) => Promise<void> | void;
  options?: readonly string[];
  readOnly?: boolean;
  value: string;
}

export function TableEditableCell({ kind = 'text', label, onCommit, options, readOnly, value }: TableEditableCellProps) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(value);
  const [pending, setPending] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const reduceMotion = useReducedMotion();

  useEffect(() => {
    if (editing && !options && kind !== 'number') inputRef.current?.focus();
  }, [editing, kind, options]);

  const cancel = () => {
    setDraft(value);
    setEditing(false);
  };
  const commit = async (next = draft) => {
    if (!onCommit || next === value) {
      setEditing(false);
      return;
    }
    setPending(true);
    try {
      await onCommit(next);
      setEditing(false);
    } finally {
      setPending(false);
    }
  };

  if (readOnly || !onCommit) {
    return <span data-readonly="true" data-slot="table-readonly-cell" title={`${label} is read-only`}>{value || '—'}</span>;
  }

  return (
    <div data-slot="table-editable-cell">
      <AnimatePresence initial={false} mode="wait">
        {!editing ? (
          <m.button
            animate={{ opacity: 1, scale: 1 }}
            aria-label={`Edit ${label}`}
            className="ui-table-edit-trigger"
            exit={reduceMotion ? undefined : { opacity: 0, scale: 0.97 }}
            initial={reduceMotion ? false : { opacity: 0, scale: 0.97 }}
            key="display"
            onClick={(event) => { event.stopPropagation(); setDraft(value); setEditing(true); }}
            type="button"
          >
            <span>{value || '—'}</span><Pencil aria-hidden="true" size={12} />
          </m.button>
        ) : (
          <m.div
            animate={{ opacity: 1, scale: 1 }}
            className="ui-table-edit-control"
            exit={reduceMotion ? undefined : { opacity: 0, scale: 0.98 }}
            initial={reduceMotion ? false : { opacity: 0, scale: 0.98 }}
            key="editor"
            onKeyDown={(event) => {
              if (event.key === 'Escape') cancel();
              if (event.key === 'Enter' && !options) void commit();
            }}
          >
            {options ? (
              <Select label={label} options={options} value={draft} onValueChange={(next) => { setDraft(next); void commit(next); }} />
            ) : kind === 'number' ? (
              <NumberInput aria-label={label} onValueChange={setDraft} value={draft} />
            ) : (
              <Input aria-label={label} ref={inputRef} onChange={(event) => setDraft(event.target.value)} type={kind} value={draft} />
            )}
            {!options && (
              <span data-slot="table-edit-actions">
                <button aria-label={`Save ${label}`} disabled={pending} onClick={(event) => { event.stopPropagation(); void commit(); }} type="button"><Check aria-hidden="true" size={13} /></button>
                <button aria-label={`Cancel ${label}`} disabled={pending} onClick={(event) => { event.stopPropagation(); cancel(); }} type="button"><X aria-hidden="true" size={13} /></button>
              </span>
            )}
          </m.div>
        )}
      </AnimatePresence>
    </div>
  );
}
