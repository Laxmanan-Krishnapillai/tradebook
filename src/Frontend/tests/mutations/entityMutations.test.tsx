import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { describe, expect, it, vi } from 'vitest';
import type { GetDeliveryHistoryResponse } from '../../src/api/generated/types.gen';
import type { PhysicalDeliveryDetailsDto } from '../../src/api/generated/types.gen';
import { deliveryQueryKey, useUpdateDelivery } from '../../src/lib/mutations/entityMutations';
import { useAuthStore } from '../../src/lib/state/useAuthStore';

const row = { deliveryId: 'del-1', contractId: 'c', contractInstanceId: 'i', bookType: 'Sales', supplyMonth: '2026-01-01', capacityMw: null, volumeNominatedMwh: null, volumeRealisedMwh: '34', volumeMwh: '34', priceMechanism: null, revenueEur: null, subtotalEur: null, vatEur: null, invoiceAmountEur: null, status: 'Pending - No Invoice', version: 7, createdAt: '', updatedAt: '' } as unknown as PhysicalDeliveryDetailsDto;

describe('useUpdateDelivery', () => {
  it('rolls back the paged cache and surfaces a 409 conflict', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    client.setQueryData<GetDeliveryHistoryResponse>(deliveryQueryKey, { items: [row], totalCount: 1, page: 1, pageSize: 50, hasNextPage: false });
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(row), { status: 409, headers: { 'Content-Type': 'application/json' } })));
    const conflict = vi.fn();
    let mutateAsync!: ReturnType<typeof useUpdateDelivery>['mutateAsync'];
    function Harness() { mutateAsync = useUpdateDelivery(conflict).mutateAsync; return null; }
    const host = document.createElement('div');
    const root = createRoot(host);
    await act(async () => root.render(<QueryClientProvider client={client}><Harness /></QueryClientProvider>));
    await act(async () => { await expect(mutateAsync({ id: 'del-1', version: 7, changes: { volumeRealisedMwh: '40', status: 'In Progress - Invoice Received/Sent' } })).rejects.toBeDefined(); });
    expect(conflict).toHaveBeenCalledWith('del-1', row);
    expect(client.getQueryData<GetDeliveryHistoryResponse>(deliveryQueryKey)?.items[0].volumeRealisedMwh).toBe('34');
    await act(async () => root.unmount());
    vi.unstubAllGlobals();
  });

  it('does not repopulate cache after the same account starts a new session epoch', async () => {
    useAuthStore.getState().setSession({ actorId: 'actor', accountKey: 'account' });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    client.setQueryData<GetDeliveryHistoryResponse>(deliveryQueryKey, { items: [row], totalCount: 1, page: 1, pageSize: 50, hasNextPage: false });
    let resolve!: (response: Response) => void;
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((done) => { resolve = done; })));
    let mutateAsync!: ReturnType<typeof useUpdateDelivery>['mutateAsync'];
    function Harness() { mutateAsync = useUpdateDelivery(vi.fn()).mutateAsync; return null; }
    const root = createRoot(document.createElement('div'));
    await act(async () => root.render(<QueryClientProvider client={client}><Harness /></QueryClientProvider>));

    let mutation!: ReturnType<typeof mutateAsync>;
    await act(async () => {
      mutation = mutateAsync({ id: row.deliveryId, version: row.version, changes: { volumeRealisedMwh: '40', status: row.status } });
      await vi.waitFor(() => expect(fetch).toHaveBeenCalledOnce());
    });
    client.clear();
    useAuthStore.getState().clearSession();
    useAuthStore.getState().setSession({ actorId: 'actor', accountKey: 'account' });
    await act(async () => {
      resolve(new Response(JSON.stringify({ ...row, volumeRealisedMwh: '40', version: 8 }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }));
      await mutation;
    });

    expect(client.getQueryCache().getAll()).toHaveLength(0);
    await act(async () => root.unmount());
    useAuthStore.getState().clearSession();
    vi.unstubAllGlobals();
  });

  it('serializes and rebases queued delivery field edits', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidateQueries = vi.spyOn(client, 'invalidateQueries');
    client.setQueryData<GetDeliveryHistoryResponse>(deliveryQueryKey, { items: [row], totalCount: 1, page: 1, pageSize: 50, hasNextPage: false });
    const pending: Array<{ body: Record<string, unknown>; resolve: (response: Response) => void }> = [];
    vi.stubGlobal('fetch', vi.fn((_input: RequestInfo | URL, init?: RequestInit) => new Promise<Response>((resolve) => {
      pending.push({ body: JSON.parse(String(init?.body)) as Record<string, unknown>, resolve });
    })));
    let mutateAsync!: ReturnType<typeof useUpdateDelivery>['mutateAsync'];
    function Harness() { mutateAsync = useUpdateDelivery(vi.fn()).mutateAsync; return null; }
    const root = createRoot(document.createElement('div'));
    await act(async () => root.render(<QueryClientProvider client={client}><Harness /></QueryClientProvider>));

    let first!: ReturnType<typeof mutateAsync>;
    let second!: ReturnType<typeof mutateAsync>;
    await act(async () => {
      first = mutateAsync({
        id: row.deliveryId,
        version: row.version,
        changes: { volumeRealisedMwh: '40', status: row.status },
        intent: ['volumeRealisedMwh'],
      });
      await vi.waitFor(() => expect(pending).toHaveLength(1));
      second = mutateAsync({
        id: row.deliveryId,
        version: row.version,
        changes: { volumeRealisedMwh: row.volumeRealisedMwh, status: 'In Progress - Invoice Received/Sent' },
        intent: ['status'],
      });
    });
    expect(pending).toHaveLength(1);

    await act(async () => {
      pending[0].resolve(new Response(JSON.stringify({ ...row, volumeRealisedMwh: '40', version: 8 }), {
        status: 200, headers: { 'Content-Type': 'application/json' },
      }));
      await vi.waitFor(() => expect(pending).toHaveLength(2));
    });
    expect(pending[1].body).toMatchObject({
      version: 8,
      volumeRealisedMwh: '40',
      status: 'In Progress - Invoice Received/Sent',
    });
    await act(async () => {
      pending[1].resolve(new Response(JSON.stringify({
        ...row,
        volumeRealisedMwh: '40',
        status: 'In Progress - Invoice Received/Sent',
        version: 9,
      }), { status: 200, headers: { 'Content-Type': 'application/json' } }));
      await Promise.all([first, second]);
    });
    expect(invalidateQueries).not.toHaveBeenCalled();

    await act(async () => root.unmount());
    vi.unstubAllGlobals();
  });
});
