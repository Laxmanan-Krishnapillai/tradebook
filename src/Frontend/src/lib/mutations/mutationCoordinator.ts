import type { QueryKey } from '@tanstack/react-query';

const tails = new Map<string, Promise<void>>();
const confirmedVersions = new Map<string, number>();
const conflicts = new Map<string, { promise: Promise<void>; resolve: () => void }>();

function aggregateScope(queryKey: QueryKey): string {
  return JSON.stringify(queryKey.slice(0, 1));
}

export function mutationScopeKey(
  queryKey: QueryKey,
  sessionIdentity: string | undefined,
  entityId: string,
): string {
  return `${sessionIdentity ?? 'anonymous'}\u0000${aggregateScope(queryKey)}\u0000${entityId}`;
}

export async function acquireMutationScope(scopeKey: string): Promise<() => void> {
  const previous = tails.get(scopeKey) ?? Promise.resolve();
  let resolveCurrent!: () => void;
  const current = new Promise<void>((resolve) => { resolveCurrent = resolve; });
  const tail = previous.catch(() => undefined).then(() => current);
  tails.set(scopeKey, tail);
  await previous.catch(() => undefined);

  let released = false;
  return () => {
    if (released) return;
    released = true;
    resolveCurrent();
    if (tails.get(scopeKey) === tail) {
      void tail.finally(() => {
        if (tails.get(scopeKey) === tail) tails.delete(scopeKey);
      });
    }
  };
}

export function resolveMutationVersion(scopeKey: string, requestedVersion: number): number {
  return Math.max(requestedVersion, confirmedVersions.get(scopeKey) ?? requestedVersion);
}

export function recordMutationVersion(scopeKey: string, version: number): void {
  const current = confirmedVersions.get(scopeKey);
  if (current === undefined || version > current) confirmedVersions.set(scopeKey, version);
}

export function markMutationConflict(scopeKey: string): void {
  if (conflicts.has(scopeKey)) return;
  let resolve!: () => void;
  const promise = new Promise<void>((done) => { resolve = done; });
  conflicts.set(scopeKey, { promise, resolve });
}

export async function waitForMutationConflictResolution(scopeKey: string): Promise<void> {
  await conflicts.get(scopeKey)?.promise;
}

export function clearMutationConflictForEntity(entityId: string): void {
  const suffix = `\u0000${entityId}`;
  for (const [scopeKey, conflict] of conflicts) {
    if (!scopeKey.endsWith(suffix)) continue;
    conflicts.delete(scopeKey);
    conflict.resolve();
  }
}

export async function withMutationScope<T>(scopeKey: string, operation: () => Promise<T>): Promise<T> {
  const release = await acquireMutationScope(scopeKey);
  try {
    return await operation();
  } finally {
    release();
  }
}

export function clearMutationVersions(): void {
  confirmedVersions.clear();
  for (const conflict of conflicts.values()) conflict.resolve();
  conflicts.clear();
}
