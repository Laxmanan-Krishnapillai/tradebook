import { endSession } from '../session/sessionController';
import { useAuthStore } from '../state/useAuthStore';
import { client as generatedClient } from '../../api/generated/client.gen';
import type { ProblemDetails } from '../../api/generated/types.gen';
import { zProblemDetails } from '../../api/generated/zod.gen';

let generatedClientConfigured = false;

export function configureGeneratedApiClient(): void {
  if (generatedClientConfigured) return;
  generatedClientConfigured = true;
  generatedClient.setConfig({ baseUrl: globalThis.location.origin, responseStyle: 'data', throwOnError: true });
  generatedClient.interceptors.request.use((request) => {
    const token = useAuthStore.getState().accessToken;
    if (token) request.headers.set('Authorization', `Bearer ${token}`);
    return request;
  });
  generatedClient.interceptors.response.use((response) => {
    const token = useAuthStore.getState().accessToken;
    if (response.status === 401 && token) {
      void endSession('unauthorized', { expectedAccessToken: token });
    }
    return response;
  });
}

export class ApiError extends Error {
  constructor(public readonly status: number, public readonly problem?: unknown) {
    super(`HTTP ${status}`);
    this.name = 'ApiError';
  }
}

function parseProblemDetails(value: unknown): ProblemDetails | undefined {
  if (typeof value !== 'object' || value === null || !['type', 'title', 'status', 'detail', 'instance', 'errors'].some((key) => key in value)) return undefined;
  const result = zProblemDetails.safeParse(value);
  return result.success ? result.data : undefined;
}

export function problemFieldErrors(problem: ProblemDetails | undefined): Record<string, string[]> {
  if (!problem?.errors) return {};
  return Object.fromEntries(
    Object.entries(problem.errors).flatMap(([path, messages]) => {
      if (typeof messages === 'string') return [[path, [messages]]];
      if (Array.isArray(messages) && messages.every((message) => typeof message === 'string')) return [[path, messages]];
      return [];
    }),
  );
}

/**
 * Browser fetch accepts relative URLs, but its Node implementation (used by
 * Vitest/MSW) does not. Resolving against the document URL keeps requests on
 * the current origin in production while producing an absolute URL in tests.
 */
export function resolveApiUrl(path: string, documentUrl = globalThis.location?.href): string {
  if (/^[a-z][a-z\d+.-]*:/i.test(path)) return path;
  if (!documentUrl) throw new TypeError(`Cannot resolve relative API URL '${path}' without a document URL.`);
  return new URL(path, documentUrl).toString();
}

function fetchCompatibleSignal(url: string, signal: AbortSignal | null | undefined): AbortSignal | undefined {
  if (!signal) return undefined;
  try {
    // jsdom and Node fetch can expose AbortSignal constructors from different
    // realms. Browser signals pass this preflight unchanged; incompatible test
    // signals are omitted so MSW can still exercise the request path.
    void new Request(url, { signal });
    return signal;
  } catch (error) {
    if (error instanceof TypeError) return undefined;
    throw error;
  }
}

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = useAuthStore.getState().accessToken;
  const headers = new Headers(init.headers);
  headers.set('Accept', 'application/json');
  if (init.body !== undefined && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
  if (token) headers.set('Authorization', `Bearer ${token}`);

  const url = resolveApiUrl(path);
  const response = await fetch(url, { ...init, headers, signal: fetchCompatibleSignal(url, init.signal) });
  if (!response.ok) {
    let problem: unknown;
    try {
      const body = await response.json();
      problem = parseProblemDetails(body) ?? body;
    } catch { problem = undefined; }
    if (response.status === 401 && token) {
      void endSession('unauthorized', { expectedAccessToken: token });
    }
    throw new ApiError(response.status, problem);
  }

  return response.status === 204 ? undefined as T : response.json() as Promise<T>;
}
