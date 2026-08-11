import { queryOptions } from '@tanstack/react-query';
import type {
  GetBioticketHistoryResponse,
  GetCapacityBookingHistoryResponse,
  GetContractHistoryResponse,
  GetDeliveryHistoryResponse,
  GetGooCertificateHistoryResponse,
  GetHedgeHistoryResponse,
  GetMarketPriceHistoryResponse,
  GetTaxTariffHistoryResponse,
  GetTransferHistoryResponse,
} from '../../api/generated/types.gen';
import { apiFetch } from '../api/client';
import { queryKeys } from './queryKeys';

const INITIAL_LIST_PAGE = 1;
const INITIAL_LIST_PAGE_SIZE = 100;

function createInitialListQueryOptions<TData>(queryKey: readonly unknown[], basePath: string) {
  return queryOptions({
    queryKey,
    queryFn: ({ signal }) =>
      apiFetch<TData>(
        `${basePath}?page=${INITIAL_LIST_PAGE}&pageSize=${INITIAL_LIST_PAGE_SIZE}`,
        { signal },
      ),
  });
}

export const initialListQueryOptions = {
  biotickets: () =>
    createInitialListQueryOptions<GetBioticketHistoryResponse>(
      queryKeys.biotickets.list({ page: INITIAL_LIST_PAGE, pageSize: INITIAL_LIST_PAGE_SIZE }),
      '/api/v1/biotickets',
    ),
  capacityBookings: () =>
    createInitialListQueryOptions<GetCapacityBookingHistoryResponse>(
      queryKeys.capacityBookings.list({ page: INITIAL_LIST_PAGE, pageSize: INITIAL_LIST_PAGE_SIZE }),
      '/api/v1/capacity-bookings',
    ),
  contracts: () =>
    createInitialListQueryOptions<GetContractHistoryResponse>(
      queryKeys.contracts.list({ page: INITIAL_LIST_PAGE, pageSize: INITIAL_LIST_PAGE_SIZE }),
      '/api/v1/contracts',
    ),
  deliveries: () =>
    createInitialListQueryOptions<GetDeliveryHistoryResponse>(
      queryKeys.deliveries.list({ page: INITIAL_LIST_PAGE, pageSize: INITIAL_LIST_PAGE_SIZE }),
      '/api/v1/deliveries',
    ),
  gooCertificates: () =>
    createInitialListQueryOptions<GetGooCertificateHistoryResponse>(
      queryKeys.gooCertificates.list({ page: INITIAL_LIST_PAGE, pageSize: INITIAL_LIST_PAGE_SIZE }),
      '/api/v1/goo-certificates',
    ),
  hedges: () =>
    createInitialListQueryOptions<GetHedgeHistoryResponse>(
      queryKeys.hedges.list({ page: INITIAL_LIST_PAGE, pageSize: INITIAL_LIST_PAGE_SIZE }),
      '/api/v1/hedges',
    ),
  marketPrices: () =>
    createInitialListQueryOptions<GetMarketPriceHistoryResponse>(
      queryKeys.marketPrices.list({ page: INITIAL_LIST_PAGE, pageSize: INITIAL_LIST_PAGE_SIZE }),
      '/api/v1/market-prices',
    ),
  taxTariffs: () =>
    createInitialListQueryOptions<GetTaxTariffHistoryResponse>(
      queryKeys.taxTariffs.list({ page: INITIAL_LIST_PAGE, pageSize: INITIAL_LIST_PAGE_SIZE }),
      '/api/v1/tax-tariffs',
    ),
  transfers: () =>
    createInitialListQueryOptions<GetTransferHistoryResponse>(
      queryKeys.transfers.list({ page: INITIAL_LIST_PAGE, pageSize: INITIAL_LIST_PAGE_SIZE }),
      '/api/v1/transfers',
    ),
} as const;
