import { describe, expect, it, vi } from 'vitest';
import { tokenProvider } from '../../src/lib/auth/tokenProvider';

describe('API token boundary', () => {
  it('never stores token values in Zustand and unsafe mutations are not replayed', async () => {
    expect(Object.keys((await import('../../src/lib/state/useAuthStore')).useAuthStore.getState())).not.toContain('accessToken');
    const acquisition = vi.spyOn(tokenProvider, 'acquireForApi').mockResolvedValue({ kind: 'success', accessToken: 'boundary-token' });
    const fetch = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(undefined, { status: 401 }));
    const { apiFetch } = await import('../../src/lib/api/client');
    await expect(apiFetch('/api/v1/contracts', { method: 'POST' })).rejects.toMatchObject({ status: 401 });
    expect(acquisition).toHaveBeenCalledTimes(1);
    expect(fetch).toHaveBeenCalledTimes(1);
  });
});
