import { Select as BaseSelect } from '@base-ui/react/select';
import { Check, ChevronDown } from 'lucide-react';

export interface SelectProps {
  disabled?: boolean;
  label: string;
  options: readonly SelectOptionValue[];
  value: string;
  onValueChange: (value: string) => void;
}

interface MultiSelectProps {
  disabled?: boolean;
  label: string;
  options: readonly SelectOptionValue[];
  value: string[];
  onValueChange: (value: string[]) => void;
}

export interface SelectOption {
  label: string;
  value: string;
}

type SelectOptionValue = string | SelectOption;

function normalizedOptions(options: readonly SelectOptionValue[]): SelectOption[] {
  return options.map((option) => typeof option === 'string' ? { label: option, value: option } : option);
}

function SelectPopup({ options }: { options: readonly SelectOptionValue[] }) {
  const items = normalizedOptions(options);
  return (
    <BaseSelect.Portal>
      <BaseSelect.Positioner align="start" alignItemWithTrigger={false} className="ui-select-positioner" sideOffset={4}>
        <BaseSelect.Popup className="ui-select-popup">
          <BaseSelect.List>
            {items.map((option) => (
              <BaseSelect.Item key={option.value} value={option.value} className="ui-select-item">
                <BaseSelect.ItemIndicator><Check aria-hidden="true" size={14} /></BaseSelect.ItemIndicator>
                <BaseSelect.ItemText>{option.label}</BaseSelect.ItemText>
              </BaseSelect.Item>
            ))}
          </BaseSelect.List>
        </BaseSelect.Popup>
      </BaseSelect.Positioner>
    </BaseSelect.Portal>
  );
}

export function Select({ disabled, label, options, value, onValueChange }: SelectProps) {
  const items = normalizedOptions(options);
  return (
    <BaseSelect.Root disabled={disabled} value={value} onValueChange={(next) => onValueChange(next ?? '')}>
      <BaseSelect.Trigger aria-label={label} className="ui-select-trigger">
        <BaseSelect.Value>{(selected: string) => items.find((item) => item.value === selected)?.label ?? selected}</BaseSelect.Value>
        <BaseSelect.Icon><ChevronDown aria-hidden="true" size={14} /></BaseSelect.Icon>
      </BaseSelect.Trigger>
      <SelectPopup options={options} />
    </BaseSelect.Root>
  );
}

export function MultiSelect({ disabled, label, options, value, onValueChange }: MultiSelectProps) {
  const items = normalizedOptions(options);
  return (
    <BaseSelect.Root disabled={disabled} multiple value={value} onValueChange={onValueChange}>
      <BaseSelect.Trigger aria-label={label} className="ui-select-trigger">
        <BaseSelect.Value>
          {(selected: string[]) => selected.length === 0
            ? `Select ${label.toLowerCase()}`
            : `${items.find((item) => item.value === selected[0])?.label ?? selected[0]}${selected.length > 1 ? ` (+${selected.length - 1})` : ''}`}
        </BaseSelect.Value>
        <BaseSelect.Icon><ChevronDown aria-hidden="true" size={14} /></BaseSelect.Icon>
      </BaseSelect.Trigger>
      <SelectPopup options={options} />
    </BaseSelect.Root>
  );
}
