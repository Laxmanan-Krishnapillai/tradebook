import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createMemoryHistory, RouterProvider } from '@tanstack/react-router';
import type { Meta, StoryObj } from '@storybook/react-vite';
import { useEffect, useMemo } from 'react';
import { tokenProvider } from '../../../src/lib/auth/tokenProvider';
import type { CreatePhysicalDeliveryRequest, GetDeliveryHistoryResponse, PhysicalDeliveryDetailsDto, UpdatePhysicalDeliveryRequest } from '../../../src/api/generated/types.gen';
import { queryKeys } from '../../../src/lib/query/queryKeys';
import { createAppRouter } from '../../../src/app/router';

const sampleDeliveries: PhysicalDeliveryDetailsDto[] = [
  {
    deliveryId: 'del-101',
    contractId: 'C-1001',
    contractInstanceId: 'PLN-ALPHA',
    bookType: 'Sourcing',
    supplyMonth: '2026-01-01',
    capacityMw: '5',
    volumeNominatedMwh: '10',
    volumeRealisedMwh: '8.5',
    priceMechanism: 'FIXED',
    startDay: null,
    endDay: null,
    status: 'Pending - No Invoice',
    version: 1,
    createdAt: '2026-08-01T12:00:00Z',
    updatedAt: '2026-08-01T12:00:00Z',
    volumeMwh: '8.5',
    revenueEur: null,
    subtotalEur: null,
    vatEur: null,
    invoiceAmountEur: null,
    invoiceNumber: null,
  },
  {
    deliveryId: 'del-102',
    contractId: 'C-2002',
    contractInstanceId: 'PLN-BETA',
    bookType: 'Sales',
    supplyMonth: '2026-01-15',
    capacityMw: '6',
    volumeNominatedMwh: '12',
    volumeRealisedMwh: '11',
    priceMechanism: 'FIXED',
    startDay: null,
    endDay: null,
    status: 'Completed - Payment Received/Sent',
    version: 3,
    createdAt: '2026-08-04T09:00:00Z',
    updatedAt: '2026-08-04T09:00:00Z',
    volumeMwh: '11',
    revenueEur: null,
    subtotalEur: null,
    vatEur: null,
    invoiceAmountEur: null,
    invoiceNumber: null,
  },
];

const deliveryStatuses = [
  'In Progress - Invoice Received/Sent',
  'Pending - No Invoice',
  'Awaiting',
  'Issue',
  'Completed - Payment Received/Sent',
] as const;

for (let index = 3; index <= 20; index += 1) {
  const source = sampleDeliveries[index % 2];
  sampleDeliveries.push({
    ...source,
    deliveryId: `del-${100 + index}`,
    contractId: `C-${1000 + index}`,
    contractInstanceId: `CI-2026-${String(400 + index).padStart(4, '0')}`,
    bookType: index % 3 === 0 ? 'Intercompany' : index % 2 === 0 ? 'Sales' : 'Sourcing',
    supplyMonth: `2026-${String(((index - 1) % 12) + 1).padStart(2, '0')}-01`,
    volumeRealisedMwh: String(3_800 + index * 425),
    volumeMwh: String(3_800 + index * 425),
    invoiceAmountEur: index % 4 === 0 ? null : String(92_000 + index * 8_450),
    status: deliveryStatuses[index % deliveryStatuses.length],
    version: index,
  });
}

