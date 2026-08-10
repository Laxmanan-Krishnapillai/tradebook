import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DeliveriesPage } from '../../src/components/deliveries/DeliveriesPage';
import { CommandStackProvider } from '../../src/lib/commands/CommandStackContext';
import { useUiStore } from '../../src/lib/state/useUiStore';

function jsonResponse(payload: unknown, status = 200) {
  return new Response(JSON.stringify(payload), { status, headers: { 'Content-Type': 'application/json' } });
}

function stubDeliveriesApi() {
  const emptyPage = { items: [], totalCount: 0, page: 1, pageSize: 100, hasNextPage: false };
  const fetchMock = vi.fn((_input: RequestInfo | URL, init?: RequestInit) => {
    const method = (init?.method ?? 'GET').toUpperCase();
    if (method === 'GET') return Promise.resolve(jsonResponse(emptyPage));
    if (method === 'POST') {
      return Promise.resolve(jsonResponse({
        deliveryId: 'del-created-1',
        contractInstanceId: 'CI-1',
        invoiceAmountEur: '0',
        status: 'Pending - No Invoice',
        version: 1,
        createdAt: new Date().toISOString(),
      }, 201));
    }
    return Promise.resolve(jsonResponse({}, 500));
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

afterEach(() => {
  useUiStore.getState().reset();
  vi.unstubAllGlobals();
});

describe('DeliveriesPage create modal', () => {
  it('creates a delivery with a realised volume and sends it as a wire string', async () => {
    const fetchMock = stubDeliveriesApi();
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
    render(
      <QueryClientProvider client={queryClient}>
        <CommandStackProvider>
          <DeliveriesPage />
        </CommandStackProvider>
      </QueryClientProvider>,
    );
    await screen.findByText('0 records');
    fireEvent.click(screen.getByTestId('btn-create-delivery'));
    fireEvent.change(screen.getByLabelText('Contract ID'), { target: { value: 'C-100' } });
    fireEvent.change(screen.getByLabelText('Realised volume MWh'), { target: { value: '12.5' } });
    fireEvent.click(screen.getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(true));
    const [url, init] = fetchMock.mock.calls.find(([, request]) => request?.method === 'POST')!;
    expect(String(url)).toContain('/api/v1/deliveries');
    const body = JSON.parse(String(init?.body)) as Record<string, unknown>;
    expect(body.contractId).toBe('C-100');
    expect(body.volumeRealisedMwh).toBe('12.5');
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Create physical delivery' })).toBeNull());
    queryClient.clear();
  });
});
