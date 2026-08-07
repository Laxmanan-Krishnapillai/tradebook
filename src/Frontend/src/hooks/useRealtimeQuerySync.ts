import { createContext, createElement, useContext, useEffect, useState, type ReactNode } from 'react';
import { type QueryClient, type QueryKey, useQueryClient } from '@tanstack/react-query';
import type { GetDeliveryHistoryResponse } from '../api/generated/get-delivery-history-response';
import type { PhysicalDeliveryDetailsDto } from '../api/generated/physical-delivery-details-dto';
import { ApiError, apiFetch } from '../lib/api/client';
import { queryKeys } from '../lib/query/queryKeys';
import {
  DashboardStreamClient,
  isKnownAggregateType,
  type EntityChangedEvent,
  type KnownAggregateType,
} from '../lib/realtime/signalRClient';
import { EntityEventBatcher } from '../lib/streaming/eventBatcher';

type DeliveryListCache = GetDeliveryHistoryResponse | PhysicalDeliveryDetailsDto[];
type DeliveryLoader = (deliveryId: string, signal?: AbortSignal) => Promise<PhysicalDeliveryDetailsDto>;

interface ReconciliationOptions {
  signal?: AbortSignal;
}

export const affectedQueryRoots: Record<KnownAggregateType, readonly QueryKey[]> = {
  PhysicalDelivery: [queryKeys.deliveries.all, queryKeys.analytics.all],
  Contract: [queryKeys.contracts.all, queryKeys.analytics.all],
  CapacityBooking: [queryKeys.capacityBookings.all, queryKeys.analytics.all],
  Transfer: [queryKeys.transfers.all, queryKeys.analytics.all],
  BioticketDelivery: [queryKeys.biotickets.all, queryKeys.analytics.all],
  GooCertificateTransaction: [queryKeys.gooCertificates.all, queryKeys.analytics.all],
  MarketPrice: [queryKeys.marketPrices.all, queryKeys.analytics.all],
  TaxTariff: [queryKeys.taxTariffs.all, queryKeys.analytics.all],
  Hedge: [queryKeys.hedges.all, queryKeys.analytics.all],
  WorkspaceDashboard: [queryKeys.dashboards.all],
};

function eventVersion(event: EntityChangedEvent): number | undefined {
  try {
    const value = (JSON.parse(event.payloadJson) as { version?: unknown }).version;
    return typeof value === 'number' && Number.isSafeInteger(value) && value >= 0 ? value : undefined;
  } catch {
    return undefined;
  }
}

function rows(cache: DeliveryListCache): PhysicalDeliveryDetailsDto[] {
  return Array.isArray(cache) ? cache : cache.items;
}

function replaceRows(
  cache: DeliveryListCache,
  nextRows: PhysicalDeliveryDetailsDto[],
  totalDelta = 0,
): DeliveryListCache {
  return Array.isArray(cache)
    ? nextRows
    : { ...cache, items: nextRows, totalCount: Math.max(0, cache.totalCount + totalDelta) };
}

function installDelivery(
  queryClient: QueryClient,
  event: EntityChangedEvent,
  delivery: PhysicalDeliveryDetailsDto,
): void {
  queryClient.setQueryData(queryKeys.deliveries.detail(delivery.deliveryId), delivery);
  for (const [queryKey, cache] of queryClient.getQueriesData<DeliveryListCache>({ queryKey: queryKeys.deliveries.lists() })) {
    if (!cache) continue;
    const current = rows(cache);
    const index = current.findIndex((row) => row.deliveryId === event.aggregateId);
    if (index < 0) continue;
    if (current[index].version > delivery.version) continue;
    queryClient.setQueryData(
      queryKey,
      replaceRows(cache, current.map((row, rowIndex) => rowIndex === index ? delivery : row)),
    );
  }
}

function removeDelivery(queryClient: QueryClient, deliveryId: string): void {
  queryClient.removeQueries({ queryKey: queryKeys.deliveries.detail(deliveryId), exact: true });
  for (const [queryKey, cache] of queryClient.getQueriesData<DeliveryListCache>({ queryKey: queryKeys.deliveries.lists() })) {
    if (!cache) continue;
    const current = rows(cache);
    const hasRow = current.some((row) => row.deliveryId === deliveryId);
    if (!hasRow) continue;
    queryClient.setQueryData(
      queryKey,
      replaceRows(cache, current.filter((row) => row.deliveryId !== deliveryId), -1),
    );
  }
}

