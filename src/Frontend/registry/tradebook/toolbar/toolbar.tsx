import type { HTMLAttributes, ReactNode } from 'react';

export interface ToolbarProps extends HTMLAttributes<HTMLDivElement> {
  label: string;
  children: ReactNode;
}

export function Toolbar({ children, label, ...props }: ToolbarProps) {
  return (
    <div aria-label={label} className="flex items-center gap-2" role="toolbar" {...props}>
      {children}
    </div>
  );
}
