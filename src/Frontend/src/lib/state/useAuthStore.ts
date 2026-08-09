import { create } from 'zustand';

export interface AuthSession { accountKey: string; actorId: string; displayName?: string }
interface AuthState extends Partial<AuthSession> {
  isAuthenticated: boolean;
  setSession: (session: AuthSession) => void;
  clearSession: () => void;
}
export const useAuthStore = create<AuthState>((set) => ({
  isAuthenticated: false,
  setSession: (session) => set({ ...session, isAuthenticated: true }),
  clearSession: () => set({ accountKey: undefined, actorId: undefined, displayName: undefined, isAuthenticated: false }),
}));
export function getAuthSession(): AuthSession | undefined {
  const { isAuthenticated, accountKey, actorId, displayName } = useAuthStore.getState();
  return isAuthenticated && accountKey && actorId ? { accountKey, actorId, displayName } : undefined;
}
