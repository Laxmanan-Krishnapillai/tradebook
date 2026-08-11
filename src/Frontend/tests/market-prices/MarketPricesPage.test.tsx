import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MarketPricesPage } from '../../src/components/market-prices/MarketPricesPage';
import { CommandStackProvider } from '../../src/lib/commands/CommandStackContext';
import { queryKeys } from '../../src/lib/query/queryKeys';
import { replaceAnimatedNumber } from '../helpers/animatedNumberInput';

interface MarketPriceRow {
  priceDate: string;
  ttfEurMwh?: string | null;
  egsiEtfEurMwh?: string | null;
  theEurMwh?: string | null;
  bgoEurMwh?: string | null;
  pgoEurMwh?: string | null;
  euaEurMwh?: string | null;
  withinDayMktEurMwh?: string | null;
  eurSek?: string | null;
  eurChf?: string | null;
  eurGbp?: string | null;
  eurUsd?: string | null;
  eurDkk?: string | null;
  version: number;
  createdAt: string;
}

async function changeAnimatedNumber(label: string, value: string) {
  const editor = screen.getByRole('textbox', { name: label });
  await replaceAnimatedNumber(editor, value);
}

function jsonResponse(payload: unknown, status = 200) {
  return new Response(JSON.stringify(payload), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}
function parseRequestBody(request?: RequestInit) {
  return JSON.parse(String(request?.body ?? '{}')) as Record<string, unknown>;
}

function emptyMarketResponse() {
  return { items: [], totalCount: 0, page: 1, pageSize: 100, hasNextPage: false };
}

function responseWithRows(rows: MarketPriceRow[]) {
  return {
    items: rows,
    totalCount: rows.length,
    page: 1,
    pageSize: 100,
    hasNextPage: false,
  };
}

function stubMarketPricesApi(rows: MarketPriceRow[] = []) {
  const fetchMock = vi.fn((_input: RequestInfo | URL, init?: RequestInit) => {
    const method = (init?.method ?? 'GET').toUpperCase();
    if (method === 'GET') {
      return Promise.resolve(jsonResponse(responseWithRows(rows)));
    }
    if (method === 'PUT') {
      const body = JSON.parse(String(init?.body)) as Record<string, unknown>;
      return Promise.resolve(jsonResponse({ ...body, version: 1 }));
    }
    if (method === 'DELETE') {
      return Promise.resolve(jsonResponse({}, 204));
    }
    return Promise.resolve(jsonResponse({}, 500));
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
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
    const fetchMock = stubMarketPricesApi([]);
    const queryClient = renderPage();
    await screen.findByText('0 daily observations');
    fireEvent.click(screen.getByTestId('btn-create-market-price'));
    fireEvent.change(screen.getByLabelText('Date'), { target: { value: '2026-01-05' } });
    await changeAnimatedNumber('TTF EUR/MWh', '31.5');
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(fetchMock.mock.calls.some(([, init]) => (init?.method ?? 'GET').toUpperCase() === 'PUT')).toBe(true));
    const [url, init] = fetchMock.mock.calls.find(([, request]) => (request?.method ?? 'GET').toUpperCase() === 'PUT')!;
    expect(String(url)).toContain('/api/v1/market-prices/2026-01-05');
    const body = parseRequestBody(init);
    expect(body.priceDate).toBe('2026-01-05');
    expect(body.ttfEurMwh).toBe('31.5');
    expect(body.version).toBe(0);
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Add market price' })).toBeNull());
    queryClient.clear();
  });

  it('renders an error for an invalid create, never calls the API, and recovers', async () => {
    const fetchMock = stubMarketPricesApi([]);
    const queryClient = renderPage();
    await screen.findByText('0 daily observations');
    fireEvent.click(screen.getByTestId('btn-create-market-price'));
    await changeAnimatedNumber('TTF EUR/MWh', '');
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('TTF EUR/MWh is required.');
    expect(fetchMock.mock.calls.every(([, init]) => (init?.method ?? 'GET').toUpperCase() === 'GET')).toBe(true);
    await changeAnimatedNumber('TTF EUR/MWh', '42');
    await waitFor(() => expect(screen.queryByRole('alert')).toBeNull());
    queryClient.clear();
  });
});

