import { afterEach, describe, expect, it, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import { apiFetch, problemFieldErrors, resolveApiUrl } from '../../src/lib/api/client';
import { server } from '../../src/mocks/server';
import { useAuthStore } from '../../src/lib/state/useAuthStore';

afterEach(() => {
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
    useAuthStore.getState().setSession('access-token', '2099-01-01T00:00:00.000Z', 'actor-id');
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ ok: true }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' }
    }));
    vi.stubGlobal('fetch', fetchMock);

    await apiFetch<{ ok: boolean }>('/api/v1/test');

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe(new URL('/api/v1/test', globalThis.location.href).toString());
    expect(new Headers(init.headers).get('Authorization')).toBe('Bearer access-token');
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
});
