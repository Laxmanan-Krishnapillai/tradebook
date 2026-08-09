export interface ComboboxOption { label: string; value: string }
export interface ComboboxProps {
  label: string;
  options: readonly ComboboxOption[];
  value?: string;
  onChange?: (value: string) => void;
}

export function Combobox({ label, onChange, options, value }: ComboboxProps) {
  return (
    <label>
      {label}
      <select onChange={(event) => onChange?.(event.currentTarget.value)} value={value}>
        {options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>
    </label>
  );
}
