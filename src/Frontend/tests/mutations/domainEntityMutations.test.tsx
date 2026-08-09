import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { ContractDetailsDto } from '../../src/api/generated/types.gen';
import type { HedgeDetailsDto } from '../../src/api/generated/types.gen';
import {
  domainQueryKeys,
  type PagedEntityCache,
  useCreateContract,
  useDeleteContract,
  useDeleteHedge,
  useUpdateContract
} from '../../src/lib/mutations/domainEntityMutations';
import { queryKeys } from '../../src/lib/query/queryKeys';

const contract = {
  contractId: '11111111-1111-1111-1111-111111111111',
  contractName: 'OLD',
  counterpartyId: '22222222-2222-2222-2222-222222222222',
  productType: 'Gas',
  action: 'Buy',
  isActive: true,
  version: 4,
  createdAt: '',
  updatedAt: ''
} as ContractDetailsDto;

const secondContract = {
  ...contract,
  contractId: '33333333-3333-3333-3333-333333333333',
  contractName: 'SECOND'
};

const mountedRoots: Root[] = [];

function page<T>(items: T[], totalCount: number, pageNumber: number, pageSize = 1): PagedEntityCache<T> {
  return { items, totalCount, page: pageNumber, pageSize, hasNextPage: totalCount > pageNumber * pageSize };
}

async function renderHook<T>(client: QueryClient, useValue: () => T): Promise<() => T> {
  let current!: T;
  function Harness() {
    current = useValue();
    return null;
  }
  const root = createRoot(document.createElement('div'));
  mountedRoots.push(root);
  await act(async () => root.render(<QueryClientProvider client={client}><Harness /></QueryClientProvider>));
  return () => current;
}

function deferredFetch() {
  let resolve!: (response: Response) => void;
  vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((done) => { resolve = done; })));
  return (body: unknown, status = 200) => resolve(new Response(
    status === 204 ? null : JSON.stringify(body),
    { status, headers: status === 204 ? undefined : { 'Content-Type': 'application/json' } }
  ));
}

afterEach(async () => {
  for (const root of mountedRoots.splice(0)) await act(async () => root.unmount());
  vi.unstubAllGlobals();
});

