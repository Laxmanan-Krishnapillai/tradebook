import { tokenProvider } from '../auth/tokenProvider';

export class ApiError extends Error { constructor(public readonly status: number, public readonly problem?: unknown) { super(`HTTP ${status}`); } }
export class ReauthenticationRequiredError extends Error {}
export function resolveApiUrl(path: string, documentUrl = globalThis.location?.href): string {
  if (/^[a-z][a-z\d+.-]*:/i.test(path)) return path;
  if (!documentUrl) throw new TypeError(`Cannot resolve relative API URL '${path}' without a document URL.`);
  return new URL(path, documentUrl).toString();
}
const safeReplayMethods = new Set(['GET', 'HEAD', 'OPTIONS']);

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const method = (init.method ?? 'GET').toUpperCase();
  const send = async (forceRefresh: boolean) => {
    const token = await tokenProvider.acquireForApi(forceRefresh);
    if (token.kind === 'interaction-required') throw new ReauthenticationRequiredError();
    const headers = new Headers(init.headers);
    headers.set('Accept', 'application/json');
    headers.set('Authorization', `Bearer ${token.accessToken}`);
    if (init.body !== undefined && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
    return fetch(resolveApiUrl(path), { ...init, headers });
  };
  let response = await send(false);
  if (response.status === 401 && safeReplayMethods.has(method)) response = await send(true);
  if (!response.ok) {
    let problem: unknown; try { problem = await response.json(); } catch { problem = undefined; }
    throw new ApiError(response.status, problem);
  }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>;
}
