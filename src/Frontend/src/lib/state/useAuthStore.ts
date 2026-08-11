import { create } from 'zustand';

export interface AuthSession { accountKey: string; actorId: string; displayName?: string }
interface AuthState extends Partial<AuthSession> {
  isAuthenticated: boolean;
  sessionEpoch: number;
  setSession: (session: AuthSession) => void;
  clearSession: () => void;
}
export const useAuthStore = create<AuthState>((set) => ({
  isAuthenticated: false,
  sessionEpoch: 0,
  setSession: (session) => set((state) => ({
    ...session,
    isAuthenticated: true,
    sessionEpoch: state.sessionEpoch + 1,
  })),
  clearSession: () => set((state) => ({
    accountKey: undefined,
    actorId: undefined,
    displayName: undefined,
    isAuthenticated: false,
    sessionEpoch: state.sessionEpoch + 1,
  })),
}));
export function getAuthSession(): AuthSession | undefined {
  const { isAuthenticated, accountKey, actorId, displayName } = useAuthStore.getState();
  return isAuthenticated && accountKey && actorId ? { accountKey, actorId, displayName } : undefined;
}

export function getAuthSessionIdentity(): string {
  const state = useAuthStore.getState();
  return `${state.sessionEpoch}\u0000${state.actorId ?? ''}\u0000${state.accountKey ?? ''}`;
}
