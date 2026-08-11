import { useMutation, useQueryClient, type QueryClient, type QueryKey } from '@tanstack/react-query';
import type { CreatePhysicalDeliveryRequest } from '../../api/generated/types.gen';
import type { CreatePhysicalDeliveryResponse } from '../../api/generated/types.gen';
import type { GetDeliveryHistoryResponse } from '../../api/generated/types.gen';
import type { PhysicalDeliveryDetailsDto } from '../../api/generated/types.gen';
import type { UpdatePhysicalDeliveryRequest } from '../../api/generated/types.gen';
import { ApiError, apiFetch } from '../api/client';
import { isFirstListPage, isUnfilteredList, queryKeys } from '../query/queryKeys';
import { getAuthSessionIdentity } from '../state/useAuthStore';
import {
  acquireMutationScope,
  markMutationConflict,
  mutationScopeKey,
  recordMutationVersion,
  resolveMutationVersion,
  waitForMutationConflictResolution,
} from './mutationCoordinator';
import {
  type EntitySnapshot,
  rollbackEntityDelete,
  rollbackEntityUpdate,
  rollbackOptimisticCreate,
} from './optimisticCache';

export type CreateDeliveryVariables = CreatePhysicalDeliveryRequest;
type DeliveryChanges = Omit<UpdatePhysicalDeliveryRequest, 'deliveryId' | 'version'>;
export interface UpdateDeliveryVariables {
  id: string;
  version: number;
  changes: DeliveryChanges;
  intent?: readonly (keyof DeliveryChanges)[];
}
export interface DeleteDeliveryVariables { id: string; version: number; reason: string; }

type ConflictHandler = (id: string, serverState?: PhysicalDeliveryDetailsDto) => void;
type ErrorHandler = (error: unknown) => void;

interface MutationContext {
  release?: () => void;
  scopeKey?: string;
  sessionIdentity: string;
  snapshots: EntitySnapshot<PhysicalDeliveryDetailsDto>[];
}

interface CreateMutationContext extends MutationContext {
  optimisticId: string;
  optimisticKeys: QueryKey[];
  previousTotals: ReadonlyMap<string, number | undefined>;
  seededKeys: QueryKey[];
}

export const deliveryQueryKey = queryKeys.deliveries.all;

const sessionIdentity = getAuthSessionIdentity;

function isCurrentSession<T extends MutationContext>(context: T | undefined): context is T {
  return context !== undefined && context.sessionIdentity === sessionIdentity();
}

function isHistory(value: unknown): value is GetDeliveryHistoryResponse {
  return typeof value === 'object' && value !== null && Array.isArray((value as { items?: unknown }).items);
}

function snapshots(queryClient: QueryClient): EntitySnapshot<PhysicalDeliveryDetailsDto>[] {
  return queryClient.getQueriesData<GetDeliveryHistoryResponse | PhysicalDeliveryDetailsDto>({
    queryKey: queryKeys.deliveries.all,
  });
}

function patchHistory(
  queryClient: QueryClient,
  updater: (history: GetDeliveryHistoryResponse, key: QueryKey) => GetDeliveryHistoryResponse,
): void {
  for (const [key, value] of queryClient.getQueriesData({ queryKey: queryKeys.deliveries.all })) {
    if (isHistory(value)) queryClient.setQueryData(key, updater(value, key));
  }
}

function installDelivery(queryClient: QueryClient, delivery: PhysicalDeliveryDetailsDto): void {
  patchHistory(queryClient, (history) => ({
    ...history,
    items: history.items.map((item) => item.deliveryId === delivery.deliveryId && item.version <= delivery.version
      ? delivery
      : item),
  }));
  queryClient.setQueryData<PhysicalDeliveryDetailsDto>(
    queryKeys.deliveries.detail(delivery.deliveryId),
    (current) => !current || current.version <= delivery.version ? delivery : current,
  );
}

function optimisticDelivery(request: CreateDeliveryVariables, optimisticId: string): PhysicalDeliveryDetailsDto {
  const now = new Date().toISOString();
  return {
    deliveryId: optimisticId,
    contractId: request.contractId,
    contractInstanceId: request.contractInstanceId ?? optimisticId,
    bookType: request.bookType,
    supplyMonth: request.supplyMonth,
    capacityMw: request.capacityMw,
    volumeNominatedMwh: request.volumeNominatedMwh,
    volumeRealisedMwh: request.volumeRealisedMwh,
    volumeMwh: request.volumeRealisedMwh,
    priceMechanism: request.priceMechanism,
    revenueEur: null,
    subtotalEur: null,
    vatEur: null,
    invoiceAmountEur: null,
    status: 'Pending - No Invoice',
    version: 0,
    createdAt: now,
    updatedAt: now,
  };
}

