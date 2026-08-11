import { Combobox as BaseCombobox } from '@base-ui/react/combobox';
import { Check, ChevronsUpDown, Search } from 'lucide-react';

export interface ComboboxOption {
  description?: string;
  label: string;
  value: string;
}

export interface ComboboxProps {
  disabled?: boolean;
  label: string;
  options: readonly ComboboxOption[];
  placeholder?: string;
  value?: string;
  onChange?: (value: string) => void;
}

export function Combobox({
  disabled,
  label,
  onChange,
  options,
  placeholder = 'Search records…',
  value,
}: ComboboxProps) {
  const selected = options.find((option) => option.value === value) ?? null;

  return (
    <label className="ui-combobox-field">
      <span className="ui-field-label">{label}</span>
      <BaseCombobox.Root
        disabled={disabled}
        itemToStringValue={(option: ComboboxOption) => option.label}
        items={[...options]}
        onValueChange={(option) => onChange?.(option?.value ?? '')}
        value={selected}
      >
        <div className="ui-combobox-control">
          <Search aria-hidden="true" className="ui-combobox-search-icon" size={14} />
          <BaseCombobox.Input aria-label={label} className="ui-combobox-input" placeholder={placeholder} />
          <BaseCombobox.Trigger aria-label={`Show ${label} options`} className="ui-combobox-trigger">
            <ChevronsUpDown aria-hidden="true" size={14} />
          </BaseCombobox.Trigger>
        </div>
        <BaseCombobox.Portal>
          <BaseCombobox.Positioner align="start" className="ui-combobox-positioner" sideOffset={4}>
            <BaseCombobox.Popup className="ui-combobox-popup">
              <BaseCombobox.Empty className="ui-combobox-empty">No matching records.</BaseCombobox.Empty>
              <BaseCombobox.List className="ui-combobox-list">
                {(option: ComboboxOption) => (
                  <BaseCombobox.Item className="ui-combobox-item" key={option.value} value={option}>
                    <BaseCombobox.ItemIndicator className="ui-combobox-indicator">
                      <Check aria-hidden="true" size={14} />
                    </BaseCombobox.ItemIndicator>
                    <span className="ui-combobox-option-copy">
                      <span>{option.label}</span>
                      {option.description ? <small>{option.description}</small> : null}
                    </span>
                  </BaseCombobox.Item>
                )}
              </BaseCombobox.List>
            </BaseCombobox.Popup>
          </BaseCombobox.Positioner>
        </BaseCombobox.Portal>
      </BaseCombobox.Root>
    </label>
  );
}
