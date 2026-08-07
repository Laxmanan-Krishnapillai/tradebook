import { QueryClient } from '@tanstack/react-query';
import { describe, expect, it, vi } from 'vitest';
import type { PhysicalDeliveryDetailsDto } from '../../src/api/generated/physical-delivery-details-dto';
import { affectedQueryRoots, reconcileRealtimeBatch } from '../../src/hooks/useRealtimeQuerySync';
import { queryKeys } from '../../src/lib/query/queryKeys';
import { subscribedAggregateTypes, type EntityChangedEvent } from '../../src/lib/realtime/signalRClient';

const event = (aggregateType: string, aggregateId = `${aggregateType}-1`): EntityChangedEvent => ({
  eventId: crypto.randomUUID(),
  sequenceId: 1,
  aggregateType,
  aggregateId,
  eventType: 'Updated',
  payloadJson: JSON.stringify({ version: 2 }),
});

const delivery = {
  deliveryId: 'delivery-1', contractId: 'contract-1', contractInstanceId: 'instance-1', bookType: 'Sales',
  supplyMonth: '2026-01-01', status: 'Issue', version: 2, createdAt: '', updatedAt: '',
} as PhysicalDeliveryDetailsDto;

describe('realtime query synchronization', () => {
  it('has an explicit mapping for every subscribed aggregate and workspace dashboards', () => {
    expect(Object.keys(affectedQueryRoots).sort()).toEqual(
      [...subscribedAggregateTypes, 'WorkspaceDashboard'].sort(),
    );
    for (const aggregateType of subscribedAggregateTypes) {
      expect(affectedQueryRoots[aggregateType].length).toBeGreaterThan(0);
    }
  });

  it('deduplicates invalidation roots across a burst and ignores unknown aggregate types', async () => {
    const client = new QueryClient();
    const invalidate = vi.spyOn(client, 'invalidateQueries').mockResolvedValue();
    await reconcileRealtimeBatch(client, [event('Contract'), event('Contract', 'contract-2'), event('MarketPrice'), event('Unknown')]);
    const invalidated = invalidate.mock.calls.map(([filter]) => JSON.stringify(filter?.queryKey)).sort();
    expect(invalidated).toEqual([
      JSON.stringify(queryKeys.analytics.all),
      JSON.stringify(queryKeys.contracts.all),
      JSON.stringify(queryKeys.marketPrices.all),
    ].sort());
  });

  it('patches only known list membership and keeps filtered totals coherent', async () => {
    const client = new QueryClient();
    const included = queryKeys.deliveries.list({ filters: { status: 'Issue' } });
    const excluded = queryKeys.deliveries.list({ filters: { status: 'Awaiting' } });
    client.setQueryData(included, { items: [{ ...delivery, version: 1 }], totalCount: 1, page: 1, pageSize: 100, hasNextPage: false });
    client.setQueryData(excluded, { items: [], totalCount: 0, page: 1, pageSize: 100, hasNextPage: false });

    await reconcileRealtimeBatch(client, [event('PhysicalDelivery', delivery.deliveryId)], async () => delivery);

    expect(client.getQueryData<{ items: PhysicalDeliveryDetailsDto[]; totalCount: number }>(included)).toMatchObject({ items: [delivery], totalCount: 1 });
    expect(client.getQueryData<{ items: PhysicalDeliveryDetailsDto[]; totalCount: number }>(excluded)).toMatchObject({ items: [], totalCount: 0 });
  });

  it('does not install a late canonical response after the session signal is aborted', async () => {
    const client = new QueryClient();
    const controller = new AbortController();
    let resolve!: (value: PhysicalDeliveryDetailsDto) => void;
    const pending = new Promise<PhysicalDeliveryDetailsDto>((next) => { resolve = next; });
    const reconciliation = reconcileRealtimeBatch(
      client,
      [event('PhysicalDelivery', delivery.deliveryId)],
      () => pending,
      { signal: controller.signal },
    );
    controller.abort();
    resolve(delivery);
    await reconciliation;
    expect(client.getQueryData(queryKeys.deliveries.detail(delivery.deliveryId))).toBeUndefined();
  });
});