function replaceOptimistic(
  queryClient: QueryClient,
  optimisticId: string,
  response: CreatePhysicalDeliveryResponse,
): void {
  patchHistory(queryClient, (history) => ({
    ...history,
    items: history.items.map((item) => item.deliveryId === optimisticId ? {
      ...item,
      ...response,
      updatedAt: response.createdAt,
    } : item),
  }));
}

function installConflict(
  queryClient: QueryClient,
  id: string,
  error: ApiError,
  onConflict: ConflictHandler,
): void {
  const current = error.problem as PhysicalDeliveryDetailsDto | undefined;
  if (current) installDelivery(queryClient, current);
  onConflict(id, current);
}

async function prepareCreateMutation(queryClient: QueryClient): Promise<MutationContext> {
  const identity = sessionIdentity();
  await queryClient.cancelQueries({ queryKey: queryKeys.deliveries.all });
  if (identity !== sessionIdentity()) throw new Error('The authenticated session changed before the mutation started.');
  return { sessionIdentity: identity, snapshots: snapshots(queryClient) };
}

async function prepareMutation(queryClient: QueryClient, id: string): Promise<MutationContext> {
  const identity = sessionIdentity();
  const scopeKey = mutationScopeKey(queryKeys.deliveries.all, identity, id);
  const release = await acquireMutationScope(scopeKey);
  try {
    await waitForMutationConflictResolution(scopeKey);
    await queryClient.cancelQueries({ queryKey: queryKeys.deliveries.all });
    if (identity !== sessionIdentity()) throw new Error('The authenticated session changed before the mutation started.');
    return { release, scopeKey, sessionIdentity: identity, snapshots: snapshots(queryClient) };
  } catch (error) {
    release();
    throw error;
  }
}

function latestDelivery(
  snapshots: EntitySnapshot<PhysicalDeliveryDetailsDto>[],
  id: string,
): PhysicalDeliveryDetailsDto | undefined {
  let latest: PhysicalDeliveryDetailsDto | undefined;
  for (const [, snapshot] of snapshots) {
    const candidates = isHistory(snapshot) ? snapshot.items : snapshot ? [snapshot] : [];
    for (const candidate of candidates) {
      if (candidate.deliveryId !== id || (latest && latest.version >= candidate.version)) continue;
      latest = candidate;
    }
  }
  return latest;
}

function settleMutation(context: MutationContext | undefined): void {
  context?.release?.();
}

const noError: ErrorHandler = () => undefined;

export function useCreateDelivery(onError: ErrorHandler = noError) {
  const queryClient = useQueryClient();
  return useMutation<CreatePhysicalDeliveryResponse, unknown, CreateDeliveryVariables, CreateMutationContext>({
    mutationFn: (request) => apiFetch<CreatePhysicalDeliveryResponse>('/api/v1/deliveries', {
      method: 'POST',
      body: JSON.stringify(request),
    }),
    retry: false,
    onMutate: async (request) => {
      const context = await prepareCreateMutation(queryClient);
      const optimisticId = `optimistic-${crypto.randomUUID()}`;
      const optimistic = optimisticDelivery(request, optimisticId);
      const listCaches = context.snapshots.filter(([key, value]) => (
        isHistory(value) && isUnfilteredList(key, queryKeys.deliveries.all)
      ));
      const previousTotals = new Map<string, number | undefined>(
        listCaches.map(([key, value]) => [JSON.stringify(key), (value as GetDeliveryHistoryResponse).totalCount]),
      );
      const seededKeys: QueryKey[] = [];
      if (listCaches.length === 0) {
        const key = queryKeys.deliveries.list({ page: 1, pageSize: 100 });
        seededKeys.push(key);
        previousTotals.set(JSON.stringify(key), undefined);
        queryClient.setQueryData<GetDeliveryHistoryResponse>(key, {
          items: [optimistic],
          totalCount: 1,
          page: 1,
          pageSize: 100,
          hasNextPage: false,
        });
      } else {
        for (const [key, value] of listCaches) {
          const history = value as GetDeliveryHistoryResponse;
          const items = isFirstListPage(key, queryKeys.deliveries.all)
            ? [optimistic, ...history.items].slice(0, history.pageSize)
            : history.items;
          queryClient.setQueryData<GetDeliveryHistoryResponse>(key, {
            ...history,
            items,
            totalCount: history.totalCount + 1,
            hasNextPage: history.totalCount + 1 > history.page * history.pageSize,
          });
        }
      }
      return {
        ...context,
        optimisticId,
        optimisticKeys: listCaches.length === 0 ? seededKeys : listCaches.map(([key]) => key),
        previousTotals,
        seededKeys,
      };
    },
    onSuccess: (created, _request, context) => {
      if (isCurrentSession(context)) replaceOptimistic(queryClient, context.optimisticId, created);
    },
    onError: (error, _request, context) => {
      if (!isCurrentSession(context)) return;
      rollbackOptimisticCreate(
        queryClient,
        context.optimisticKeys,
        context.optimisticId,
        (delivery: PhysicalDeliveryDetailsDto) => delivery.deliveryId,
        context.seededKeys,
        context.previousTotals,
      );
      onError(error);
    },
    onSettled: (_data, _error, _request, context) => {
      settleMutation(context);
      if (isCurrentSession(context)) return queryClient.invalidateQueries({ queryKey: queryKeys.deliveries.all });
    },
  });
}

