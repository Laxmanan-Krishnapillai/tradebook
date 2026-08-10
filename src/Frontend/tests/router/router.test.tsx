import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createMemoryHistory, RouterProvider } from '@tanstack/react-router';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { createAppRouter } from '../../src/app/router';
import type { AuthSession } from '../../src/lib/state/useAuthStore';
import { validateInternalReturnPath } from '../../src/lib/session/sessionController';
import { useUiStore } from '../../src/lib/state/useUiStore';

const validSession: AuthSession = {
  actorId: 'actor-1',
  accountKey: 'account-1',
};

async function load(initialPath: string, session?: AuthSession) {
  const router = createAppRouter({
    history: createMemoryHistory({ initialEntries: [initialPath] }),
    bindSession: false,
    context: {
      queryClient: new QueryClient(),
      session: { get: () => session },
      realtimeEnabled: false,
    },
  });
  await router.load();
  return router;
}

// Vite auto-code-splits every route component, so `router.load()` imports the
// matched page's chunk on demand. Under Vitest that first import transforms and
// executes the page's whole module graph cold (the dashboard chunk alone pulls in
// echarts + lightweight-charts and takes >10s on a warm dev machine), which blows
// the default 5s budget. Raise the budget for the tests that pay that one-off
// cold-load cost; the assertions themselves are unchanged.
const routeChunkColdLoadTimeout = { timeout: 60_000 };

describe('production route tree', () => {
  it('redirects the index canonically and protects authenticated routes', routeChunkColdLoadTimeout, async () => {
    const index = await load('/', validSession);
    expect(index.state.location.pathname).toBe('/deliveries');

    const protectedRoute = await load('/contracts');
    expect(protectedRoute.state.location.pathname).toBe('/login');
    expect(protectedRoute.state.location.search).toEqual({ redirect: '/contracts' });
  });

  it.each([
    '/deliveries', '/contracts', '/market-prices', '/dashboard', '/capacity-bookings', '/transfers',
    '/biotickets', '/goo-certificates', '/tax-tariffs', '/hedges', '/workflow',
  ])('deep-links to %s with the production route tree', routeChunkColdLoadTimeout, async (path) => {
    const router = await load(path, validSession);
    expect(router.state.location.pathname).toBe(path);
    expect(router.state.matches.some((match) => match.status === 'notFound')).toBe(false);
  });

  it('marks an unknown URL as not found', async () => {
    const router = await load('/does-not-exist', validSession);
    vi.spyOn(window, 'scrollTo').mockImplementation(() => undefined);
    const view = render(<RouterProvider router={router} />);
    expect(await screen.findByRole('heading', { name: 'Page not found' })).toBeTruthy();
    view.unmount();
  });

  it('accepts only whitelisted internal login return targets', () => {
    expect(validateInternalReturnPath('/dashboard?ignored=true')).toBe('/dashboard');
    expect(validateInternalReturnPath('/workflow')).toBe('/workflow');
    expect(validateInternalReturnPath('https://evil.example/deliveries')).toBeUndefined();
    expect(validateInternalReturnPath('//evil.example/deliveries')).toBeUndefined();
    expect(validateInternalReturnPath('/deliveries\\evil')).toBeUndefined();
    expect(validateInternalReturnPath('/does-not-exist')).toBeUndefined();
  });

  it('navigates from cmdk and opens the delivery modal through the UI store', async () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const router = createAppRouter({
      history: createMemoryHistory({ initialEntries: ['/contracts'] }),
      bindSession: false,
      context: {
        queryClient,
        session: { get: () => validSession },
        realtimeEnabled: false,
      },
    });
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      items: [], totalCount: 0, page: 1, pageSize: 100, hasNextPage: false,
    }), { status: 200, headers: { 'Content-Type': 'application/json' } })));
    vi.stubGlobal('ResizeObserver', class {
      observe() { /* browser layout is outside this integration boundary */ }
      unobserve() { /* browser layout is outside this integration boundary */ }
      disconnect() { /* browser layout is outside this integration boundary */ }
    });
    Object.defineProperty(HTMLElement.prototype, 'scrollIntoView', {
      configurable: true,
      value: vi.fn(),
    });
    await router.load();

    const view = render(
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>,
    );
    fireEvent.keyDown(document, { key: 'k', ctrlKey: true });
    fireEvent.click(await screen.findByText('Create New Physical Delivery'));

    await waitFor(() => expect(router.state.location.pathname).toBe('/deliveries'));
    expect(await screen.findByRole('dialog', { name: 'Create physical delivery' })).toBeTruthy();
    expect(useUiStore.getState().activeModal).toBe('create-delivery');

    view.unmount();
    useUiStore.getState().reset();
    vi.unstubAllGlobals();
  });
});
