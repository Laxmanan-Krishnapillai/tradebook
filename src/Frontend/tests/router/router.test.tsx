import { QueryClient } from '@tanstack/react-query';
import { createMemoryHistory, RouterProvider } from '@tanstack/react-router';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { createAppRouter } from '../../src/app/router';
import type { AuthSession } from '../../src/lib/state/useAuthStore';
import { validateInternalReturnPath } from '../../src/lib/session/sessionController';

const validSession: AuthSession = {
  actorId: 'actor-1',
  accessToken: 'token-1',
  expiresAtUtc: '2099-01-01T00:00:00.000Z',
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

describe('production route tree', () => {
  it('redirects the index canonically and protects authenticated routes', async () => {
    const index = await load('/', validSession);
    expect(index.state.location.pathname).toBe('/deliveries');

    const protectedRoute = await load('/contracts');
    expect(protectedRoute.state.location.pathname).toBe('/login');
    expect(protectedRoute.state.location.search).toEqual({ redirect: '/contracts' });
  });

  it.each([
    '/deliveries', '/contracts', '/market-prices', '/dashboard', '/capacity-bookings', '/transfers',
    '/biotickets', '/goo-certificates', '/tax-tariffs', '/hedges', '/workflow',
  ])('deep-links to %s with the production route tree', async (path) => {
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
});
