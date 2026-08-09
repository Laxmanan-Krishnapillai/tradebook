import NumberFlow from '@number-flow/react';
import type { Format } from '@number-flow/react';

interface NumericCellProps {
  value: number;
  format?: Format;
  animate?: boolean;
  flashOnChange?: boolean;
  className?: string;
}

export function NumericCell({ value, format, animate = false, flashOnChange = false, className }: NumericCellProps) {
  const classes = ['text-right tabular-nums slashed-zero lining-nums', flashOnChange && 'transition-colors duration-fast', className]
    .filter(Boolean)
    .join(' ');

  return <NumberFlow className={classes} value={value} format={format} animated={animate} respectMotionPreference />;
}
