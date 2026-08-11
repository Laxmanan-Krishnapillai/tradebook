import { useMutation, useQueryClient, type QueryClient, type QueryKey } from '@tanstack/react-query';
import type { BioticketDetailsDto } from '../../api/generated/types.gen';
import type { CapacityBookingDetailsDto } from '../../api/generated/types.gen';
import type { ContractDetailsDto } from '../../api/generated/types.gen';
import type { CreateBioticketRequest } from '../../api/generated/types.gen';
import type { CreateCapacityBookingRequest } from '../../api/generated/types.gen';
import type { CreateContractRequest } from '../../api/generated/types.gen';
import type { CreateGooCertificateTransactionRequest } from '../../api/generated/types.gen';
import type { CreateHedgeRequest } from '../../api/generated/types.gen';
import type { CreateTaxTariffRequest } from '../../api/generated/types.gen';
import type { CreateTransferRequest } from '../../api/generated/types.gen';
import type { GooCertificateTransactionDetailsDto } from '../../api/generated/types.gen';
import type { HedgeDetailsDto } from '../../api/generated/types.gen';
import type { MarketPriceDetailsDto } from '../../api/generated/types.gen';
import type { RequestGooBatchExportRequest } from '../../api/generated/types.gen';
import type { TaxTariffDetailsDto } from '../../api/generated/types.gen';
import type { TransferDetailsDto } from '../../api/generated/types.gen';
import type { UpdateBioticketRequest } from '../../api/generated/types.gen';
import type { UpdateCapacityBookingRequest } from '../../api/generated/types.gen';
import type { UpdateContractRequest } from '../../api/generated/types.gen';
import type { UpdateGooCertificateTransactionRequest } from '../../api/generated/types.gen';
import type { UpdateHedgeRequest } from '../../api/generated/types.gen';
import type { UpdateTaxTariffRequest } from '../../api/generated/types.gen';
import type { UpdateTransferRequest } from '../../api/generated/types.gen';
import type { UpsertMarketPriceRequest } from '../../api/generated/types.gen';
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
  isPagedEntityCache,
  type PagedEntityCache,
  rollbackEntityDelete,
  rollbackEntityUpdate,
  rollbackOptimisticCreate,
} from './optimisticCache';

interface VersionedEntity { version: number; }
export type { PagedEntityCache } from './optimisticCache';
export interface EntityUpdateVariables<TChanges> {
  id: string;
  version: number;
  changes: TChanges;
  intent?: readonly (keyof TChanges)[];
}
export interface EntityDeleteVariables { id: string; version: number; reason: string; }
export type MarketPriceMutationVariables = UpsertMarketPriceRequest & {
  intent?: readonly (keyof UpsertMarketPriceRequest)[];
};
export type EntityConflictHandler<T> = (id: string, serverState?: T) => void;
type ErrorHandler = (error: unknown) => void;
type EntitySnapshots<T> = EntitySnapshot<T>[];

interface EntityMutationContext<T> {
  release?: () => void;
  scopeKey?: string;
  sessionIdentity: string;
  snapshots: EntitySnapshots<T>;
}

interface EntityDescriptor<T extends VersionedEntity> {
  basePath: string;
  idField: string;
  queryKey: readonly string[];
  listKey: QueryKey;
  detailKey: (id: string) => QueryKey;
  idOf: (entity: T) => string;
  optimisticDeletePatch?: Partial<T>;
}

const currentSessionIdentity = getAuthSessionIdentity;
function isCurrentSession<T extends { sessionIdentity?: string }>(context: T | undefined): context is T {
  return context !== undefined && context.sessionIdentity === currentSessionIdentity();
}
function isPagedCache<T>(value: unknown): value is PagedEntityCache<T> {
  return isPagedEntityCache<T>(value);
}
function takeSnapshots<T>(queryClient: QueryClient, queryKey: readonly string[]): EntitySnapshots<T> {
  return queryClient.getQueriesData<PagedEntityCache<T> | T>({ queryKey });
}
function installEntity<T extends VersionedEntity>(queryClient: QueryClient, descriptor: EntityDescriptor<T>, entity: T): void {
  queryClient.setQueryData<T>(
    descriptor.detailKey(descriptor.idOf(entity)),
    (current) => !current || current.version <= entity.version ? entity : current,
  );
  for (const [key, value] of queryClient.getQueriesData({ queryKey: descriptor.queryKey })) {
    if (!isPagedCache<T>(value)) continue;
    queryClient.setQueryData<PagedEntityCache<T>>(key, {
      ...value,
      items: value.items.map((item) => descriptor.idOf(item) === descriptor.idOf(entity) && item.version <= entity.version
        ? entity
        : item),
    });
  }
}

