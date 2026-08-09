import type { QueryKey } from '@tanstack/react-query';
import type { JsonQueryAst } from '../../types/semanticAst';

type ListFilterValue = string | number | boolean | null | readonly (string | number | boolean)[];

export interface ListQueryRequest {
  page?: number;
  pageSize?: number;
  sort?: readonly { member: string; direction: 'asc' | 'desc' }[];
  filters?: Readonly<Record<string, ListFilterValue | undefined>>;
}

export interface NormalizedListQueryRequest {
  page: number;
  pageSize: number;
  sort: readonly { member: string; direction: 'asc' | 'desc' }[];
  filters: readonly { member: string; value: ListFilterValue }[];
}

function normalizeFilterValue(value: ListFilterValue): ListFilterValue {
  if (!Array.isArray(value)) return value;
  return [...(value as readonly (string | number | boolean)[])]
    .sort((left, right) => String(left).localeCompare(String(right)));
}

export function normalizeListRequest(request: ListQueryRequest = {}): NormalizedListQueryRequest {
  const page = Number.isSafeInteger(request.page) && (request.page ?? 0) > 0 ? request.page! : 1;
  const pageSize = Number.isSafeInteger(request.pageSize) && (request.pageSize ?? 0) > 0 ? request.pageSize! : 100;
  const sort = (request.sort ?? [])
    .map((item) => ({ member: item.member.trim(), direction: item.direction }))
    .filter((item) => item.member.length > 0);
  const filters = Object.entries(request.filters ?? {})
    .filter((entry): entry is [string, ListFilterValue] => entry[1] !== undefined)
    .map(([member, value]) => ({ member: member.trim(), value: normalizeFilterValue(value) }))
    .filter((item) => item.member.length > 0)
    .sort((left, right) => left.member.localeCompare(right.member));
  return { page, pageSize, sort, filters };
}

function entityKeys<const TRoot extends string>(root: TRoot) {
  const all = [root] as const;
  return {
    all,
    lists: () => [...all, 'list'] as const,
    list: (request: ListQueryRequest = {}) => listQueryKey(all, request),
    details: () => [...all, 'detail'] as const,
    detail: (id: string) => [...all, 'detail', id] as const,
  } as const;
}

export function listQueryKey<const TRoot extends readonly unknown[]>(root: TRoot, request: ListQueryRequest = {}) {
  return [...root, 'list', normalizeListRequest(request)] as const;
}

export const queryKeys = {
  deliveries: entityKeys('deliveries'),
  contracts: entityKeys('contracts'),
  capacityBookings: entityKeys('capacity-bookings'),
  transfers: entityKeys('transfers'),
  biotickets: entityKeys('biotickets'),
  gooCertificates: entityKeys('goo-certificates'),
  marketPrices: entityKeys('market-prices'),
  taxTariffs: entityKeys('tax-tariffs'),
  hedges: entityKeys('hedges'),
  dashboards: entityKeys('dashboards'),
  analytics: {
    all: ['analytics'] as const,
    result: (widgetId: string, ast: JsonQueryAst) => ['analytics', widgetId, ast] as const,
  },
} as const;

export function isFirstListPage(queryKey: QueryKey, root: readonly unknown[]): boolean {
  if (queryKey.length === root.length) return true;
  if (queryKey[root.length] === 'list') {
    const request = queryKey[root.length + 1];
    return typeof request === 'object' && request !== null && 'page' in request
      ? (request as { page?: unknown }).page === 1
      : false;
  }
  return queryKey[root.length] === 1;
}

export function isUnfilteredList(queryKey: QueryKey, root: readonly unknown[]): boolean {
  if (queryKey.length === root.length || typeof queryKey[root.length] === 'number') return true;
  if (queryKey[root.length] !== 'list') return false;
  const request = queryKey[root.length + 1];
  return typeof request === 'object' && request !== null && 'filters' in request
    && Array.isArray((request as { filters?: unknown }).filters)
    && (request as { filters: unknown[] }).filters.length === 0;
}
