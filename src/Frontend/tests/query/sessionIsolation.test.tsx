import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { apiFetch } from '../../src/lib/api/client';
import { queryClient } from '../../src/lib/query/queryClient';
import { queryKeys } from '../../src/lib/query/queryKeys';
import {
  beginSession,
  endSession,
  registerSessionNavigation,
} from '../../src/lib/session/sessionController';
import { getAuthSession, useAuthStore, type AuthSession } from '../../src/lib/state/useAuthStore';
import { useUiStore } from '../../src/lib/state/useUiStore';

const session = (actorId: string, accessToken = `token-${actorId}`): AuthSession => ({
  actorId,
  accessToken,
  expiresAtUtc: '2099-01-01T00:00:00.000Z',
});

describe('authenticated Query session boundary', () => {
  beforeEach(() => {
    queryClient.clear();
    useAuthStore.getState().clearSession();
    useUiStore.getState().reset();
    registerSessionNavigation({
      invalidate: async () => undefined,
      navigateTo: async () => undefined,
      navigateToLogin: async () => undefined,
    });
  });

  afterEach(async () => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
    await endSession('logout', { navigate: false });
  });

  it('cancels and removes actor A data and UI state before actor B is installed', async () => {
    await beginSession(session('actor-a'));
    queryClient.setQueryData(queryKeys.contracts.list(), { items: [{ contractId: 'private-a' }] });
    useUiStore.getState().openModal('create-delivery');

    await beginSession(session('actor-b', 'rotated-token'));

    expect(queryClient.getQueryCache().getAll()).toHaveLength(0);
    expect(useUiStore.getState().activeModal).toBeNull();
    expect(getAuthSession()).toEqual(session('actor-b', 'rotated-token'));
  });

  it('clears Query and UI state on logout and token expiry', async () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-08-07T12:00:00.000Z'));
    await beginSession({ ...session('actor-a'), expiresAtUtc: '2026-08-07T12:00:01.000Z' });
    queryClient.setQueryData(queryKeys.deliveries.list(), { items: [{ deliveryId: 'private-a' }] });
    useUiStore.getState().openModal('create-delivery');

    await vi.advanceTimersByTimeAsync(1_001);
    await vi.waitFor(() => expect(getAuthSession()).toBeUndefined());
    expect(queryClient.getQueryCache().getAll()).toHaveLength(0);
    expect(useUiStore.getState().activeModal).toBeNull();

    await beginSession(session('actor-b'));
    queryClient.setQueryData(queryKeys.hedges.list(), { items: [{ hedgeId: 'private-b' }] });
    await endSession('logout', { navigate: false });
    expect(getAuthSession()).toBeUndefined();
    expect(queryClient.getQueryCache().getAll()).toHaveLength(0);
  });

  it('ignores a delayed actor A 401 after actor B has logged in', async () => {
    let resolve!: (response: Response) => void;
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((next) => { resolve = next; })));
    await beginSession(session('actor-a'));
    const pending = apiFetch('/api/v1/contracts');

    await beginSession(session('actor-b'));
    resolve(new Response(undefined, { status: 401 }));
    await expect(pending).rejects.toMatchObject({ status: 401 });
    await vi.waitFor(() => expect(getAuthSession()?.actorId).toBe('actor-b'));
  });

  it('clears the current session after an HTTP 401', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(undefined, { status: 401 })));
    await beginSession(session('actor-a'));
    queryClient.setQueryData(queryKeys.contracts.list(), { items: [{ contractId: 'private-a' }] });
    await expect(apiFetch('/api/v1/contracts')).rejects.toMatchObject({ status: 401 });
    await vi.waitFor(() => expect(getAuthSession()).toBeUndefined());
    expect(queryClient.getQueryCache().getAll()).toHaveLength(0);
  });
});
