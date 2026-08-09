import type { ReactNode } from 'react';

export interface KpiTileProps {
  label: string;
  value: string;
  delta?: number;
  spark?: ReactNode;
}

export function KpiTile({ label, value, delta, spark }: KpiTileProps) {
  const increased = (delta ?? 0) >= 0;
  return (
    <article className="kpi-tile">
      <p className="kpi-label">{label}</p>
      <div className="kpi-value-row">
        <strong className="kpi-value">{value}</strong>
        {delta !== undefined && (
          <span className={increased ? 'kpi-delta-up' : 'kpi-delta-down'}>
            <span aria-hidden="true">{increased ? '▲' : '▼'}</span>{' '}
            <span className="sr-only">{increased ? 'Increased' : 'Decreased'} by </span>
            {Math.abs(delta)}%
          </span>
        )}
      </div>
      {spark}
    </article>
  );
}