function addRoot(roots: Map<string, QueryKey>, queryKey: QueryKey): void {
  roots.set(JSON.stringify(queryKey), queryKey);
}

async function reconcileDelivery(
  queryClient: QueryClient,
  event: EntityChangedEvent,
  roots: Map<string, QueryKey>,
  loadDelivery: DeliveryLoader,
  signal?: AbortSignal,
): Promise<void> {
  const version = eventVersion(event);
  const detail = queryClient.getQueryData<PhysicalDeliveryDetailsDto>(
    queryKeys.deliveries.detail(event.aggregateId),
  );
  const cachedRows = queryClient
    .getQueriesData<DeliveryListCache>({ queryKey: queryKeys.deliveries.lists() })
    .flatMap(([, cache]) => cache ? rows(cache) : [])
    .filter((row) => row.deliveryId === event.aggregateId)
    .concat(detail ? [detail] : []);
  if (version !== undefined && cachedRows.length > 0 && cachedRows.every((row) => row.version >= version)) {
    addRoot(roots, queryKeys.deliveries.lists());
    addRoot(roots, queryKeys.analytics.all);
    return;
  }
  try {
    const delivery = await loadDelivery(event.aggregateId, signal);
    if (signal?.aborted) return;
    installDelivery(queryClient, event, delivery);
  } catch (error) {
    if (signal?.aborted) return;
    if (error instanceof ApiError && error.status === 404) removeDelivery(queryClient, event.aggregateId);
    addRoot(roots, queryKeys.deliveries.all);
  }
  addRoot(roots, queryKeys.deliveries.lists());
  addRoot(roots, queryKeys.analytics.all);
}

export async function reconcileRealtimeBatch(
  queryClient: QueryClient,
  events: readonly EntityChangedEvent[],
  loadDelivery: DeliveryLoader = (id, signal) => apiFetch<PhysicalDeliveryDetailsDto>(
    `/api/v1/deliveries/${encodeURIComponent(id)}`,
    { signal },
  ),
  options: ReconciliationOptions = {},
): Promise<void> {
  const roots = new Map<string, QueryKey>();
  for (const event of events) {
    if (options.signal?.aborted) return;
    if (!isKnownAggregateType(event.aggregateType)) continue;
    if (event.aggregateType === 'PhysicalDelivery') {
      await reconcileDelivery(queryClient, event, roots, loadDelivery, options.signal);
      continue;
    }
    for (const queryKey of affectedQueryRoots[event.aggregateType]) addRoot(roots, queryKey);
  }
  if (options.signal?.aborted) return;
  await Promise.all([...roots.values()].map((queryKey) => queryClient.invalidateQueries({ queryKey })));
}

export function reconcileRealtimeEvent(
  queryClient: QueryClient,
  event: EntityChangedEvent,
  loadDelivery?: DeliveryLoader,
): Promise<void> {
  return reconcileRealtimeBatch(queryClient, [event], loadDelivery);
}

const LastRealtimeEventContext = createContext<EntityChangedEvent | undefined>(undefined);

export function RealtimeQuerySyncProvider({
  lastEvent,
  children,
}: {
  lastEvent?: EntityChangedEvent;
  children: ReactNode;
}) {
  return createElement(LastRealtimeEventContext.Provider, { value: lastEvent }, children);
}

export function useLastRealtimeEvent(): EntityChangedEvent | undefined {
  return useContext(LastRealtimeEventContext);
}

export function useRealtimeQuerySync(enabled = true, sessionKey = ''): EntityChangedEvent | undefined {
  const queryClient = useQueryClient();
  const [lastEvent, setLastEvent] = useState<EntityChangedEvent>();

  useEffect(() => {
    if (!enabled) return;
    let active = true;
    const controller = new AbortController();
    const batcher = new EntityEventBatcher();
    batcher.start((events) => {
      if (active) setLastEvent(events.at(-1));
      void reconcileRealtimeBatch(queryClient, events, undefined, { signal: controller.signal });
    });
    const stream = new DashboardStreamClient(
      (event) => batcher.pushEvent(event),
      { onError: () => { if (active) void queryClient.invalidateQueries(); } },
    );
    void stream.start();
    return () => {
      active = false;
      controller.abort();
      batcher.stop();
      void stream.stop();
    };
  }, [enabled, queryClient, sessionKey]);

  return lastEvent;
}