export function useUpdateDelivery(onConflict: ConflictHandler, onError: ErrorHandler = noError) {
  const queryClient = useQueryClient();
  return useMutation<PhysicalDeliveryDetailsDto, unknown, UpdateDeliveryVariables, MutationContext>({
    mutationFn: ({ id, version, changes }) => {
      return apiFetch<PhysicalDeliveryDetailsDto>(
        `/api/v1/deliveries/${encodeURIComponent(id)}`,
        {
          method: 'PUT',
          body: JSON.stringify({
            deliveryId: id,
            version,
            ...changes,
          }),
        },
      );
    },
    retry: false,
    onMutate: async (variables) => {
      const context = await prepareMutation(queryClient, variables.id);
      const latest = latestDelivery(context.snapshots, variables.id);
      if (variables.intent && latest && latest.version > variables.version) {
        const intended = new Set<PropertyKey>(variables.intent);
        const latestRecord = latest as unknown as Record<string, unknown>;
        variables.changes = Object.fromEntries(Object.entries(variables.changes).map(([key, value]) => [
          key,
          intended.has(key) || !(key in latestRecord) ? value : latestRecord[key],
        ])) as UpdateDeliveryVariables['changes'];
        variables.version = latest.version;
      }
      const { changes, id } = variables;
      patchHistory(queryClient, (history) => ({
        ...history,
        items: history.items.map((delivery) => delivery.deliveryId === id
          ? { ...delivery, ...changes, status: changes.status ?? delivery.status }
          : delivery),
      }));
      queryClient.setQueryData<PhysicalDeliveryDetailsDto>(queryKeys.deliveries.detail(id), (delivery) => (
        delivery ? { ...delivery, ...changes, status: changes.status ?? delivery.status } : delivery
      ));
      return context;
    },
    onSuccess: (updated, _variables, context) => {
      if (!isCurrentSession(context)) return;
      if (context.scopeKey) recordMutationVersion(context.scopeKey, updated.version);
      installDelivery(queryClient, updated);
    },
    onError: (error, { id, changes }, context) => {
      if (!isCurrentSession(context)) return;
      rollbackEntityUpdate(queryClient, context.snapshots, id, (delivery) => delivery.deliveryId, changes);
      if (error instanceof ApiError && error.status === 409) {
        if (context.scopeKey) markMutationConflict(context.scopeKey);
        installConflict(queryClient, id, error, onConflict);
      }
      else onError(error);
    },
    onSettled: (_data, _error, _variables, context) => {
      settleMutation(context);
    },
  });
}

export function useDeleteDelivery(onConflict: ConflictHandler, onError: ErrorHandler = noError) {
  const queryClient = useQueryClient();
  return useMutation<void, unknown, DeleteDeliveryVariables, MutationContext>({
    mutationFn: ({ id, version, reason }) => {
      const scopeKey = mutationScopeKey(queryKeys.deliveries.all, sessionIdentity(), id);
      return apiFetch<void>(`/api/v1/deliveries/${encodeURIComponent(id)}`, {
        method: 'DELETE',
        body: JSON.stringify({
          deliveryId: id,
          version: resolveMutationVersion(scopeKey, version),
          reason,
        }),
      });
    },
    retry: false,
    onMutate: async ({ id }) => {
      const context = await prepareMutation(queryClient, id);
      patchHistory(queryClient, (history) => {
        const contained = history.items.some((delivery) => delivery.deliveryId === id);
        if (!contained) return history;
        const items = history.items.map((delivery) => delivery.deliveryId === id
          ? { ...delivery, status: 'Cancelled' }
          : delivery);
        return { ...history, items };
      });
      queryClient.setQueryData<PhysicalDeliveryDetailsDto>(queryKeys.deliveries.detail(id), (delivery) => (
        delivery ? { ...delivery, status: 'Cancelled' } : delivery
      ));
      return context;
    },
    onError: (error, { id }, context) => {
      if (!isCurrentSession(context)) return;
      rollbackEntityDelete(
        queryClient,
        context.snapshots,
        id,
        (delivery) => delivery.deliveryId,
        { status: 'Cancelled' },
      );
      if (error instanceof ApiError && error.status === 409) {
        if (context.scopeKey) markMutationConflict(context.scopeKey);
        installConflict(queryClient, id, error, onConflict);
      }
      else onError(error);
    },
    onSettled: (_data, _error, _variables, context) => {
      settleMutation(context);
      if (isCurrentSession(context)) return queryClient.invalidateQueries({ queryKey: queryKeys.deliveries.all });
    },
  });
}
