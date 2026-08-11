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

const session = (actorId: string, accountKey = `account-${actorId}`): AuthSession => ({ actorId, accountKey });

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

  it('clears Query and UI state on logout', async () => {
    await beginSession(session('actor-a'));
    queryClient.setQueryData(queryKeys.deliveries.list(), { items: [] });
    useUiStore.getState().openModal('create-delivery');
    await endSession('unauthorized', { navigate: false });
    expect(getAuthSession()).toBeUndefined();
    expect(queryClient.getQueryCache().getAll()).toHaveLength(0);
    expect(useUiStore.getState().activeModal).toBeNull();
  });

  it('invalidates the old identity before awaiting cache cancellation', async () => {
    await beginSession(session('actor-a'));
    let releaseCancellation!: () => void;
    const cancellation = new Promise<void>((resolve) => {
      releaseCancellation = resolve;
    });
    const cancelQueries = vi.spyOn(queryClient, 'cancelQueries').mockReturnValue(cancellation);

    const transition = beginSession(session('actor-b'));

    await vi.waitFor(() => expect(getAuthSession()).toBeUndefined());
    releaseCancellation();
    await transition;

    expect(getAuthSession()).toEqual(session('actor-b'));
    cancelQueries.mockRestore();
  });

});
