import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { describe, expect, it, vi } from 'vitest';
import { useUpdateDelivery } from '../../src/lib/mutations/entityMutations';
const row = { deliveryId: 'del-1', contractId: 'c', contractInstanceId: 'i', bookType: 'Sales', supplyMonth: '2026-01-01', capacityMw: null, volumeNominatedMwh: null, volumeRealisedMwh: 34, volumeMwh: 34, priceMechanism: null, revenueEur: null, subtotalEur: null, vatEur: null, invoiceAmountEur: null, status: 'Pending - No Invoice', version: 7, createdAt: '', updatedAt: '' };
describe('useUpdateDelivery', () => {
  it('rolls back and surfaces a 409 conflict', async () => { const client = new QueryClient({ defaultOptions: { queries: { retry: false } } }); client.setQueryData(['deliveries'], [row]); vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(row), { status: 409 }))); const conflict = vi.fn(); let mutate!: ReturnType<typeof useUpdateDelivery>['mutate']; function Harness() { mutate = useUpdateDelivery(conflict).mutate; return null; } const host = document.createElement('div'); const root = createRoot(host); await act(async () => root.render(<QueryClientProvider client={client}><Harness /></QueryClientProvider>)); act(() => mutate({ id: 'del-1', version: 7, changes: { volumeRealisedMwh: 40, status: 'Invoice Received' } })); await vi.waitFor(() => expect(conflict).toHaveBeenCalledWith('del-1', row)); expect(client.getQueryData<typeof row[]>(['deliveries'])?.[0].volumeRealisedMwh).toBe(34); root.unmount(); vi.unstubAllGlobals(); });
});