describe('domain entity mutation hooks', () => {
  it('updates and reconciles the matching entity without corrupting sibling pages', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const pageOneKey = [...domainQueryKeys.contracts, 1] as const;
    const pageTwoKey = [...domainQueryKeys.contracts, 2] as const;
    client.setQueryData(pageOneKey, page([contract], 2, 1));
    client.setQueryData(pageTwoKey, page([secondContract], 2, 2));
    const resolve = deferredFetch();
    const mutation = await renderHook(client, () => useUpdateContract());

    await act(async () => {
      mutation().mutate({
        id: secondContract.contractId,
        version: 4,
        changes: {
          contractName: 'NEW',
          counterpartyId: secondContract.counterpartyId,
          productType: secondContract.productType,
          action: secondContract.action
        }
      });
      await vi.waitFor(() => expect(client.getQueryData<PagedEntityCache<ContractDetailsDto>>(pageTwoKey)?.items[0].contractName).toBe('NEW'));
    });
    expect(client.getQueryData<PagedEntityCache<ContractDetailsDto>>(pageOneKey)?.items[0]).toEqual(contract);
    expect(client.getQueryData<PagedEntityCache<ContractDetailsDto>>(pageTwoKey)?.totalCount).toBe(2);

    await act(async () => {
      resolve({ ...secondContract, contractName: 'NEW', version: 5 });
      await vi.waitFor(() => expect(client.getQueryData<PagedEntityCache<ContractDetailsDto>>(pageTwoKey)?.items[0].version).toBe(5));
    });
    const body = JSON.parse(String(vi.mocked(fetch).mock.calls[0][1]?.body)) as Record<string, unknown>;
    expect(body).toMatchObject({ contractId: secondContract.contractId, contractName: 'NEW', version: 4 });
  });

  it('restores every page and surfaces server truth on an OCC conflict', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const pageOneKey = [...domainQueryKeys.contracts, 1] as const;
    const pageTwoKey = [...domainQueryKeys.contracts, 2] as const;
    client.setQueryData(pageOneKey, page([contract], 2, 1));
    client.setQueryData(pageTwoKey, page([secondContract], 2, 2));
    const serverState = { ...secondContract, contractName: 'SERVER', version: 5 };
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(serverState), {
      status: 409,
      headers: { 'Content-Type': 'application/json' }
    })));
    const onConflict = vi.fn();
    const mutation = await renderHook(client, () => useUpdateContract(onConflict));

    await act(async () => {
      mutation().mutate({
        id: secondContract.contractId,
        version: 4,
        changes: {
          contractName: 'CLIENT',
          counterpartyId: secondContract.counterpartyId,
          productType: secondContract.productType,
          action: secondContract.action
        }
      });
      await vi.waitFor(() => expect(onConflict).toHaveBeenCalledWith(secondContract.contractId, serverState));
    });
    expect(client.getQueryData<PagedEntityCache<ContractDetailsDto>>(pageOneKey)).toEqual(page([contract], 2, 1));
    expect(client.getQueryData<PagedEntityCache<ContractDetailsDto>>(pageTwoKey)).toEqual(page([serverState], 2, 2));
  });

  it('prepends a bounded optimistic create only to page one and updates totals on all pages', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const pageOneKey = queryKeys.contracts.list();
    const pageTwoKey = [...domainQueryKeys.contracts, 2] as const;
    client.setQueryData(pageOneKey, page([contract], 2, 1));
    client.setQueryData(pageTwoKey, page([secondContract], 2, 2));
    const resolve = deferredFetch();
    const mutation = await renderHook(client, () => useCreateContract());

    await act(async () => {
      mutation().mutate({
        contractName: 'CREATED',
        counterpartyId: contract.counterpartyId,
        productType: contract.productType,
        action: contract.action,
        contractType: 'External'
      });
      await vi.waitFor(() => expect(client.getQueryData<PagedEntityCache<ContractDetailsDto>>(pageOneKey)?.items[0].contractName).toBe('CREATED'));
    });
    const optimisticPageOne = client.getQueryData<PagedEntityCache<ContractDetailsDto>>(pageOneKey)!;
    expect(optimisticPageOne.items).toHaveLength(1);
    expect(optimisticPageOne.totalCount).toBe(3);
    expect(client.getQueryData<PagedEntityCache<ContractDetailsDto>>(pageTwoKey)).toEqual(page([secondContract], 3, 2));

    const created = { ...contract, contractId: '44444444-4444-4444-4444-444444444444', contractName: 'CREATED', version: 1 };
    await act(async () => {
      resolve(created);
      await vi.waitFor(() => expect(client.getQueryData<PagedEntityCache<ContractDetailsDto>>(pageOneKey)?.items[0].contractId).toBe(created.contractId));
    });
  });

  it('seeds the paginated page-one key when a create starts before history has loaded', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const resolve = deferredFetch();
    const mutation = await renderHook(client, () => useCreateContract());

    const pageOneKey = queryKeys.contracts.list();
    await act(async () => {
      mutation().mutate({
        contractName: 'EARLY',
        counterpartyId: contract.counterpartyId,
        productType: contract.productType,
        action: contract.action,
        contractType: 'External'
      });
      await vi.waitFor(() => expect(client.getQueryData<PagedEntityCache<ContractDetailsDto>>(pageOneKey)?.items[0].contractName).toBe('EARLY'));
    });
    expect(client.getQueryData(domainQueryKeys.contracts)).toBeUndefined();
    await act(async () => {
      resolve({ ...contract, contractName: 'EARLY', version: 1 });
      await vi.waitFor(() => expect(mutation().isSuccess).toBe(true));
    });
  });

  it('optimistically patches soft deactivation without changing paginated totals', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const pageOneKey = [...domainQueryKeys.contracts, 1] as const;
    const pageTwoKey = [...domainQueryKeys.contracts, 2] as const;
    client.setQueryData(pageOneKey, page([contract], 2, 1));
    client.setQueryData(pageTwoKey, page([secondContract], 2, 2));
    const resolve = deferredFetch();
    const mutation = await renderHook(client, () => useDeleteContract());

    await act(async () => {
      mutation().mutate({ id: contract.contractId, version: contract.version, reason: 'Deactivate' });
      await vi.waitFor(() => expect(client.getQueryData<PagedEntityCache<ContractDetailsDto>>(pageOneKey)?.items[0].isActive).toBe(false));
    });
    expect(client.getQueryData<PagedEntityCache<ContractDetailsDto>>(pageOneKey)?.totalCount).toBe(2);
    expect(client.getQueryData<PagedEntityCache<ContractDetailsDto>>(pageTwoKey)).toEqual(page([secondContract], 2, 2));
    await act(async () => {
      resolve(undefined, 204);
      await vi.waitFor(() => expect(mutation().isSuccess).toBe(true));
    });
  });

  it('removes hard-deleted rows and decrements totals across every cached page', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const first = { hedgeId: 'hedge-1', contractId: contract.contractId, month: '2026-08-01', version: 2, createdAt: '', updatedAt: '' } as HedgeDetailsDto;
    const second = { ...first, hedgeId: 'hedge-2' };
    const pageOneKey = [...domainQueryKeys.hedges, 1] as const;
    const pageTwoKey = [...domainQueryKeys.hedges, 2] as const;
    client.setQueryData(pageOneKey, page([first], 2, 1));
    client.setQueryData(pageTwoKey, page([second], 2, 2));
    const resolve = deferredFetch();
    const mutation = await renderHook(client, () => useDeleteHedge());

    await act(async () => {
      mutation().mutate({ id: first.hedgeId, version: first.version, reason: 'Delete' });
      await vi.waitFor(() => expect(client.getQueryData<PagedEntityCache<HedgeDetailsDto>>(pageOneKey)?.items).toEqual([]));
    });
    expect(client.getQueryData<PagedEntityCache<HedgeDetailsDto>>(pageOneKey)?.totalCount).toBe(1);
    expect(client.getQueryData<PagedEntityCache<HedgeDetailsDto>>(pageTwoKey)?.totalCount).toBe(1);
    await act(async () => {
      resolve(undefined, 204);
      await vi.waitFor(() => expect(mutation().isSuccess).toBe(true));
    });
  });
});
