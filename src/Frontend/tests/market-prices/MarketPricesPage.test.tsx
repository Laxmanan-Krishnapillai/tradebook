import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MarketPricesPage } from '../../src/components/market-prices/MarketPricesPage';
import { CommandStackProvider } from '../../src/lib/commands/CommandStackContext';

function jsonResponse(payload: unknown, status = 200) {
  return new Response(JSON.stringify(payload), { status, headers: { 'Content-Type': 'application/json' } });
}

function stubMarketPricesApi() {
  const emptyPage = { items: [], totalCount: 0, page: 1, pageSize: 100, hasNextPage: false };
  const fetchMock = vi.fn((_input: RequestInfo | URL, init?: RequestInit) => {
    const method = (init?.method ?? 'GET').toUpperCase();
    if (method === 'GET') return Promise.resolve(jsonResponse(emptyPage));
    if (method === 'PUT') {
      const body = JSON.parse(String(init?.body)) as Record<string, unknown>;
      return Promise.resolve(jsonResponse({ ...body, version: 1 }));
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
        <MarketPricesPage />
      </CommandStackProvider>
    </QueryClientProvider>,
  );
  return queryClient;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('MarketPricesPage create modal', () => {
  it('submits the create form and sends Money fields as wire strings', async () => {
    const fetchMock = stubMarketPricesApi();
    const queryClient = renderPage();
    await screen.findByText('0 daily observations');
    fireEvent.click(screen.getByTestId('btn-create-market-price'));
    fireEvent.change(screen.getByLabelText('Date'), { target: { value: '2026-01-05' } });
    fireEvent.change(screen.getByLabelText('TTF EUR/MWh'), { target: { value: '31.5' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'PUT')).toBe(true));
    const [url, init] = fetchMock.mock.calls.find(([, request]) => request?.method === 'PUT')!;
    expect(String(url)).toContain('/api/v1/market-prices/2026-01-05');
    const body = JSON.parse(String(init?.body)) as Record<string, unknown>;
    expect(body.priceDate).toBe('2026-01-05');
    expect(body.ttfEurMwh).toBe('31.5');
    expect(body.version).toBe(0);
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Add market price' })).toBeNull());
    queryClient.clear();
  });

  it('renders the error summary for an invalid create, never calls the API, and recovers', async () => {
    const fetchMock = stubMarketPricesApi();
    const queryClient = renderPage();
    await screen.findByText('0 daily observations');
    fireEvent.click(screen.getByTestId('btn-create-market-price'));
    fireEvent.change(screen.getByLabelText('TTF EUR/MWh'), { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('TTF EUR/MWh is required.');
    expect(fetchMock.mock.calls.every(([, init]) => (init?.method ?? 'GET').toUpperCase() === 'GET')).toBe(true);
    fireEvent.change(screen.getByLabelText('TTF EUR/MWh'), { target: { value: '42' } });
    await waitFor(() => expect(screen.queryByRole('alert')).toBeNull());
    queryClient.clear();
  });
});
