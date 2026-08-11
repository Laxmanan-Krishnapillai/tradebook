import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CapacityBookingsPage } from '../../src/components/domain/DomainEntityPages';
import { CommandStackProvider } from '../../src/lib/commands/CommandStackContext';
import type { CapacityBookingDetailsDto } from '../../src/api/generated/types.gen';
import { queryKeys } from '../../src/lib/query/queryKeys';

interface PageResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  hasNextPage: boolean;
}

function jsonResponse(payload: unknown, status = 200) {
  return new Response(JSON.stringify(payload), { status, headers: { 'Content-Type': 'application/json' } });
}
function parseRequestBody(request?: RequestInit): Record<string, unknown> {
  return JSON.parse(String(request?.body ?? '{}')) as Record<string, unknown>;
}

function stubCapacityApi(rows: CapacityBookingDetailsDto[]) {
  const page: PageResponse<CapacityBookingDetailsDto> = {
    items: rows,
    totalCount: rows.length,
    page: 1,
    pageSize: 100,
    hasNextPage: false,
  };
  const fetchMock = vi.fn((_input: RequestInfo | URL, init?: RequestInit) => {
    const method = (init?.method ?? 'GET').toUpperCase();
    if (method === 'GET') {
      return Promise.resolve(jsonResponse(page));
    }
    if (method === 'PUT') {
      const body = JSON.parse(String(init?.body)) as Record<string, unknown>;
      return Promise.resolve(jsonResponse({ ...body, version: 2 }));
    }
    if (method === 'DELETE') {
      return Promise.resolve(jsonResponse({}, 204));
    }
    if (method === 'POST') {
      const body = JSON.parse(String(init?.body)) as Record<string, unknown>;
      return Promise.resolve(jsonResponse({ ...body, capacityBookingId: 'created', version: 1, createdAt: '2026-01-01T00:00:00Z', updatedAt: '2026-01-01T00:00:00Z' }));
    }
    return Promise.resolve(jsonResponse({}, 500));
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <CommandStackProvider>
        <CapacityBookingsPage />
      </CommandStackProvider>
    </QueryClientProvider>,
  );
  return queryClient;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('CapacityBookingsPage domain workspace', () => {
  const rows: CapacityBookingDetailsDto[] = [
    {
      capacityBookingId: 'cb-1',
      contractId: 'c1',
      contractInstanceId: 'Instance-Alpha',
      supplyMonth: '2026-01-01',
      version: 1,
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
    },
    {
      capacityBookingId: 'cb-2',
      contractId: 'c2',
      contractInstanceId: 'Instance-Beta',
      supplyMonth: '2026-01-02',
      version: 1,
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
    },
  ];

  it('filters by search term and reports visible count', async () => {
    stubCapacityApi(rows);
    renderPage();
    await screen.findByText('2 records');
    fireEvent.change(screen.getByLabelText('Search Capacity bookings'), { target: { value: 'beta' } });
    expect(screen.getByText('Instance-Beta')).toBeTruthy();
    expect(screen.queryByText('Instance-Alpha')).toBeNull();
    expect(screen.getByText('1 of 2 records')).toBeTruthy();
    expect(screen.getByRole('checkbox', { name: 'Select all records' })).toBeTruthy();
  });

  it('opens the edit panel, saves and deletes the selected entity', async () => {
    const fetchMock = stubCapacityApi(rows);
    renderPage();
    await screen.findByText('2 records');
    fireEvent.click(screen.getByRole('button', { name: 'Open Capacity bookings Instance-Alpha' }));
    const dialog = screen.getByRole('heading', { name: 'Capacity bookings Instance-Alpha' }).closest<HTMLElement>('[role="dialog"]');
    expect(dialog).toBeTruthy();
    fireEvent.change(within(dialog!).getByLabelText('Start area'), { target: { value: 'NO1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(fetchMock.mock.calls.some(([, init]) => (init?.method ?? 'GET').toUpperCase() === 'PUT')).toBe(true));
    const [saveUrl, saveInit] = fetchMock.mock.calls.find(([, request]) => (request?.method ?? 'GET').toUpperCase() === 'PUT')!;
    expect(String(saveUrl)).toContain('/api/v1/capacity-bookings/cb-1');
    const saveBody = parseRequestBody(saveInit);
    expect(saveBody.startArea).toBe('NO1');
    fireEvent.click(screen.getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(fetchMock.mock.calls.some(([, init]) => (init?.method ?? 'GET').toUpperCase() === 'DELETE')).toBe(true));
    const [deleteUrl, deleteInit] = fetchMock.mock.calls.find(([, request]) => (request?.method ?? 'GET').toUpperCase() === 'DELETE')!;
    expect(String(deleteUrl)).toContain('/api/v1/capacity-bookings/cb-1');
    expect(parseRequestBody(deleteInit).reason).toBe('Deleted from Tradebook UI');
  });

  it('edits mutable cells inline and leaves immutable cells read-only', async () => {
    const fetchMock = stubCapacityApi(rows);
    renderPage();
    await screen.findByText('2 records');

    expect(screen.getAllByTitle('Month is read-only').length).toBe(2);
    const startArea = screen.getAllByRole('textbox', { name: 'Start area' })[0];
    fireEvent.click(startArea);
    fireEvent.change(startArea, { target: { value: 'NO2' } });
    fireEvent.keyDown(startArea, { key: 'Enter' });

    await waitFor(() => expect(fetchMock.mock.calls.some(([, init]) => (init?.method ?? 'GET').toUpperCase() === 'PUT')).toBe(true));
    const [, saveInit] = fetchMock.mock.calls.find(([, init]) => (init?.method ?? 'GET').toUpperCase() === 'PUT')!;
    expect(parseRequestBody(saveInit).startArea).toBe('NO2');
  });

  it('preserves a dirty generic detail draft across refreshes and adopts its matching result', async () => {
    stubCapacityApi(rows);
    const queryClient = renderPage();
    await screen.findByText('2 records');
    fireEvent.click(screen.getByRole('button', { name: 'Open Capacity bookings Instance-Alpha' }));
    const dialog = screen.getByRole('heading', { name: 'Capacity bookings Instance-Alpha' }).closest<HTMLElement>('[role="dialog"]');
    expect(dialog).toBeTruthy();
    fireEvent.change(within(dialog!).getByLabelText('Start area'), { target: { value: 'NO1' } });

    const key = queryKeys.capacityBookings.list({ page: 1, pageSize: 100 });
    queryClient.setQueryData(key, {
      items: [{ ...rows[0], startArea: 'REMOTE', version: 2 }, rows[1]],
      totalCount: 2, page: 1, pageSize: 100, hasNextPage: false,
    });
    await waitFor(() => expect((within(dialog!).getByLabelText('Start area') as HTMLInputElement).value).toBe('NO1'));
    expect(screen.getByText('Unsaved')).toBeTruthy();

    queryClient.setQueryData(key, {
      items: [{ ...rows[0], startArea: 'NO1', version: 3 }, rows[1]],
      totalCount: 2, page: 1, pageSize: 100, hasNextPage: false,
    });
    await waitFor(() => expect(screen.getByText('v3')).toBeTruthy());
    expect(screen.queryByText('Unsaved')).toBeNull();
  });
});
