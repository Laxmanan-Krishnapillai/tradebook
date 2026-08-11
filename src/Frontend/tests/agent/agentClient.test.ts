import { afterEach, describe, expect, it, vi } from 'vitest';
import { authenticatedAgentFetch } from '../../src/lib/agent/agentClient';
import { tokenProvider } from '../../src/lib/auth/tokenProvider';

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('authenticated AG-UI transport', () => {
  it('acquires a fresh API token for every run request', async () => {
    vi.spyOn(tokenProvider, 'acquireForApi')
      .mockResolvedValueOnce({ kind: 'success', accessToken: 'token-1' })
      .mockResolvedValueOnce({ kind: 'success', accessToken: 'token-2' });
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 200 }));
    vi.stubGlobal('fetch', fetchMock);

    await authenticatedAgentFetch('/api/v1/agent/run', { method: 'POST' });
    await authenticatedAgentFetch('/api/v1/agent/run', { method: 'POST' });

    expect(tokenProvider.acquireForApi).toHaveBeenCalledTimes(2);
    expect(new Headers(fetchMock.mock.calls[0]?.[1]?.headers).get('Authorization')).toBe('Bearer token-1');
    expect(new Headers(fetchMock.mock.calls[1]?.[1]?.headers).get('Authorization')).toBe('Bearer token-2');
  });

  it('does not replay an agent POST after an unauthorized response', async () => {
    vi.spyOn(tokenProvider, 'acquireForApi').mockResolvedValue({ kind: 'success', accessToken: 'expired-token' });
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 401 }));
    vi.stubGlobal('fetch', fetchMock);

    const response = await authenticatedAgentFetch('/api/v1/agent/run', { method: 'POST' });

    expect(response.status).toBe(401);
    expect(fetchMock).toHaveBeenCalledOnce();
    expect(tokenProvider.acquireForApi).toHaveBeenCalledOnce();
  });

  it('rejects a cross-origin run endpoint before acquiring or attaching a bearer token', async () => {
    const acquire = vi.spyOn(tokenProvider, 'acquireForApi');
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);

    await expect(authenticatedAgentFetch('https://untrusted.example/agent/run', { method: 'POST' }))
      .rejects.toThrow('must use the Tradebook application origin');

    expect(acquire).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
