import { Input as InputPrimitive } from '@base-ui/react/input';
import { m, useReducedMotion } from 'motion/react';
import { useState, type RefAttributes } from 'react';

export type InputProps = Omit<InputPrimitive.Props & RefAttributes<HTMLInputElement>, 'size'> & {
  size?: 'sm' | 'default' | 'lg' | number;
};

const focusSpring = { damping: 28, mass: 0.5, stiffness: 420, type: 'spring' } as const;

/** coss/Base UI input with SmoothUI-style reduced-motion-aware focus micro-interaction. */
export function Input({ className, size = 'default', type, ...props }: InputProps) {
  const [focused, setFocused] = useState(false);
  const reduceMotion = useReducedMotion();

  return (
    <m.span
      animate={focused && !reduceMotion ? { scale: 1.003, y: -1 } : { scale: 1, y: 0 }}
      className="ui-input-control"
      data-input-type={type ?? 'text'}
      data-size={size}
      data-slot="input-control"
      onBlurCapture={() => setFocused(false)}
      onFocusCapture={() => setFocused(true)}
      transition={reduceMotion ? { duration: 0 } : focusSpring}
    >
      <InputPrimitive
        className={['ui-input', className].filter(Boolean).join(' ')}
        data-slot="input"
        size={typeof size === 'number' ? size : undefined}
        type={type}
        {...props}
      />
    </m.span>
  );
}
