import { useMutation, useQueryClient, type QueryClient, type QueryKey } from '@tanstack/react-query';
import type { CreatePhysicalDeliveryRequest } from '../../api/generated/create-physical-delivery-request';
import type { CreatePhysicalDeliveryResponse } from '../../api/generated/create-physical-delivery-response';
import type { GetDeliveryHistoryResponse } from '../../api/generated/get-delivery-history-response';
import type { PhysicalDeliveryDetailsDto } from '../../api/generated/physical-delivery-details-dto';
import type { UpdatePhysicalDeliveryRequest } from '../../api/generated/update-physical-delivery-request';
import { ApiError, apiFetch } from '../api/client';
import { isFirstListPage, isUnfilteredList, queryKeys } from '../query/queryKeys';
import { getAuthSession } from '../state/useAuthStore';

export type CreateDeliveryVariables = CreatePhysicalDeliveryRequest;
export interface UpdateDeliveryVariables {
  id: string;
  version: number;
  changes: Omit<UpdatePhysicalDeliveryRequest, 'deliveryId' | 'version'>;
}
export interface DeleteDeliveryVariables { id: string; version: number; reason: string; }

type ConflictHandler = (id: string, serverState?: PhysicalDeliveryDetailsDto) => void;
type ErrorHandler = (error: unknown) => void;
type Snapshot = [QueryKey, unknown];

interface MutationContext {
  sessionIdentity: string | undefined;
  snapshots: Snapshot[];
}

interface CreateMutationContext extends MutationContext {
  optimisticId: string;
  seededKeys: QueryKey[];
}

export const deliveryQueryKey = queryKeys.deliveries.all;

function sessionIdentity(): string | undefined {
  const session = getAuthSession();
  return session ? `${session.actorId}\u0000${session.accessToken}` : undefined;
}

function isCurrentSession<T extends MutationContext>(context: T | undefined): context is T {
  return context !== undefined && context.sessionIdentity === sessionIdentity();
}

function isHistory(value: unknown): value is GetDeliveryHistoryResponse {
  return typeof value === 'object' && value !== null && Array.isArray((value as { items?: unknown }).items);
}

function snapshots(queryClient: QueryClient): Snapshot[] {
  return queryClient.getQueriesData({ queryKey: queryKeys.deliveries.all });
}

function restore(queryClient: QueryClient, previous: Snapshot[], seededKeys: QueryKey[] = []): void {
  for (const key of seededKeys) queryClient.removeQueries({ queryKey: key, exact: true });
  for (const [key, value] of previous) {
    if (value === undefined) queryClient.removeQueries({ queryKey: key, exact: true });
    else queryClient.setQueryData(key, value);
  }
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
    items: history.items.map((item) => item.deliveryId === delivery.deliveryId ? delivery : item),
  }));
  queryClient.setQueryData(queryKeys.deliveries.detail(delivery.deliveryId), delivery);
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

async function prepareMutation(queryClient: QueryClient): Promise<MutationContext> {
  const identity = sessionIdentity();
  await queryClient.cancelQueries({ queryKey: queryKeys.deliveries.all });
  if (identity !== sessionIdentity()) throw new Error('The authenticated session changed before the mutation started.');
  return { sessionIdentity: identity, snapshots: snapshots(queryClient) };
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
      const context = await prepareMutation(queryClient);
      const optimisticId = `optimistic-${crypto.randomUUID()}`;
      const optimistic = optimisticDelivery(request, optimisticId);
      const listCaches = context.snapshots.filter(([key, value]) => (
        isHistory(value) && isUnfilteredList(key, queryKeys.deliveries.all)
      ));
      const seededKeys: QueryKey[] = [];
      if (listCaches.length === 0) {
        const key = queryKeys.deliveries.list({ page: 1, pageSize: 100 });
        seededKeys.push(key);
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
      return { ...context, optimisticId, seededKeys };
    },
    onSuccess: (created, _request, context) => {
      if (isCurrentSession(context)) replaceOptimistic(queryClient, context.optimisticId, created);
    },
    onError: (error, _request, context) => {
      if (!isCurrentSession(context)) return;
      restore(queryClient, context.snapshots, context.seededKeys);
      onError(error);
    },
    onSettled: (_data, _error, _request, context) => {
      if (isCurrentSession(context)) return queryClient.invalidateQueries({ queryKey: queryKeys.deliveries.all });
    },
  });
}

export function useUpdateDelivery(onConflict: ConflictHandler, onError: ErrorHandler = noError) {
  const queryClient = useQueryClient();
  return useMutation<PhysicalDeliveryDetailsDto, unknown, UpdateDeliveryVariables, MutationContext>({
    mutationFn: ({ id, version, changes }) => apiFetch<PhysicalDeliveryDetailsDto>(
      `/api/v1/deliveries/${encodeURIComponent(id)}`,
      { method: 'PUT', body: JSON.stringify({ deliveryId: id, version, ...changes }) },
    ),
    retry: false,
    onMutate: async ({ id, changes }) => {
      const context = await prepareMutation(queryClient);
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
      if (isCurrentSession(context)) installDelivery(queryClient, updated);
    },
    onError: (error, { id }, context) => {
      if (!isCurrentSession(context)) return;
      restore(queryClient, context.snapshots);
      if (error instanceof ApiError && error.status === 409) installConflict(queryClient, id, error, onConflict);
      else onError(error);
    },
    onSettled: (_data, _error, _variables, context) => {
      if (isCurrentSession(context)) return queryClient.invalidateQueries({ queryKey: queryKeys.deliveries.all });
    },
  });
}

export function useDeleteDelivery(onConflict: ConflictHandler, onError: ErrorHandler = noError) {
  const queryClient = useQueryClient();
  return useMutation<void, unknown, DeleteDeliveryVariables, MutationContext>({
    mutationFn: ({ id, version, reason }) => apiFetch<void>(`/api/v1/deliveries/${encodeURIComponent(id)}`, {
      method: 'DELETE',
      body: JSON.stringify({ deliveryId: id, version, reason }),
    }),
    retry: false,
    onMutate: async ({ id }) => {
      const context = await prepareMutation(queryClient);
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
      restore(queryClient, context.snapshots);
      if (error instanceof ApiError && error.status === 409) installConflict(queryClient, id, error, onConflict);
      else onError(error);
    },
    onSettled: (_data, _error, _variables, context) => {
      if (isCurrentSession(context)) return queryClient.invalidateQueries({ queryKey: queryKeys.deliveries.all });
    },
  });
}
