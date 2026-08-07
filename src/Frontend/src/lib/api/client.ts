import { useAuthStore } from '../state/useAuthStore';

export class ApiError extends Error { constructor(public readonly status: number, public readonly problem?: unknown) { super(`HTTP ${status}`); } }

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = useAuthStore.getState().accessToken;
  const response = await fetch(path, { ...init, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}), ...init.headers } });
  if (!response.ok) { let problem: unknown; try { problem = await response.json(); } catch { problem = undefined; } throw new ApiError(response.status, problem); }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>;
}
