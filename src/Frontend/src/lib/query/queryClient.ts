import { QueryClient } from '@tanstack/react-query';

export const QUERY_STALE_TIME_MS = 15_000;

function queryRetryPolicy(failureCount: number, error: unknown): boolean {
  const status = typeof error === 'object' && error !== null && 'status' in error
    ? (error as { status?: unknown }).status
    : undefined;
  if (typeof status === 'number') return status >= 500 && failureCount < 2;
  return failureCount < 2;
}

export function createTradebookQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: QUERY_STALE_TIME_MS,
        refetchOnWindowFocus: true,
        retry: queryRetryPolicy,
      },
      mutations: {
        retry: false,
      },
    },
  });
}

export const queryClient = createTradebookQueryClient();
