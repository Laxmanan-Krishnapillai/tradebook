import { createContext, useContext, type HTMLAttributes, type TableHTMLAttributes, type TdHTMLAttributes } from 'react';
import { usePreferences, type Density } from '../../stores/preferences';

const rowPadding: Record<Density, string> = { condensed: 'py-1', regular: 'py-2', relaxed: 'py-3' };
const DensityContext = createContext<Density>('regular');

export function Table({ className, ...props }: TableHTMLAttributes<HTMLTableElement>) {
  const density = usePreferences((state) => state.density);
  return <DensityContext.Provider value={density}><table className={['w-full border-collapse', className].filter(Boolean).join(' ')} data-density={density} {...props} /></DensityContext.Provider>;
}

export function TableRow(props: HTMLAttributes<HTMLTableRowElement>) {
  return <tr {...props} />;
}

export function TableCell({ className, ...props }: TdHTMLAttributes<HTMLTableCellElement>) {
  const density = useContext(DensityContext);
  return <td className={[rowPadding[density], 'border-b border-border px-3 text-left', className].filter(Boolean).join(' ')} {...props} />;
}