function createMockFetch() {
  let rows = [...sampleDeliveries];
  const byId = (deliveryId: string) => rows.find((row) => row.deliveryId === deliveryId);
  let created = 200;
  const response = (payload: unknown, status = 200) =>
    new Response(JSON.stringify(payload), {
      status,
      headers: { 'Content-Type': 'application/json' },
    });
  return async function mockFetch(input: RequestInfo | URL, init: RequestInit = {}) {
    const method = (init.method ?? 'GET').toUpperCase();
    const url = new URL(String(input), 'http://localhost');
    const pathname = url.pathname;

    if (pathname === '/api/v1/contracts' && method === 'GET') {
      const items = rows.slice(0, 12).map((row, index) => ({
        contractId: row.contractId,
        contractName: `${row.bookType} contract ${String(index + 1).padStart(2, '0')}`,
        productType: 'Gas',
        action: row.bookType === 'Sales' ? 'Sell' : 'Buy',
      }));
      return response({ items, totalCount: items.length, page: 1, pageSize: 100, hasNextPage: false });
    }

    if (pathname.startsWith('/api/v1/deliveries')) {
      if (method === 'GET') {
        if (pathname === '/api/v1/deliveries') {
          const page = 1;
          const pageSize = 100;
          return response({
            items: rows,
            totalCount: rows.length,
            page,
            pageSize,
            hasNextPage: false,
          } as GetDeliveryHistoryResponse);
        }
        const deliveryId = pathname.split('/').at(-1);
        const delivery = deliveryId ? byId(deliveryId) : undefined;
        if (!deliveryId || !delivery) return response({}, 404);
        return response(delivery);
      }
      if (method === 'POST') {
        const request = JSON.parse(String(init.body ?? '{}')) as CreatePhysicalDeliveryRequest;
        const nextId = `del-created-${++created}`;
        const createdDelivery: PhysicalDeliveryDetailsDto = {
          deliveryId: nextId,
          contractId: request.contractId,
          contractInstanceId: request.contractInstanceId ?? null,
          bookType: request.bookType,
          supplyMonth: request.supplyMonth,
          capacityMw: request.capacityMw ?? null,
          volumeNominatedMwh: request.volumeNominatedMwh ?? null,
          volumeRealisedMwh: request.volumeRealisedMwh ?? null,
          priceMechanism: request.priceMechanism ?? null,
          startDay: request.startDay ?? null,
          endDay: request.endDay ?? null,
          status: 'Pending - No Invoice',
          version: 1,
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
          volumeMwh: request.volumeRealisedMwh ?? null,
          revenueEur: null,
          subtotalEur: null,
          vatEur: null,
          invoiceAmountEur: null,
          invoiceNumber: null,
        };
        rows = [createdDelivery, ...rows];
        return response(createdDelivery, 201);
      }
      if (method === 'PUT') {
        const targetId = pathname.split('/').at(-1);
        if (!targetId) return response({}, 404);
        const request = JSON.parse(String(init.body ?? '{}')) as UpdatePhysicalDeliveryRequest;
        const target = byId(targetId);
        if (!target) return response({}, 404);
        const changes = {
          ...(request.volumeRealisedMwh !== undefined ? { volumeRealisedMwh: request.volumeRealisedMwh } : {}),
          ...(request.status !== undefined ? { status: request.status } : {}),
        };
        rows = rows.map((row) =>
          row.deliveryId === targetId ? { ...row, ...changes, version: row.version + 1 } : row,
        );
        const updated = byId(targetId) ?? target;
        return response(updated);
      }
      if (method === 'DELETE') {
        const targetId = pathname.split('/').at(-1);
        if (!targetId) return response({}, 404);
        rows = rows.filter((row) => row.deliveryId !== targetId);
        return response({}, 200);
      }
    }

    return response({ message: `Unhandled API call ${method} ${pathname}` }, 404);
  };
}

type StoryRouterState = {
  storyRouter: ReturnType<typeof createAppRouter>;
  queryClient: QueryClient;
  previousFetch: typeof window.fetch;
  previousTokenProvider: () => Promise<{ kind: 'success'; accessToken: string } | { kind: 'interaction-required' }>;
};

function EntityWorkspaceStory() {
  const router = useMemo(() => {
    const storySession = {
      accountKey: 'storybook-shell',
      actorId: 'actor-11111111-1111-1111-1111-111111111111',
      displayName: 'Story Operator',
    };

    const queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false, staleTime: Infinity },
        mutations: { retry: false },
      },
    });

    queryClient.setQueryData(queryKeys.deliveries.list({ page: 1, pageSize: 100 }), {
      items: sampleDeliveries,
      totalCount: sampleDeliveries.length,
      page: 1,
      pageSize: 100,
      hasNextPage: false,
    } as GetDeliveryHistoryResponse);

    const mockFetch = createMockFetch();
    const previousFetch = window.fetch;
    const previousTokenProvider = tokenProvider.acquireForApi;
    const history = createMemoryHistory({
      initialEntries: ['/deliveries'],
    });
    const storyRouter = createAppRouter({
      history,
      bindSession: false,
      context: {
        queryClient,
        session: {
          get: () => storySession,
        },
        realtimeEnabled: false,
      },
    });
    tokenProvider.acquireForApi = async () => ({ kind: 'success', accessToken: 'storybook-token' });
    window.fetch = mockFetch;
    return {
      storyRouter,
      queryClient,
      previousFetch,
      previousTokenProvider,
    };
  }, []);

  useEffect(() => {
    const root = document.documentElement;
    const wasDark = root.classList.contains('dark');
    const previousZoom = root.style.zoom;
    root.classList.add('dark');
    root.style.zoom = '0.875';
    return () => {
      if (!wasDark) root.classList.remove('dark');
      root.style.zoom = previousZoom;
      window.fetch = router.previousFetch;
      tokenProvider.acquireForApi = router.previousTokenProvider;
    };
  }, [router]);

  return (
    <QueryClientProvider client={router.queryClient}>
      <div className="dark" style={{ height: '100dvh', width: '100%' }}>
        <RouterProvider router={router.storyRouter} />
      </div>
    </QueryClientProvider>
  );
}

const meta = {
  title: 'Tradebook/Entity Workspace/Deliveries',
  component: EntityWorkspaceStory,
  parameters: {
    layout: 'fullscreen',
  },
} satisfies Meta<typeof EntityWorkspaceStory>;

export default meta;
type Story = StoryObj<typeof meta>;
export const RealisticDeliveries: Story = {};