async function prepareEntityMutation<T extends VersionedEntity>(
  queryClient: QueryClient,
  descriptor: EntityDescriptor<T>,
  id?: string,
): Promise<EntityMutationContext<T>> {
  const sessionIdentity = currentSessionIdentity();
  const scopeKey = id === undefined
    ? undefined
    : mutationScopeKey(descriptor.queryKey, sessionIdentity, id);
  const release = scopeKey ? await acquireMutationScope(scopeKey) : undefined;
  try {
    if (scopeKey) await waitForMutationConflictResolution(scopeKey);
    await queryClient.cancelQueries({ queryKey: descriptor.queryKey });
    if (sessionIdentity !== currentSessionIdentity()) throw new Error('The authenticated session changed before the mutation started.');
    return {
      release,
      scopeKey,
      sessionIdentity,
      snapshots: takeSnapshots<T>(queryClient, descriptor.queryKey),
    };
  } catch (error) {
    release?.();
    throw error;
  }
}

function latestSnapshotEntity<T extends VersionedEntity>(
  snapshots: EntitySnapshots<T>,
  id: string,
  idOf: (entity: T) => string,
): T | undefined {
  let latest: T | undefined;
  for (const [, snapshot] of snapshots) {
    const candidates = isPagedCache<T>(snapshot) ? snapshot.items : snapshot ? [snapshot] : [];
    for (const candidate of candidates) {
      if (idOf(candidate) !== id || (latest && latest.version >= candidate.version)) continue;
      latest = candidate;
    }
  }
  return latest;
}

function rebaseChanges<T extends VersionedEntity, TChanges extends object>(
  latest: T,
  changes: TChanges,
  intent: readonly (keyof TChanges)[],
): TChanges {
  const intended = new Set<PropertyKey>(intent);
  const latestRecord = latest as unknown as Record<string, unknown>;
  return Object.fromEntries(Object.entries(changes).map(([key, value]) => [
    key,
    intended.has(key) || !(key in latestRecord) ? value : latestRecord[key],
  ])) as TChanges;
}

function settleEntityMutation(context: { release?: () => void } | undefined): void {
  context?.release?.();
}

function seedFirstPageCaches<T extends VersionedEntity>(
  queryClient: QueryClient,
  descriptor: EntityDescriptor<T>,
  snapshots: EntitySnapshots<T>,
  optimistic: T
): QueryKey[] {
  const existingFirstPageKeys = snapshots
    .filter(([cachedKey, cache]) => cache === undefined && isFirstListPage(cachedKey, descriptor.queryKey))
    .map(([cachedKey]) => cachedKey);
  const seededKeys = existingFirstPageKeys.length > 0 ? existingFirstPageKeys : [descriptor.listKey];
  for (const seededKey of seededKeys) {
    queryClient.setQueryData<PagedEntityCache<T>>(seededKey, {
      items: [optimistic],
      totalCount: 1,
      page: 1,
      pageSize: 100,
      hasNextPage: false
    });
  }
  return seededKeys;
}

function withTotalDelta<T>(cache: PagedEntityCache<T>, totalDelta: number, items: T[]): PagedEntityCache<T> {
  const totalCount = Math.max(0, cache.totalCount + totalDelta);
  return { ...cache, items, totalCount, hasNextPage: totalCount > cache.page * cache.pageSize };
}

