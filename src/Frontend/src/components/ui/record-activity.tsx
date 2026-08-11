import { useQuery } from '@tanstack/react-query';
import type { ActivityEntryDto, GetActivityResponse } from '../../api/generated/types.gen';
import { apiFetch } from '../../lib/api/client';

interface RecordActivityProps {
  entityId: string;
  entityName: string;
}

interface JsonPatchChange {
  path?: unknown;
  value?: unknown;
}

function fieldLabel(path: string) {
  return path
    .replace(/^\//, '')
    .replaceAll('_', ' ')
    .replace(/\b\w/g, (letter) => letter.toUpperCase());
}

function changeSummary(entry: ActivityEntryDto) {
  if (entry.operation === 'INSERT') return ['Record created'];
  if (entry.operation === 'DELETE') return ['Record deleted'];
  if (!Array.isArray(entry.changes)) return [`Record ${entry.operation.toLowerCase()}`];
  const changes = entry.changes as JsonPatchChange[];
  if (changes.length === 0) return [`Record ${entry.operation.toLowerCase()}`];
  return changes.map((change) => {
    const path = typeof change.path === 'string' ? change.path : '/field';
    const value = change.value === null || change.value === undefined || change.value === ''
      ? 'cleared'
      : String(change.value);
    return `${fieldLabel(path)} changed to ${value}`;
  });
}

export function RecordActivity({ entityId, entityName }: RecordActivityProps) {
  const activity = useQuery({
    queryKey: ['activity', entityName, entityId],
    queryFn: ({ signal }) => apiFetch<GetActivityResponse>(
      `/api/v1/activity/${encodeURIComponent(entityName)}/${encodeURIComponent(entityId)}?pageSize=100`,
      { signal },
    ),
  });

  if (activity.isPending) return <p role="status">Loading activity…</p>;
  if (activity.isError) return <p role="alert">Unable to load activity.</p>;
  if (activity.data.items.length === 0) return <p>No recorded changes yet.</p>;

  return (
    <ol data-slot="record-activity-list">
      {activity.data.items.flatMap((entry) => changeSummary(entry).map((summary, index) => (
        <li key={`${entry.auditId}-${index}`}>
          <div>
            <strong>{summary}</strong>
            <span>{new Date(entry.occurredAt).toLocaleString()} · {entry.actorId ?? 'System'}</span>
          </div>
        </li>
      )))}
    </ol>
  );
}
