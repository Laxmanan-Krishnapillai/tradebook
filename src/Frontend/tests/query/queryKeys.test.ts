import { QueryClient } from '@tanstack/react-query';
import { describe, expect, it } from 'vitest';
import { normalizeListRequest, queryKeys } from '../../src/lib/query/queryKeys';

describe('query key factories', () => {
  it('normalizes filter member and array ordering without collapsing sort priority', () => {
    const first = queryKeys.deliveries.list({
      page: 2,
      pageSize: 50,
      filters: { status: ['Issue', 'Awaiting'], bookType: 'Sales' },
      sort: [{ member: 'supplyMonth', direction: 'desc' }, { member: 'status', direction: 'asc' }],
    });
    const second = queryKeys.deliveries.list({
      pageSize: 50,
      page: 2,
      filters: { bookType: 'Sales', status: ['Awaiting', 'Issue'] },
      sort: [{ member: 'supplyMonth', direction: 'desc' }, { member: 'status', direction: 'asc' }],
    });
    expect(first).toEqual(second);
    expect(normalizeListRequest({ sort: [{ member: ' status ', direction: 'asc' }] }).sort).toEqual([
      { member: 'status', direction: 'asc' },
    ]);
  });

  it('keeps pages, filters, lists, and details distinct', () => {
    expect(queryKeys.contracts.list({ page: 1 })).not.toEqual(queryKeys.contracts.list({ page: 2 }));
    expect(queryKeys.contracts.list({ filters: { isActive: true } })).not.toEqual(
      queryKeys.contracts.list({ filters: { isActive: false } }),
    );
    expect(queryKeys.contracts.detail('contract-1')).not.toEqual(queryKeys.contracts.lists());
  });

  it('allows a root invalidation to reach every list and detail for that entity', async () => {
    const client = new QueryClient();
    const first = queryKeys.transfers.list({ page: 1 });
    const second = queryKeys.transfers.list({ page: 2 });
    const detail = queryKeys.transfers.detail('transfer-1');
    client.setQueryData(first, { items: [] });
    client.setQueryData(second, { items: [] });
    client.setQueryData(detail, { transferId: 'transfer-1' });
    client.setQueryData(queryKeys.contracts.list(), { items: [] });

    await client.invalidateQueries({ queryKey: queryKeys.transfers.all });

    expect(client.getQueryState(first)?.isInvalidated).toBe(true);
    expect(client.getQueryState(second)?.isInvalidated).toBe(true);
    expect(client.getQueryState(detail)?.isInvalidated).toBe(true);
    expect(client.getQueryState(queryKeys.contracts.list())?.isInvalidated).toBe(false);
  });
});
