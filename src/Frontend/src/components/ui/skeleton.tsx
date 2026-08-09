import type { HTMLAttributes } from 'react';

export function Skeleton({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return <div aria-hidden="true" className={['animate-pulse rounded-md bg-border', className].filter(Boolean).join(' ')} {...props} />;
}

export function TableSkeleton({ rows = 5, columns = 4 }: { rows?: number; columns?: number }) {
  return (
    <div aria-label="Loading table" className="grid gap-2" role="status">
      {Array.from({ length: rows }, (_, row) => (
        <div className="grid grid-flow-col gap-3" key={row}>
          {Array.from({ length: columns }, (_, column) => <Skeleton className="h-6" key={column} />)}
        </div>
      ))}
    </div>
  );
}
