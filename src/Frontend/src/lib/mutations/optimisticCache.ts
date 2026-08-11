import type { QueryClient, QueryKey } from '@tanstack/react-query';

export interface VersionedEntity {
  version: number;
}

export interface PagedEntityCache<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  hasNextPage: boolean;
}

export type EntitySnapshot<T> = [QueryKey, PagedEntityCache<T> | T | undefined];

export function isPagedEntityCache<T>(value: unknown): value is PagedEntityCache<T> {
  return typeof value === 'object' && value !== null && Array.isArray((value as { items?: unknown }).items);
}

function rollbackFields<T extends VersionedEntity>(current: T, previous: T, patch: object): T {
  if (current.version > previous.version) return current;
  const currentRecord = current as unknown as Record<string, unknown>;
  const previousRecord = previous as unknown as Record<string, unknown>;
  const patchRecord = patch as Record<string, unknown>;
  const next = { ...current } as T;
  const nextRecord = next as unknown as Record<string, unknown>;
  let changed = false;
  for (const [key, optimisticValue] of Object.entries(patchRecord)) {
    if (!Object.is(currentRecord[key], optimisticValue)) continue;
    nextRecord[key] = previousRecord[key];
    changed = true;
  }
  return changed ? next : current;
}

export function rollbackEntityUpdate<T extends VersionedEntity>(
  queryClient: QueryClient,
  snapshots: EntitySnapshot<T>[],
  id: string,
  idOf: (entity: T) => string,
  patch: object,
): void {
  for (const [queryKey, snapshot] of snapshots) {
    if (isPagedEntityCache<T>(snapshot)) {
      const previous = snapshot.items.find((entity) => idOf(entity) === id);
      if (!previous) continue;
      queryClient.setQueryData<PagedEntityCache<T>>(queryKey, (current) => current ? {
        ...current,
        items: current.items.map((entity) => idOf(entity) === id
          ? rollbackFields(entity, previous, patch)
          : entity),
      } : current);
      continue;
    }
    if (!snapshot || idOf(snapshot) !== id) continue;
    queryClient.setQueryData<T>(queryKey, (current) => current
      ? rollbackFields(current, snapshot, patch)
      : current);
  }
}

export function rollbackEntityDelete<T extends VersionedEntity>(
  queryClient: QueryClient,
  snapshots: EntitySnapshot<T>[],
  id: string,
  idOf: (entity: T) => string,
  optimisticPatch?: Partial<T>,
): void {
  if (optimisticPatch) {
    rollbackEntityUpdate(queryClient, snapshots, id, idOf, optimisticPatch);
    return;
  }
  for (const [queryKey, snapshot] of snapshots) {
    if (isPagedEntityCache<T>(snapshot)) {
      const previousIndex = snapshot.items.findIndex((entity) => idOf(entity) === id);
      queryClient.setQueryData<PagedEntityCache<T>>(queryKey, (current) => {
        if (!current) return current;
        if (previousIndex < 0) {
          if (current.totalCount !== snapshot.totalCount - 1) return current;
          return {
            ...current,
            totalCount: snapshot.totalCount,
            hasNextPage: snapshot.totalCount > current.page * current.pageSize,
          };
        }
        if (current.items.some((entity) => idOf(entity) === id)) return current;
        const items = [...current.items];
        items.splice(Math.min(previousIndex, items.length), 0, snapshot.items[previousIndex]);
        const totalCount = current.totalCount + 1;
        return { ...current, items, totalCount, hasNextPage: totalCount > current.page * current.pageSize };
      });
      continue;
    }
    if (!snapshot || idOf(snapshot) !== id) continue;
    queryClient.setQueryData<T>(queryKey, (current) => current ?? snapshot);
  }
}

export function rollbackOptimisticCreate<T>(
  queryClient: QueryClient,
  queryKeys: QueryKey[],
  optimisticId: string,
  idOf: (entity: T) => string,
  seededKeys: QueryKey[] = [],
  previousTotals: ReadonlyMap<string, number | undefined> = new Map(),
): void {
  const seeded = new Set(seededKeys.map((queryKey) => JSON.stringify(queryKey)));
  for (const queryKey of queryKeys) {
    const current = queryClient.getQueryData<PagedEntityCache<T>>(queryKey);
    if (!current) continue;
    const cacheKey = JSON.stringify(queryKey);
    const contained = current.items.some((entity) => idOf(entity) === optimisticId);
    const previousTotal = previousTotals.get(cacheKey);
    if (!contained && (previousTotal === undefined || current.totalCount !== previousTotal + 1)) continue;
    const items = current.items.filter((entity) => idOf(entity) !== optimisticId);
    const totalCount = contained
      ? Math.max(0, current.totalCount - 1)
      : (previousTotal ?? current.totalCount);
    if (seeded.has(cacheKey) && contained && totalCount === 0 && items.length === 0) {
      queryClient.removeQueries({ queryKey, exact: true });
      continue;
    }
    queryClient.setQueryData<PagedEntityCache<T>>(queryKey, {
      ...current,
      items,
      totalCount,
      hasNextPage: totalCount > current.page * current.pageSize,
    });
  }
}
