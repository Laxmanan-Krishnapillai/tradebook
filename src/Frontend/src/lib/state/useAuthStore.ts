import { create } from 'zustand';

export interface AuthSession {
  accessToken: string;
  expiresAtUtc: string;
  actorId: string;
}

interface AuthState {
  accessToken: string;
  expiresAtUtc?: string;
  actorId?: string;
  setSession: (accessToken: string, expiresAtUtc: string, actorId: string) => void;
  clearSession: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: '',
  expiresAtUtc: undefined,
  actorId: undefined,
  setSession: (accessToken, expiresAtUtc, actorId) => set({ accessToken, expiresAtUtc, actorId }),
  clearSession: () => set({ accessToken: '', expiresAtUtc: undefined, actorId: undefined })
}));

export function getAuthSession(): AuthSession | undefined {
  const { accessToken, expiresAtUtc, actorId } = useAuthStore.getState();
  return accessToken && expiresAtUtc && actorId ? { accessToken, expiresAtUtc, actorId } : undefined;
}
