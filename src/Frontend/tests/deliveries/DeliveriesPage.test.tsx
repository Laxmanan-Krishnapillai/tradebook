import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DeliveriesPage } from '../../src/components/deliveries/DeliveriesPage';
import { CommandStackProvider } from '../../src/lib/commands/CommandStackContext';
import { useUiStore } from '../../src/lib/state/useUiStore';
import type { PhysicalDeliveryDetailsDto } from '../../src/api/generated/types.gen';
import { replaceAnimatedNumber } from '../helpers/animatedNumberInput';

function jsonResponse(payload: unknown, status = 200) {
  return new Response(JSON.stringify(payload), { status, headers: { 'Content-Type': 'application/json' } });
}

const now = new Date().toISOString();
async function changeAnimatedNumber(label: string, value: string) {
  const editor = screen.getByRole('textbox', { name: label });
  await replaceAnimatedNumber(editor, value);
}

const sampleDeliveries: PhysicalDeliveryDetailsDto[] = [
  {
    deliveryId: 'del-1',
    contractId: 'C-100',
    contractInstanceId: 'INST-ALPHA',
    bookType: 'Sourcing',
    supplyMonth: '2026-01-01',
    volumeRealisedMwh: '12.5',
    volumeMwh: '12.5',
    status: 'Pending - No Invoice',
    version: 4,
    createdAt: now,
    updatedAt: now,
    capacityMw: null,
    volumeNominatedMwh: null,
    priceMechanism: null,
    revenueEur: null,
    subtotalEur: null,
    vatEur: null,
    invoiceAmountEur: null,
  } as PhysicalDeliveryDetailsDto,
  {
    deliveryId: 'del-2',
    contractId: 'C-200',
    contractInstanceId: 'INST-BETA',
    bookType: 'Sales',
    supplyMonth: '2026-01-15',
    volumeRealisedMwh: '3.0',
    volumeMwh: '3.0',
    status: 'Completed - Payment Received/Sent',
    version: 1,
    createdAt: now,
    updatedAt: now,
    capacityMw: null,
    volumeNominatedMwh: null,
    priceMechanism: null,
    revenueEur: null,
    subtotalEur: null,
    vatEur: null,
    invoiceAmountEur: null,
  } as PhysicalDeliveryDetailsDto,
];

function stubDeliveriesApi() {
  const listPage = {
    items: sampleDeliveries,
    totalCount: sampleDeliveries.length,
    page: 1,
    pageSize: 100,
    hasNextPage: false,
  };
  const fetchMock = vi.fn((_input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(_input);
    const method = (init?.method ?? 'GET').toUpperCase();
    if (method === 'GET' && url.includes('/api/v1/contracts')) {
      return Promise.resolve(jsonResponse({
        items: [{ contractId: 'C-100', contractName: 'Northern Supply 2026', productType: 'Gas', action: 'Buy' }],
        totalCount: 1,
        page: 1,
        pageSize: 100,
        hasNextPage: false,
      }));
    }
    if (method === 'GET') return Promise.resolve(jsonResponse(listPage));
    if (method === 'POST') {
      return Promise.resolve(jsonResponse({
        deliveryId: 'del-created-1',
        contractId: 'C-300',
        contractInstanceId: 'CI-300',
        bookType: 'Sales',
        supplyMonth: new Date().toISOString().slice(0, 10),
        invoiceAmountEur: '0',
        status: 'Pending - No Invoice',
        version: 1,
        createdAt: now,
        updatedAt: now,
      }, 201));
    }
    if (method === 'PUT') {
      const body = JSON.parse(String(init?.body)) as Record<string, unknown>;
      return Promise.resolve(jsonResponse({ ...body, deliveryId: 'del-1', version: 5 }));
    }
    if (method === 'DELETE') {
      return Promise.resolve(jsonResponse({}, 200));
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

describe('DeliveriesPage interactions', () => {
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
    await screen.findByText('2 records');
    fireEvent.click(screen.getByTestId('btn-create-delivery'));
    fireEvent.click(screen.getByRole('button', { name: 'Show Contract options' }));
    fireEvent.click(await screen.findByText('Northern Supply 2026'));
    await changeAnimatedNumber('Realised volume MWh', '12.5');
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

  it('filters by search term and updates the visible count', async () => {
    stubDeliveriesApi();
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
    render(
      <QueryClientProvider client={queryClient}>
        <CommandStackProvider>
          <DeliveriesPage />
        </CommandStackProvider>
      </QueryClientProvider>,
    );
    await screen.findByText('2 records');
    expect(screen.getByText('2 of 2 deliveries')).toBeTruthy();
    fireEvent.change(screen.getByLabelText('Search deliveries'), { target: { value: 'INST-ALPHA' } });
    expect(screen.getByText('1 of 2 deliveries')).toBeTruthy();
    expect(screen.getByText('INST-ALPHA')).toBeTruthy();
    expect(screen.queryByText('INST-BETA')).toBeNull();
    queryClient.clear();
  });

  it('opens and closes the detail panel from a row interaction', async () => {
    stubDeliveriesApi();
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
    render(
      <QueryClientProvider client={queryClient}>
        <CommandStackProvider>
          <DeliveriesPage />
        </CommandStackProvider>
      </QueryClientProvider>,
    );
    const targetRow = await screen.findByText('INST-BETA').then((node) => node.closest('tr'));
    expect(targetRow).toBeTruthy();
    fireEvent.click(targetRow!);
    await screen.findByRole('dialog', { name: 'INST-BETA' });
    fireEvent.click(screen.getByRole('button', { name: 'Close panel' }));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'INST-BETA' })).toBeNull());
    queryClient.clear();
  });

  it('saves and cancels delivery changes through existing mutation actions', async () => {
    const fetchMock = stubDeliveriesApi();
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
    render(
      <QueryClientProvider client={queryClient}>
        <CommandStackProvider>
          <DeliveriesPage />
        </CommandStackProvider>
      </QueryClientProvider>,
    );
    await screen.findByText('2 records');
    const targetRow = screen.getByText('INST-ALPHA').closest('tr');
    expect(targetRow).toBeTruthy();
    fireEvent.click(targetRow!);
    await screen.findByRole('dialog', { name: 'INST-ALPHA' });
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'PUT')).toBe(true));
    const [putUrl, putInit] = fetchMock.mock.calls.find(([, request]) => request?.method === 'PUT')!;
    expect(String(putUrl)).toContain('/api/v1/deliveries/del-1');
    const updatedBody = JSON.parse(String(putInit?.body)) as Record<string, unknown>;
    expect(updatedBody.volumeRealisedMwh).toBe('12.5');
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    await waitFor(() => expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(true));
    const [, deleteInit] = fetchMock.mock.calls.find(([, request]) => request?.method === 'DELETE')!;
    const deletedBody = JSON.parse(String(deleteInit?.body)) as Record<string, unknown>;
    expect(deletedBody).toMatchObject({ reason: 'Cancelled from Tradebook UI' });
    queryClient.clear();
  });
});
