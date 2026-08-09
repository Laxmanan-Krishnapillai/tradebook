import { Button as BaseButton } from '@base-ui-components/react/button';
import type { ButtonHTMLAttributes } from 'react';

export function Button({ className, ...props }: ButtonHTMLAttributes<HTMLButtonElement>) {
  return <BaseButton nativeButton className={className ? `ui-button ${className}` : 'ui-button'} {...props} />;
}
