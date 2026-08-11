import { afterEach, describe, expect, it, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import { apiFetch, problemFieldErrors, resolveApiUrl } from '../../src/lib/api/client';
import { server } from '../../src/mocks/server';
import { useAuthStore } from '../../src/lib/state/useAuthStore';
import { tokenProvider } from '../../src/lib/auth/tokenProvider';

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  useAuthStore.getState().clearSession();
});

describe('API URL resolution', () => {
  it('maps Problem Details field errors by property path', () => {
    expect(problemFieldErrors({ errors: { 'order.price': ['Price is required.'], ignored: 42 } })).toEqual({
      'order.price': ['Price is required.'],
    });
  });

  it('resolves relative paths against the current document origin', () => {
    const documentUrl = 'https://tradebook.example/dashboards/current?tab=charts';

    expect(resolveApiUrl('/api/v1/analytics/query', documentUrl))
      .toBe('https://tradebook.example/api/v1/analytics/query');
  });

  it('preserves absolute URLs', () => {
    expect(resolveApiUrl('https://api.example.test/v1/data', 'https://tradebook.example/'))
      .toBe('https://api.example.test/v1/data');
  });

  it('keeps same-origin authorization headers after resolving a relative path', async () => {
    useAuthStore.getState().setSession({ accountKey: 'account-1', actorId: 'actor-id' });
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ ok: true }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' }
    }));
    vi.stubGlobal('fetch', fetchMock);

    await apiFetch<{ ok: boolean }>('/api/v1/test');

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe(new URL('/api/v1/test', globalThis.location.href).toString());
    expect(new Headers(init.headers).get('Authorization')).toBe('Bearer fake-test-access-token');
  });

  it('allows MSW to intercept a resolved relative request', async () => {
    server.use(http.post(resolveApiUrl('/api/v1/intercepted'), async ({ request }) => {
      await request.json();
      return HttpResponse.json({ ok: true });
    }));

    await expect(apiFetch<{ ok: boolean }>('/api/v1/intercepted', {
      method: 'POST',
      body: JSON.stringify({ test: true }),
      signal: new AbortController().signal
    }))
      .resolves.toEqual({ ok: true });
  });

  it('does not send a request when the session changes during token acquisition', async () => {
    useAuthStore.getState().setSession({ accountKey: 'account-1', actorId: 'actor-id' });
    let resolveToken!: () => void;
    vi.spyOn(tokenProvider, 'acquireForApi').mockImplementation(() => new Promise((resolve) => {
      resolveToken = () => resolve({ kind: 'success', accessToken: 'stale-token' });
    }));
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);

    const request = apiFetch('/api/v1/contracts', { method: 'POST' });
    useAuthStore.getState().clearSession();
    useAuthStore.getState().setSession({ accountKey: 'account-2', actorId: 'actor-id' });
    resolveToken();

    await expect(request).rejects.toMatchObject({ name: 'AbortError' });
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