function useEntityCreate<T extends VersionedEntity, TRequest extends object>(descriptor: EntityDescriptor<T>, onConflict: EntityConflictHandler<T>, onError: ErrorHandler) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: TRequest) => apiFetch<T>(descriptor.basePath, { method: 'POST', body: JSON.stringify(request) }),
    retry: false,
    onMutate: async (request) => {
      const context = await prepareEntityMutation(queryClient, descriptor);
      const { snapshots } = context;
      const listSnapshots = snapshots.filter((entry): entry is [QueryKey, PagedEntityCache<T>] => (
        isPagedCache<T>(entry[1]) && isUnfilteredList(entry[0], descriptor.queryKey)
      ));
      const previousTotals = new Map<string, number | undefined>(
        listSnapshots.map(([key, cache]) => [JSON.stringify(key), cache.totalCount]),
      );
      const optimisticId = `optimistic-${crypto.randomUUID()}`;
      const now = new Date().toISOString();
      const optimistic = { ...request, [descriptor.idField]: optimisticId, version: 0, createdAt: now, updatedAt: now } as unknown as T;
      let seededKeys: QueryKey[] = [];
      if (listSnapshots.length === 0) {
        seededKeys = seedFirstPageCaches(queryClient, descriptor, snapshots, optimistic);
        for (const key of seededKeys) previousTotals.set(JSON.stringify(key), undefined);
      } else {
        for (const [cachedKey, cache] of listSnapshots) {
          const items = isFirstListPage(cachedKey, descriptor.queryKey)
            ? [optimistic, ...cache.items].slice(0, cache.pageSize)
            : cache.items;
          queryClient.setQueryData(cachedKey, withTotalDelta(cache, 1, items));
        }
      }
      return {
        ...context,
        seededKeys,
        optimisticId,
        optimisticKeys: listSnapshots.length === 0 ? seededKeys : listSnapshots.map(([key]) => key),
        previousTotals,
      };
    },
    onSuccess: (created, _request, context) => {
      if (!isCurrentSession(context)) return;
      for (const [key, value] of queryClient.getQueriesData({ queryKey: descriptor.queryKey })) {
        if (!isPagedCache<T>(value)) continue;
        queryClient.setQueryData(key, { ...value, items: value.items.map((item) => descriptor.idOf(item) === context.optimisticId ? created : item) });
      }
      installEntity(queryClient, descriptor, created);
    },
    onError: (error, _request, context) => {
      if (!isCurrentSession(context)) return;
      rollbackOptimisticCreate(
        queryClient,
        context.optimisticKeys,
        context.optimisticId,
        descriptor.idOf,
        context.seededKeys,
        context.previousTotals,
      );
      if (error instanceof ApiError && error.status === 409) {
        if (context.scopeKey) markMutationConflict(context.scopeKey);
        const current = error.problem as T | undefined;
        if (current) installEntity(queryClient, descriptor, current);
        onConflict(context.optimisticId, current);
      } else onError(error);
    },
    onSettled: (_data, _error, _request, context) => {
      settleEntityMutation(context);
      if (isCurrentSession(context)) return queryClient.invalidateQueries({ queryKey: descriptor.queryKey });
    }
  });
}

function useEntityUpdate<T extends VersionedEntity, TChanges extends object>(descriptor: EntityDescriptor<T>, onConflict: EntityConflictHandler<T>, onError: ErrorHandler) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, version, changes }: EntityUpdateVariables<TChanges>) => {
      return apiFetch<T>(`${descriptor.basePath}/${encodeURIComponent(id)}`, {
        method: 'PUT',
        body: JSON.stringify({
          ...changes,
          [descriptor.idField]: id,
          version,
        }),
      });
    },
    retry: false,
    onMutate: async (variables: EntityUpdateVariables<TChanges>) => {
      const context = await prepareEntityMutation(queryClient, descriptor, variables.id);
      const latest = latestSnapshotEntity(context.snapshots, variables.id, descriptor.idOf);
      if (variables.intent && latest && latest.version > variables.version) {
        variables.changes = rebaseChanges(latest, variables.changes, variables.intent);
        variables.version = latest.version;
      }
      const { changes, id } = variables;
      for (const [key, value] of context.snapshots) {
        if (isPagedCache<T>(value)) queryClient.setQueryData(key, { ...value, items: value.items.map((item) => descriptor.idOf(item) === id ? { ...item, ...changes } : item) });
      }
      queryClient.setQueryData<T>(descriptor.detailKey(id), (entity) => entity ? { ...entity, ...changes } : entity);
      return context;
    },
    onSuccess: (updated, _variables, context) => {
      if (!isCurrentSession(context)) return;
      if (context.scopeKey) recordMutationVersion(context.scopeKey, updated.version);
      installEntity(queryClient, descriptor, updated);
    },
    onError: (error, { id, changes }, context) => {
      if (!isCurrentSession(context)) return;
      rollbackEntityUpdate(queryClient, context.snapshots, id, descriptor.idOf, changes);
      if (error instanceof ApiError && error.status === 409) {
        if (context.scopeKey) markMutationConflict(context.scopeKey);
        const current = error.problem as T | undefined;
        if (current) installEntity(queryClient, descriptor, current);
        onConflict(id, current);
      } else onError(error);
    },
    onSettled: (_data, _error, _variables, context) => {
      settleEntityMutation(context);
    }
  });
}

