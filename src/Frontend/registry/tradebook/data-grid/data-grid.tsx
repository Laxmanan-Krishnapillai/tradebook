export interface DataGridColumn<Row> { key: keyof Row; label: string; numeric?: boolean }
export interface DataGridProps<Row extends { id: string }> {
  caption: string;
  columns: readonly DataGridColumn<Row>[];
  rows: readonly Row[];
}

export function DataGrid<Row extends { id: string }>({ caption, columns, rows }: DataGridProps<Row>) {
  return (
    <table>
      <caption className="sr-only">{caption}</caption>
      <thead><tr>{columns.map((column) => <th key={String(column.key)} scope="col">{column.label}</th>)}</tr></thead>
      <tbody>{rows.map((row) => <tr className="h-7" key={row.id}>{columns.map((column) => <td className={column.numeric ? 'tabular-nums' : undefined} key={String(column.key)}>{String(row[column.key])}</td>)}</tr>)}</tbody>
    </table>
  );
}
