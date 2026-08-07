import type { QueryClient } from '@tanstack/react-query';
import { getAuthSession, type AuthSession } from '../lib/state/useAuthStore';

export interface TradebookRouteContext {
  queryClient: QueryClient;
  session: {
    get: () => AuthSession | undefined;
  };
  realtimeEnabled: boolean;
}

export const sessionAccess = {
  get: getAuthSession,
} as const;