function useEntityDelete<T extends VersionedEntity>(descriptor: EntityDescriptor<T>, onConflict: EntityConflictHandler<T>, onError: ErrorHandler) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, version, reason }: EntityDeleteVariables) => {
      const scopeKey = mutationScopeKey(descriptor.queryKey, currentSessionIdentity(), id);
      return apiFetch<void>(`${descriptor.basePath}/${encodeURIComponent(id)}`, {
        method: 'DELETE',
        body: JSON.stringify({
          [descriptor.idField]: id,
          version: resolveMutationVersion(scopeKey, version),
          reason,
        }),
      });
    },
    retry: false,
    onMutate: async ({ id }) => {
      const context = await prepareEntityMutation(queryClient, descriptor, id);
      for (const [key, value] of context.snapshots) {
        if (!isPagedCache<T>(value)) continue;
        if (descriptor.optimisticDeletePatch) {
          queryClient.setQueryData(key, {
            ...value,
            items: value.items.map((item) => descriptor.idOf(item) === id
              ? { ...item, ...descriptor.optimisticDeletePatch }
              : item)
          });
          continue;
        }
        const contains = value.items.some((item) => descriptor.idOf(item) === id);
        if (contains || isUnfilteredList(key, descriptor.queryKey)) {
          queryClient.setQueryData(key, withTotalDelta(value, -1, value.items.filter((item) => descriptor.idOf(item) !== id)));
        }
      }
      if (descriptor.optimisticDeletePatch) queryClient.setQueryData<T>(descriptor.detailKey(id), (entity) => entity ? { ...entity, ...descriptor.optimisticDeletePatch } : entity);
      else queryClient.removeQueries({ queryKey: descriptor.detailKey(id), exact: true });
      return context;
    },
    onError: (error, { id }, context) => {
      if (!isCurrentSession(context)) return;
      rollbackEntityDelete(
        queryClient,
        context.snapshots,
        id,
        descriptor.idOf,
        descriptor.optimisticDeletePatch,
      );
      if (error instanceof ApiError && error.status === 409) {
        if (context.scopeKey) markMutationConflict(context.scopeKey);
        const current = error.problem as T | undefined;
        if (current) installEntity(queryClient, descriptor, current);
        onConflict(id, current);
      } else onError(error);
    },
    onSettled: (_data, _error, _variables, context) => {
      settleEntityMutation(context);
      if (isCurrentSession(context)) return queryClient.invalidateQueries({ queryKey: descriptor.queryKey });
    }
  });
}

const contracts = { basePath: '/api/v1/contracts', idField: 'contractId', queryKey: queryKeys.contracts.all, listKey: queryKeys.contracts.list(), detailKey: queryKeys.contracts.detail, idOf: (value: ContractDetailsDto) => value.contractId, optimisticDeletePatch: { isActive: false } } satisfies EntityDescriptor<ContractDetailsDto>;
const capacity = { basePath: '/api/v1/capacity-bookings', idField: 'capacityBookingId', queryKey: queryKeys.capacityBookings.all, listKey: queryKeys.capacityBookings.list(), detailKey: queryKeys.capacityBookings.detail, idOf: (value: CapacityBookingDetailsDto) => value.capacityBookingId } satisfies EntityDescriptor<CapacityBookingDetailsDto>;
const transfers = { basePath: '/api/v1/transfers', idField: 'transferId', queryKey: queryKeys.transfers.all, listKey: queryKeys.transfers.list(), detailKey: queryKeys.transfers.detail, idOf: (value: TransferDetailsDto) => value.transferId, optimisticDeletePatch: { status: 'Cancelled' } } satisfies EntityDescriptor<TransferDetailsDto>;
const biotickets = { basePath: '/api/v1/biotickets', idField: 'bioticketId', queryKey: queryKeys.biotickets.all, listKey: queryKeys.biotickets.list(), detailKey: queryKeys.biotickets.detail, idOf: (value: BioticketDetailsDto) => value.bioticketId, optimisticDeletePatch: { status: 'Cancelled' } } satisfies EntityDescriptor<BioticketDetailsDto>;
const certificates = { basePath: '/api/v1/goo-certificates', idField: 'gooCertificateTransactionId', queryKey: queryKeys.gooCertificates.all, listKey: queryKeys.gooCertificates.list(), detailKey: queryKeys.gooCertificates.detail, idOf: (value: GooCertificateTransactionDetailsDto) => value.gooCertificateTransactionId } satisfies EntityDescriptor<GooCertificateTransactionDetailsDto>;
const tariffs = { basePath: '/api/v1/tax-tariffs', idField: 'taxTariffId', queryKey: queryKeys.taxTariffs.all, listKey: queryKeys.taxTariffs.list(), detailKey: queryKeys.taxTariffs.detail, idOf: (value: TaxTariffDetailsDto) => value.taxTariffId } satisfies EntityDescriptor<TaxTariffDetailsDto>;
const hedges = { basePath: '/api/v1/hedges', idField: 'hedgeId', queryKey: queryKeys.hedges.all, listKey: queryKeys.hedges.list(), detailKey: queryKeys.hedges.detail, idOf: (value: HedgeDetailsDto) => value.hedgeId } satisfies EntityDescriptor<HedgeDetailsDto>;