describe('MarketPricesPage list workspace', () => {
  it('supports search filter and row selection', async () => {
    const rows: MarketPriceRow[] = [
      { priceDate: '2026-01-01', ttfEurMwh: '30', euaEurMwh: '8', eurUsd: '1.05', version: 1, createdAt: '2026-01-01T00:00:00Z' },
      { priceDate: '2026-01-02', ttfEurMwh: '31', euaEurMwh: '9', eurUsd: '1.06', version: 1, createdAt: '2026-01-02T00:00:00Z' },
    ];
    stubMarketPricesApi(rows);
    renderPage();
    await screen.findByText('2 daily observations');
    expect(screen.getByRole('checkbox', { name: 'Select all records' })).toBeTruthy();

    fireEvent.change(screen.getByLabelText('Search market prices'), { target: { value: '2026-01-02' } });
    expect(screen.getByText('2026-01-02')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Open market price 2026-01-01' })).toBeNull();
    expect(screen.getByText('1 of 2 daily observations')).toBeTruthy();
    fireEvent.change(screen.getByLabelText('Search market prices'), { target: { value: '' } });
    expect(screen.getByText('2 of 2 daily observations')).toBeTruthy();
    fireEvent.click(screen.getByRole('checkbox', { name: 'Select record 2026-01-02' }));
    expect(screen.getByLabelText('Selection').textContent).toBe('1 selected');
  });

  it('opens a panel and saves through the panel form', async () => {
    const rows: MarketPriceRow[] = [
      { priceDate: '2026-01-03', ttfEurMwh: '33', euaEurMwh: '11', version: 2, createdAt: '2026-01-03T00:00:00Z' },
    ];
    const fetchMock = stubMarketPricesApi(rows);
    renderPage();
    await screen.findByText('2026-01-03');
    fireEvent.click(screen.getByRole('button', { name: 'Open market price 2026-01-03' }));
    expect(screen.getByRole('heading', { name: 'Update market price 2026-01-03' })).toBeTruthy();
    await changeAnimatedNumber('TTF EUR/MWh', '44.4');
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(fetchMock.mock.calls.some(([, init]) => (init?.method ?? 'GET').toUpperCase() === 'PUT')).toBe(true));
    const [url, init] = fetchMock.mock.calls.find(([, request]) => (request?.method ?? 'GET').toUpperCase() === 'PUT')!;
    expect(String(url)).toContain('/api/v1/market-prices/2026-01-03');
    const body = parseRequestBody(init);
    expect(body.ttfEurMwh).toBe('44.4');
    expect(body.version).toBe(2);
    expect(screen.queryByRole('heading', { name: 'Update market price 2026-01-03' })).toBeTruthy();
  });

  it('preserves a dirty price draft and adopts its normalized successful value', async () => {
    const price: MarketPriceRow = {
      priceDate: '2026-01-03', ttfEurMwh: '33', euaEurMwh: '11', version: 2, createdAt: '2026-01-03T00:00:00Z',
    };
    stubMarketPricesApi([price]);
    const queryClient = renderPage();
    await screen.findByText('2026-01-03');
    fireEvent.click(screen.getByRole('button', { name: 'Open market price 2026-01-03' }));
    await changeAnimatedNumber('TTF EUR/MWh', '44.4');

    const key = queryKeys.marketPrices.list({ page: 1, pageSize: 100 });
    queryClient.setQueryData(key, responseWithRows([{ ...price, ttfEurMwh: '40', version: 3 }]));
    await waitFor(() => expect((screen.getByRole('textbox', { name: 'TTF EUR/MWh' }) as HTMLElement).textContent).toContain('44.4'));
    expect(screen.getByText('Unsaved')).toBeTruthy();

    queryClient.setQueryData(key, responseWithRows([{ ...price, ttfEurMwh: '44.400000', version: 4 }]));
    await waitFor(() => expect(screen.getByText('v4')).toBeTruthy());
    expect(screen.queryByText('Unsaved')).toBeNull();
  });
});
