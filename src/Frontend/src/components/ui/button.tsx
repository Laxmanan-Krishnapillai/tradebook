import { Button as BaseButton } from '@base-ui/react/button';
import type { ButtonHTMLAttributes } from 'react';

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  intent?: 'primary' | 'secondary' | 'ghost' | 'danger';
  size?: 'sm' | 'md' | 'icon';
}

export function Button({ className, intent = 'primary', size = 'md', ...props }: ButtonProps) {
  const classes = ['ui-button', `ui-button-${intent}`, `ui-button-${size}`, className]
    .filter(Boolean)
    .join(' ');
  return <BaseButton nativeButton className={classes} {...props} />;
}
