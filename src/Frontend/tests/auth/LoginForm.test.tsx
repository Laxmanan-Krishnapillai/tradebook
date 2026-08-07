import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { LoginForm } from '../../src/components/auth/LoginForm';
import { useAuthStore } from '../../src/lib/state/useAuthStore';

describe('LoginForm', () => {
  afterEach(() => { useAuthStore.getState().clearSession(); vi.unstubAllGlobals(); });

  it('stores the JWT session returned by the sole anonymous API route', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ accessToken: 'signed.jwt.value', expiresAtUtc: '2099-08-07T20:00:00Z', actorId: 'actor-1' }), { status: 200, headers: { 'Content-Type': 'application/json' } })));
    const view = render(<LoginForm />);
    fireEvent.change(screen.getByLabelText('Username'), { target: { value: 'trader' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'secret' } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    await waitFor(() => expect(useAuthStore.getState().accessToken).toBe('signed.jwt.value'));
    expect(useAuthStore.getState().actorId).toBe('actor-1');
    const request = vi.mocked(fetch).mock.calls[0];
    expect(new URL(String(request[0])).pathname).toBe('/api/v1/auth/login');
    expect((request[1]?.headers as Headers).has('Authorization')).toBe(false);
    view.unmount();
  });
});
