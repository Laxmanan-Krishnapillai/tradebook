import { beforeEach, describe, expect, it } from 'vitest';
import { useUiStore } from '../../src/lib/state/useUiStore';

describe('UI store', () => {
  beforeEach(() => useUiStore.getState().reset());

  it('owns the delivery modal without storing server or route state', () => {
    useUiStore.getState().openModal('create-delivery');
    expect(useUiStore.getState().activeModal).toBe('create-delivery');
    useUiStore.getState().closeModal();
    expect(useUiStore.getState().activeModal).toBeNull();
  });

  it('resets session-scoped UI state', () => {
    useUiStore.getState().openModal('create-delivery');
    useUiStore.getState().reset();
    expect(useUiStore.getState()).toMatchObject({ activeModal: null });
  });
});
