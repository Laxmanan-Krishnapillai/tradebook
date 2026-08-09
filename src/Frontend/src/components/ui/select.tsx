import { Select as BaseSelect } from '@base-ui-components/react/select';

export interface SelectProps {
  label: string;
  options: readonly string[];
  value: string;
  onValueChange: (value: string) => void;
}

export function Select({ label, options, value, onValueChange }: SelectProps) {
  return (
    <BaseSelect.Root value={value} onValueChange={(next) => onValueChange(next ?? '')}>
      <BaseSelect.Trigger aria-label={label} className="ui-select-trigger">
        <BaseSelect.Value />
        <BaseSelect.Icon>▾</BaseSelect.Icon>
      </BaseSelect.Trigger>
      <BaseSelect.Portal>
        <BaseSelect.Positioner>
          <BaseSelect.Popup className="ui-select-popup">
            <BaseSelect.List>
              {options.map((option) => (
                <BaseSelect.Item key={option} value={option} className="ui-select-item">
                  <BaseSelect.ItemIndicator>✓</BaseSelect.ItemIndicator>
                  <BaseSelect.ItemText>{option}</BaseSelect.ItemText>
                </BaseSelect.Item>
              ))}
            </BaseSelect.List>
          </BaseSelect.Popup>
        </BaseSelect.Positioner>
      </BaseSelect.Portal>
    </BaseSelect.Root>
  );
}
