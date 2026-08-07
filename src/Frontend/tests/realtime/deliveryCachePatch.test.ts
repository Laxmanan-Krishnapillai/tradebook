import { QueryClient } from '@tanstack/react-query';
import { describe, expect, it, vi } from 'vitest';
import type { PhysicalDeliveryDetailsDto } from '../../src/api/generated/physical-delivery-details-dto';
import { reconcileRealtimeEvent } from '../../src/hooks/useRealtimeQuerySync';
import { ApiError } from '../../src/lib/api/client';
import { queryKeys } from '../../src/lib/query/queryKeys';

const event = { eventId: 'event', sequenceId: 2, aggregateType: 'PhysicalDelivery', aggregateId: 'delivery', eventType: 'Updated', payloadJson: JSON.stringify({ aggregateId: 'delivery', version: 3 }) };
const canonical = { deliveryId: 'delivery', contractId: 'contract', contractInstanceId: 'contract-1-2026', bookType: 'Sales', supplyMonth: '2026-01-01', status: 'Cancelled', version: 3, createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-02T00:00:00Z' } as PhysicalDeliveryDetailsDto;

describe('realtime cache reconciliation', () => {
  it('loads canonical server state for the sparse payload emitted by RepositoryMutation', async () => {
    const client = new QueryClient();
    const key = queryKeys.deliveries.list({ page: 1, pageSize: 50 });
    client.setQueryData(key, { items: [{ ...canonical, status: 'Pending - No Invoice', version: 2 }], totalCount: 1, page: 1, pageSize: 50, hasNextPage: false });
    const loader = vi.fn(async () => canonical);
    await reconcileRealtimeEvent(client, event, loader);
    expect(loader).toHaveBeenCalledWith('delivery', undefined);
    expect(client.getQueryData<{ items: PhysicalDeliveryDetailsDto[] }>(key)?.items[0]).toEqual(canonical);
  });

  it('does not overwrite a mutation response that is newer than a replayed event', async () => {
    const client = new QueryClient();
    const key = queryKeys.deliveries.list({ page: 1, pageSize: 50 });
    client.setQueryData(key, { items: [{ ...canonical, version: 4 }], totalCount: 1, page: 1, pageSize: 50, hasNextPage: false });
    const loader = vi.fn(async () => canonical);
    await reconcileRealtimeEvent(client, event, loader);
    expect(loader).not.toHaveBeenCalled();
    expect(client.getQueryData<{ items: PhysicalDeliveryDetailsDto[] }>(key)?.items[0].version).toBe(4);
  });

  it('invalidates the matching domain and analytics caches for non-delivery events', async () => {
    const client = new QueryClient();
    client.setQueryData(['contracts'], { items: [] });
    client.setQueryData(['analytics', 'widget'], { rows: [] });
    await reconcileRealtimeEvent(client, { ...event, aggregateType: 'Contract', aggregateId: 'contract' });
    expect(client.getQueryState(['contracts'])?.isInvalidated).toBe(true);
    expect(client.getQueryState(['analytics', 'widget'])?.isInvalidated).toBe(true);
  });

  it('does not invent filtered-list membership while reconciling created and deleted rows', async () => {
    const client = new QueryClient();
    const other = { ...canonical, deliveryId: 'other' };
    const first = queryKeys.deliveries.list({ page: 1, pageSize: 1, filters: { status: 'Issue' } });
    const second = queryKeys.deliveries.list({ page: 2, pageSize: 1, filters: { status: 'Issue' } });
    client.setQueryData(first, { items: [], totalCount: 1, page: 1, pageSize: 1, hasNextPage: false });
    client.setQueryData(second, { items: [other], totalCount: 1, page: 2, pageSize: 1, hasNextPage: false });

    await reconcileRealtimeEvent(client, { ...event, eventType: 'Created', payloadJson: JSON.stringify({ version: 3 }) }, async () => canonical);
    expect(client.getQueryData<{ items: PhysicalDeliveryDetailsDto[]; totalCount: number }>(first)).toMatchObject({ items: [], totalCount: 1 });
    expect(client.getQueryData<{ items: PhysicalDeliveryDetailsDto[]; totalCount: number }>(second)).toMatchObject({ items: [other], totalCount: 1 });

    await reconcileRealtimeEvent(client, { ...event, eventType: 'Deleted', payloadJson: JSON.stringify({ version: 4 }) }, async () => { throw new ApiError(404); });
    expect(client.getQueryData<{ totalCount: number }>(first)?.totalCount).toBe(1);
    expect(client.getQueryData<{ items: PhysicalDeliveryDetailsDto[]; totalCount: number }>(second)).toMatchObject({ items: [other], totalCount: 1 });
  });
});
