import { Checkbox as CheckboxPrimitive } from '@base-ui/react/checkbox';
import { AnimatePresence, m, useReducedMotion } from 'motion/react';
import type { ComponentProps } from 'react';

type CheckboxProps = Omit<ComponentProps<typeof CheckboxPrimitive.Root>, 'onCheckedChange'> & {
  onCheckedChange?: (checked: boolean) => void;
};

const spring = { damping: 32, mass: 0.45, stiffness: 500, type: 'spring' } as const;

/** SmoothUI's animated checkbox interaction, adapted onto Tradebook's Base UI primitive. */
export function Checkbox({ checked = false, className, indeterminate = false, onCheckedChange, ...props }: CheckboxProps) {
  const reduceMotion = useReducedMotion();
  const state = indeterminate ? 'indeterminate' : checked ? 'checked' : 'unchecked';
  const transition = reduceMotion ? { duration: 0 } : spring;

  return (
    <CheckboxPrimitive.Root
      checked={checked}
      className={['ui-checkbox', className].filter(Boolean).join(' ')}
      data-slot="checkbox"
      indeterminate={indeterminate}
      onCheckedChange={(next) => onCheckedChange?.(next)}
      {...props}
    >
      <CheckboxPrimitive.Indicator className="ui-checkbox-indicator" data-slot="checkbox-indicator" keepMounted>
        <AnimatePresence initial={false} mode="wait">
          {state === 'checked' && (
            <m.svg
              animate={{ opacity: 1, scale: 1 }}
              aria-hidden="true"
              exit={{ opacity: 0, scale: reduceMotion ? 1 : 0.8 }}
              fill="none"
              initial={{ opacity: reduceMotion ? 1 : 0, scale: reduceMotion ? 1 : 0.8 }}
              key="checked"
              stroke="currentColor"
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth="3"
              transition={transition}
              viewBox="0 0 24 24"
            >
              <m.path
                animate={{ pathLength: 1 }}
                d="M20 6 9 17l-5-5"
                initial={{ pathLength: reduceMotion ? 1 : 0 }}
                transition={reduceMotion ? { duration: 0 } : { ...spring, delay: 0.05 }}
              />
            </m.svg>
          )}
          {state === 'indeterminate' && (
            <m.svg
              animate={{ opacity: 1, scale: 1 }}
              aria-hidden="true"
              exit={{ opacity: 0, scale: reduceMotion ? 1 : 0.8 }}
              fill="none"
              initial={{ opacity: reduceMotion ? 1 : 0, scale: reduceMotion ? 1 : 0.8 }}
              key="indeterminate"
              stroke="currentColor"
              strokeLinecap="round"
              strokeWidth="3"
              transition={transition}
              viewBox="0 0 24 24"
            >
              <m.path
                animate={{ pathLength: 1 }}
                d="M5 12h14"
                initial={{ pathLength: reduceMotion ? 1 : 0 }}
                transition={reduceMotion ? { duration: 0 } : { ...spring, delay: 0.05 }}
              />
            </m.svg>
          )}
        </AnimatePresence>
      </CheckboxPrimitive.Indicator>
    </CheckboxPrimitive.Root>
  );
}
