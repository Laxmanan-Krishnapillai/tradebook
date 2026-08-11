import { Skeleton, TableSkeleton } from '../ui/skeleton';

interface ListRoutePendingProps {
  columns: number;
  label: string;
}

export function ListRoutePending({ columns, label }: ListRoutePendingProps) {
  return (
    <section aria-busy="true" aria-label={`Loading ${label}`} data-slot="list-route-pending">
      <header>
        <div className="grid gap-2">
          <Skeleton className="h-3 w-20" />
          <Skeleton className="h-5 w-40" />
        </div>
        <Skeleton className="h-8 w-28" />
      </header>
      <div aria-hidden="true" className="toolbar flex items-center justify-between gap-4">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-3 w-24" />
      </div>
      <div className="p-5">
        <TableSkeleton columns={columns} rows={8} />
      </div>
    </section>
  );
}
