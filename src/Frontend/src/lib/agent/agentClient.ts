import { HttpAgent } from '@ag-ui/client';
import { queryOptions } from '@tanstack/react-query';
import type { InAppAgentStatusResponse } from '../../api/generated/types.gen';
import { apiFetch, resolveApiUrl } from '../api/client';
import { tokenProvider } from '../auth/tokenProvider';

export const inAppAgentStatusOptions = queryOptions({
  queryKey: ['agent', 'status'] as const,
  queryFn: () => apiFetch<InAppAgentStatusResponse>('/api/v1/agent/status'),
  staleTime: 30_000,
});

export async function authenticatedAgentFetch(url: string, init: RequestInit): Promise<Response> {
  const resolvedUrl = resolveApiUrl(url);
  if (new URL(resolvedUrl).origin !== globalThis.location.origin) {
    throw new Error('The assistant run endpoint must use the Tradebook application origin.');
  }

  const token = await tokenProvider.acquireForApi();
  if (token.kind === 'interaction-required') {
    throw new Error('Your session needs attention before the assistant can run. Sign in again and retry.');
  }

  const headers = new Headers(init.headers);
  headers.set('Authorization', `Bearer ${token.accessToken}`);

  // Agent runs are POST requests and are deliberately never replayed after a 401.
  return fetch(resolvedUrl, { ...init, headers });
}

export function createInAppAgent(runPath: string): HttpAgent {
  return new HttpAgent({
    url: resolveApiUrl(runPath),
    fetch: authenticatedAgentFetch,
  });
}
