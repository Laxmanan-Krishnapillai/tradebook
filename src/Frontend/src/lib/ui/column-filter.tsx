import { Input } from '@base-ui/react/input';
import { Popover } from '@base-ui/react/popover';
import { Filter, X } from 'lucide-react';
import { m, useReducedMotion } from 'motion/react';

interface ColumnFilterProps {
  label: string;
  onChange: (value: string) => void;
  value: string;
}

export function ColumnFilter({ label, onChange, value }: ColumnFilterProps) {
  const reduceMotion = useReducedMotion();
  return (
    <Popover.Root>
      <Popover.Trigger
        aria-label={`Filter ${label}`}
        className="ui-column-filter-trigger"
        data-active={value ? 'true' : undefined}
      >
        <Filter aria-hidden="true" size={12} />
      </Popover.Trigger>
      <Popover.Portal>
        <Popover.Positioner align="center" sideOffset={6}>
          <Popover.Popup
            className="ui-column-filter-popup"
            render={<m.div initial={reduceMotion ? false : { opacity: 0, scale: 0.96, y: -3 }} animate={{ opacity: 1, scale: 1, y: 0 }} />}
          >
            <span>Filter {label}</span>
            <div>
              <Input
                aria-label={`Filter ${label} values`}
                className="ui-input"
                onChange={(event) => onChange(event.target.value)}
                placeholder="Contains…"
                value={value}
              />
              {value && (
                <button aria-label={`Clear ${label} filter`} onClick={() => onChange('')} type="button">
                  <X aria-hidden="true" size={13} />
                </button>
              )}
            </div>
          </Popover.Popup>
        </Popover.Positioner>
      </Popover.Portal>
    </Popover.Root>
  );
}
