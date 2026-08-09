import { endSession } from '../session/sessionController';
import { useAuthStore } from '../state/useAuthStore';
import { z } from 'zod';

export class ApiError extends Error {
  constructor(public readonly status: number, public readonly problem?: unknown) {
    super(`HTTP ${status}`);
    this.name = 'ApiError';
  }
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

export async function apiFetch<T>(path: string, init: RequestInit = {}, schema?: z.ZodType<T>): Promise<T> {
  const token = useAuthStore.getState().accessToken;
  const headers = new Headers(init.headers);
  headers.set('Accept', 'application/json');
  if (init.body !== undefined && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
  if (token) headers.set('Authorization', `Bearer ${token}`);

  const url = resolveApiUrl(path);
  const response = await fetch(url, { ...init, headers, signal: fetchCompatibleSignal(url, init.signal) });
  if (!response.ok) {
    let problem: unknown;
    try { problem = z.json().parse(await response.json()); } catch { problem = undefined; }
    if (response.status === 401 && token) {
      void endSession('unauthorized', { expectedAccessToken: token });
    }
    throw new ApiError(response.status, problem);
  }

  if (response.status === 204) return undefined as T;
  const payload: unknown = await response.json();
  if (schema) return schema.parse(payload);
  return z.json().parse(payload) as T;
}
