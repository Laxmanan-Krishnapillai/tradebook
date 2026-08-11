import { NumberFlowInput } from '@daformat/react-number-flow-input';
import { useEffect, useRef } from 'react';

export interface NumberInputProps {
  'aria-describedby'?: string;
  'aria-invalid'?: boolean | 'false' | 'true' | 'grammar' | 'spelling';
  'aria-label': string;
  autoFocus?: boolean;
  className?: string;
  decimalScale?: number;
  focusOnMount?: boolean;
  id?: string;
  max?: number | string;
  maxLength?: number;
  min?: number | string;
  name?: string;
  onBlur?: () => void;
  onFocus?: () => void;
  onValueChange: (value: string) => void;
  placeholder?: string;
  required?: boolean;
  value: number | string | null | undefined;
}

/**
 * Editable animated numeric input from @daformat/react-number-flow-input.
 * It uses an animated contenteditable surface rather than a native number input,
 * while preserving the exact decimal string for the API/Zod validation boundary.
 */
export function NumberInput({
  'aria-describedby': ariaDescribedBy,
  'aria-invalid': ariaInvalid,
  'aria-label': ariaLabel,
  className,
  focusOnMount,
  max,
  min,
  onBlur,
  onFocus,
  onValueChange,
  value,
  ...props
}: NumberInputProps) {
  const editorRef = useRef<HTMLElement>(null);

  useEffect(() => {
    const editor = editorRef.current;
    if (!editor) return;
    editor.closest('[data-numberflow-input-root]')
      ?.querySelector('[data-numberflow-input-real-input]')
      ?.setAttribute('aria-hidden', 'true');
    editor.setAttribute('aria-label', ariaLabel);
    if (ariaDescribedBy) editor.setAttribute('aria-describedby', ariaDescribedBy);
    else editor.removeAttribute('aria-describedby');
    if (ariaInvalid !== undefined) editor.setAttribute('aria-invalid', String(ariaInvalid));
    else editor.removeAttribute('aria-invalid');
  }, [ariaDescribedBy, ariaInvalid, ariaLabel]);

  useEffect(() => {
    if (focusOnMount) editorRef.current?.focus({ preventScroll: true });
  }, [focusOnMount]);

  const numericMin = min === undefined ? undefined : Number(min);
  const numericMax = max === undefined ? undefined : Number(max);

  return (
    <NumberFlowInput
      {...props}
      ref={editorRef}
      className={['ui-number-input', className].filter(Boolean).join(' ')}
      min={numericMin}
      max={numericMax}
      value={value ?? ''}
      onBlur={onBlur}
      onFocus={onFocus}
      onChangeText={onValueChange}
      autoAddLeadingZero
      animateOnValueChange
    />
  );
}
