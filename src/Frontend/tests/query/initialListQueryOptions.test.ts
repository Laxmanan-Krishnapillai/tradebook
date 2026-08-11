import { QueryClient } from '@tanstack/react-query';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { initialListQueryOptions } from '../../src/lib/query/initialListQueryOptions';
import { queryKeys } from '../../src/lib/query/queryKeys';

const payload = { items: [], totalCount: 0, page: 1, pageSize: 100, hasNextPage: false };

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('initial list query options', () => {
  it.each([
    {
      createOptions: initialListQueryOptions.biotickets,
      expectedKey: queryKeys.biotickets.list({ page: 1, pageSize: 100 }),
      expectedPath: '/api/v1/biotickets?page=1&pageSize=100',
    },
    {
      createOptions: initialListQueryOptions.capacityBookings,
      expectedKey: queryKeys.capacityBookings.list({ page: 1, pageSize: 100 }),
      expectedPath: '/api/v1/capacity-bookings?page=1&pageSize=100',
    },
    {
      createOptions: initialListQueryOptions.contracts,
      expectedKey: queryKeys.contracts.list({ page: 1, pageSize: 100 }),
      expectedPath: '/api/v1/contracts?page=1&pageSize=100',
    },
    {
      createOptions: initialListQueryOptions.deliveries,
      expectedKey: queryKeys.deliveries.list({ page: 1, pageSize: 100 }),
      expectedPath: '/api/v1/deliveries?page=1&pageSize=100',
    },
    {
      createOptions: initialListQueryOptions.gooCertificates,
      expectedKey: queryKeys.gooCertificates.list({ page: 1, pageSize: 100 }),
      expectedPath: '/api/v1/goo-certificates?page=1&pageSize=100',
    },
    {
      createOptions: initialListQueryOptions.hedges,
      expectedKey: queryKeys.hedges.list({ page: 1, pageSize: 100 }),
      expectedPath: '/api/v1/hedges?page=1&pageSize=100',
    },
    {
      createOptions: initialListQueryOptions.marketPrices,
      expectedKey: queryKeys.marketPrices.list({ page: 1, pageSize: 100 }),
      expectedPath: '/api/v1/market-prices?page=1&pageSize=100',
    },
    {
      createOptions: initialListQueryOptions.taxTariffs,
      expectedKey: queryKeys.taxTariffs.list({ page: 1, pageSize: 100 }),
      expectedPath: '/api/v1/tax-tariffs?page=1&pageSize=100',
    },
    {
      createOptions: initialListQueryOptions.transfers,
      expectedKey: queryKeys.transfers.list({ page: 1, pageSize: 100 }),
      expectedPath: '/api/v1/transfers?page=1&pageSize=100',
    },
  ])('shares the page query key and fetches $expectedPath', async ({ createOptions, expectedKey, expectedPath }) => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(payload), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }));
    vi.stubGlobal('fetch', fetchMock);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const options = createOptions();

    expect(options.queryKey).toEqual(expectedKey);
    if (typeof options.queryFn !== 'function') throw new TypeError('Expected an initial-list query function.');
    await expect(options.queryFn({
      client,
      meta: undefined,
      queryKey: options.queryKey,
      signal: new AbortController().signal,
    })).resolves.toEqual(payload);
    expect(String(fetchMock.mock.calls[0]?.[0])).toContain(expectedPath);
  });
});
