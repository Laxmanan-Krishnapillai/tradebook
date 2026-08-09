import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { describe, expect, it, vi } from 'vitest';
import type { GetDeliveryHistoryResponse } from '../../src/api/generated/types.gen';
import type { PhysicalDeliveryDetailsDto } from '../../src/api/generated/types.gen';
import { deliveryQueryKey, useUpdateDelivery } from '../../src/lib/mutations/entityMutations';

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
});