const noConflict = () => undefined;
const noError = () => undefined;

export const domainQueryKeys = { contracts: contracts.queryKey, capacityBookings: capacity.queryKey, transfers: transfers.queryKey, biotickets: biotickets.queryKey, gooCertificates: certificates.queryKey, marketPrices: queryKeys.marketPrices.all, taxTariffs: tariffs.queryKey, hedges: hedges.queryKey };
export const useCreateContract = (onConflict: EntityConflictHandler<ContractDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityCreate<ContractDetailsDto, CreateContractRequest>(contracts, onConflict, onError);
export const useUpdateContract = (onConflict: EntityConflictHandler<ContractDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityUpdate<ContractDetailsDto, Omit<UpdateContractRequest, 'contractId' | 'version'>>(contracts, onConflict, onError);
export const useDeleteContract = (onConflict: EntityConflictHandler<ContractDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityDelete(contracts, onConflict, onError);
export const useCreateCapacityBooking = (onConflict: EntityConflictHandler<CapacityBookingDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityCreate<CapacityBookingDetailsDto, CreateCapacityBookingRequest>(capacity, onConflict, onError);
export const useUpdateCapacityBooking = (onConflict: EntityConflictHandler<CapacityBookingDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityUpdate<CapacityBookingDetailsDto, Omit<UpdateCapacityBookingRequest, 'capacityBookingId' | 'version'>>(capacity, onConflict, onError);
export const useDeleteCapacityBooking = (onConflict: EntityConflictHandler<CapacityBookingDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityDelete(capacity, onConflict, onError);
export const useCreateTransfer = (onConflict: EntityConflictHandler<TransferDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityCreate<TransferDetailsDto, CreateTransferRequest>(transfers, onConflict, onError);
export const useUpdateTransfer = (onConflict: EntityConflictHandler<TransferDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityUpdate<TransferDetailsDto, Omit<UpdateTransferRequest, 'transferId' | 'version'>>(transfers, onConflict, onError);
export const useDeleteTransfer = (onConflict: EntityConflictHandler<TransferDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityDelete(transfers, onConflict, onError);
export const useCreateBioticket = (onConflict: EntityConflictHandler<BioticketDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityCreate<BioticketDetailsDto, CreateBioticketRequest>(biotickets, onConflict, onError);
export const useUpdateBioticket = (onConflict: EntityConflictHandler<BioticketDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityUpdate<BioticketDetailsDto, Omit<UpdateBioticketRequest, 'bioticketId' | 'version'>>(biotickets, onConflict, onError);
export const useDeleteBioticket = (onConflict: EntityConflictHandler<BioticketDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityDelete(biotickets, onConflict, onError);
export const useCreateGooCertificate = (onConflict: EntityConflictHandler<GooCertificateTransactionDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityCreate<GooCertificateTransactionDetailsDto, CreateGooCertificateTransactionRequest>(certificates, onConflict, onError);
export const useUpdateGooCertificate = (onConflict: EntityConflictHandler<GooCertificateTransactionDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityUpdate<GooCertificateTransactionDetailsDto, Omit<UpdateGooCertificateTransactionRequest, 'gooCertificateTransactionId' | 'version'>>(certificates, onConflict, onError);
export const useDeleteGooCertificate = (onConflict: EntityConflictHandler<GooCertificateTransactionDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityDelete(certificates, onConflict, onError);
export function useRequestGooBatchExport(onConflict: EntityConflictHandler<GooCertificateTransactionDetailsDto> = noConflict, onError: ErrorHandler = noError) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RequestGooBatchExportRequest) => {
      const scopeKey = mutationScopeKey(
        certificates.queryKey,
        currentSessionIdentity(),
        request.gooCertificateTransactionId,
      );
      return apiFetch<GooCertificateTransactionDetailsDto>(
        `/api/v1/goo-certificates/${encodeURIComponent(request.gooCertificateTransactionId)}/request-batch-export`,
        {
          method: 'POST',
          body: JSON.stringify({
            ...request,
            version: resolveMutationVersion(scopeKey, request.version),
          }),
        },
      );
    },
    retry: false,
    onMutate: async (request) => {
      const context = await prepareEntityMutation(
        queryClient,
        certificates,
        request.gooCertificateTransactionId,
      );
      for (const [key, value] of context.snapshots) {
        if (!isPagedCache<GooCertificateTransactionDetailsDto>(value)) continue;
        queryClient.setQueryData(key, {
          ...value,
          items: value.items.map((item) => item.gooCertificateTransactionId === request.gooCertificateTransactionId
            ? { ...item, status: 'Batch export requested' }
            : item),
        });
      }
      queryClient.setQueryData<GooCertificateTransactionDetailsDto>(certificates.detailKey(request.gooCertificateTransactionId), (entity) => entity ? { ...entity, status: 'Batch export requested' } : entity);
      return context;
    },
    onSuccess: (saved, _request, context) => {
      if (!isCurrentSession(context)) return;
      if (context.scopeKey) recordMutationVersion(context.scopeKey, saved.version);
      installEntity(queryClient, certificates, saved);
    },
    onError: (error, request, context) => {
      if (!isCurrentSession(context)) return;
      rollbackEntityUpdate(
        queryClient,
        context.snapshots,
        request.gooCertificateTransactionId,
        certificates.idOf,
        { status: 'Batch export requested' },
      );
      if (error instanceof ApiError && error.status === 409) {
        if (context.scopeKey) markMutationConflict(context.scopeKey);
        const current = error.problem as GooCertificateTransactionDetailsDto | undefined;
        if (current) installEntity(queryClient, certificates, current);
        onConflict(request.gooCertificateTransactionId, current);
      }
      else onError(error);
    },
    onSettled: (_data, _error, _request, context) => {
      settleEntityMutation(context);
      if (isCurrentSession(context)) return queryClient.invalidateQueries({ queryKey: certificates.queryKey });
    }
  });
}
export const useCreateTaxTariff = (onConflict: EntityConflictHandler<TaxTariffDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityCreate<TaxTariffDetailsDto, CreateTaxTariffRequest>(tariffs, onConflict, onError);
export const useUpdateTaxTariff = (onConflict: EntityConflictHandler<TaxTariffDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityUpdate<TaxTariffDetailsDto, Omit<UpdateTaxTariffRequest, 'taxTariffId' | 'version'>>(tariffs, onConflict, onError);
export const useDeleteTaxTariff = (onConflict: EntityConflictHandler<TaxTariffDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityDelete(tariffs, onConflict, onError);
export const useCreateHedge = (onConflict: EntityConflictHandler<HedgeDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityCreate<HedgeDetailsDto, CreateHedgeRequest>(hedges, onConflict, onError);
export const useUpdateHedge = (onConflict: EntityConflictHandler<HedgeDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityUpdate<HedgeDetailsDto, Omit<UpdateHedgeRequest, 'hedgeId' | 'version'>>(hedges, onConflict, onError);
export const useDeleteHedge = (onConflict: EntityConflictHandler<HedgeDetailsDto> = noConflict, onError: ErrorHandler = noError) => useEntityDelete(hedges, onConflict, onError);

export function useUpsertMarketPrice(onConflict: EntityConflictHandler<MarketPriceDetailsDto> = noConflict, onError: ErrorHandler = noError) {
  const queryClient = useQueryClient();
  const queryKey = domainQueryKeys.marketPrices;
  const descriptor = { basePath: '/api/v1/market-prices', idField: 'priceDate', queryKey, listKey: queryKeys.marketPrices.list(), detailKey: queryKeys.marketPrices.detail, idOf: (value: MarketPriceDetailsDto) => value.priceDate } satisfies EntityDescriptor<MarketPriceDetailsDto>;
  return useMutation({
    mutationFn: ({ intent: _intent, ...request }: MarketPriceMutationVariables) => {
      return apiFetch<MarketPriceDetailsDto>(
        `/api/v1/market-prices/${encodeURIComponent(request.priceDate)}`,
        {
          method: 'PUT',
          body: JSON.stringify(request),
        },
      );
    },
    retry: false,
    onMutate: async (request: MarketPriceMutationVariables) => {
      const context = await prepareEntityMutation(queryClient, descriptor, request.priceDate);
      const { snapshots } = context;
      const latest = latestSnapshotEntity(snapshots, request.priceDate, descriptor.idOf);
      if (request.intent && latest && latest.version > request.version) {
        Object.assign(request, rebaseChanges(latest, request, request.intent), {
          version: latest.version,
        });
      }
      const { intent: _intent, ...wireRequest } = request;
      const listSnapshots = snapshots.filter((entry): entry is [QueryKey, PagedEntityCache<MarketPriceDetailsDto>] => (
        isPagedCache<MarketPriceDetailsDto>(entry[1]) && isUnfilteredList(entry[0], queryKey)
      ));
      const previousTotals = new Map<string, number | undefined>(
        listSnapshots.map(([key, cache]) => [JSON.stringify(key), cache.totalCount]),
      );
      const exists = listSnapshots.some(([, cache]) => cache.items.some((item) => item.priceDate === request.priceDate));
      const optimistic = { ...wireRequest } as MarketPriceDetailsDto;
      let seededKeys: QueryKey[] = [];
      if (listSnapshots.length === 0) {
        seededKeys = seedFirstPageCaches(queryClient, descriptor, snapshots, optimistic);
        for (const key of seededKeys) previousTotals.set(JSON.stringify(key), undefined);
      } else {
        for (const [cachedKey, cache] of listSnapshots) {
          const items = exists
            ? cache.items.map((item) => item.priceDate === request.priceDate ? { ...item, ...wireRequest } : item)
            : isFirstListPage(cachedKey, queryKey) ? [optimistic, ...cache.items].slice(0, cache.pageSize) : cache.items;
          queryClient.setQueryData(cachedKey, exists ? { ...cache, items } : withTotalDelta(cache, 1, items));
        }
      }
      return {
        ...context,
        exists,
        optimisticKeys: listSnapshots.length === 0 ? seededKeys : listSnapshots.map(([key]) => key),
        previousTotals,
        seededKeys,
      };
    },
    onSuccess: (saved, _request, context) => {
      if (!isCurrentSession(context)) return;
      if (context.scopeKey) recordMutationVersion(context.scopeKey, saved.version);
      installEntity(queryClient, descriptor, saved);
    },
    onError: (error, request, context) => {
      if (!isCurrentSession(context)) return;
      const { intent: _intent, ...wireRequest } = request;
      if (context.exists) {
        rollbackEntityUpdate(
          queryClient,
          context.snapshots,
          request.priceDate,
          descriptor.idOf,
          wireRequest,
        );
      } else {
        rollbackOptimisticCreate(
          queryClient,
          context.optimisticKeys,
          request.priceDate,
          descriptor.idOf,
          context.seededKeys,
          context.previousTotals,
        );
      }
      if (error instanceof ApiError && error.status === 409) {
        if (context.scopeKey) markMutationConflict(context.scopeKey);
        const current = error.problem as MarketPriceDetailsDto | undefined;
        if (current) installEntity(queryClient, descriptor, current);
        onConflict(request.priceDate, current);
      } else onError(error);
    },
    onSettled: (_data, _error, _request, context) => {
      settleEntityMutation(context);
      if (isCurrentSession(context) && !context.exists) return queryClient.invalidateQueries({ queryKey });
    }
  });
}

export function useDeleteMarketPrice(onConflict: EntityConflictHandler<MarketPriceDetailsDto> = noConflict, onError: ErrorHandler = noError) {
  const descriptor = { basePath: '/api/v1/market-prices', idField: 'priceDate', queryKey: domainQueryKeys.marketPrices, listKey: queryKeys.marketPrices.list(), detailKey: queryKeys.marketPrices.detail, idOf: (value: MarketPriceDetailsDto) => value.priceDate } satisfies EntityDescriptor<MarketPriceDetailsDto>;
  return useEntityDelete(descriptor, onConflict, onError);
}
