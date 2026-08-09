import { queryClient } from '../query/queryClient';
import { getAuthSession, useAuthStore, type AuthSession } from '../state/useAuthStore';
import { useUiStore } from '../state/useUiStore';
import { msalInstance } from '../auth/msalInstance';

const authenticatedRoutePaths = [
  '/deliveries',
  '/contracts',
  '/market-prices',
  '/dashboard',
  '/capacity-bookings',
  '/transfers',
  '/biotickets',
  '/goo-certificates',
  '/tax-tariffs',
  '/hedges',
  '/workflow',
] as const;

export type AuthenticatedRoutePath = (typeof authenticatedRoutePaths)[number];
export type SessionEndReason = 'logout' | 'expired' | 'unauthorized';
interface SessionNavigation { invalidate: () => Promise<void>; navigateTo: (path: AuthenticatedRoutePath) => Promise<void>; navigateToLogin: () => Promise<void> }
let navigation: SessionNavigation | undefined;
let transition = Promise.resolve();
export function registerSessionNavigation(next: SessionNavigation): void { navigation = next; }
export function validateInternalReturnPath(value: unknown): AuthenticatedRoutePath | undefined {
  if (typeof value !== 'string' || !value.startsWith('/') || value.startsWith('//') || value.includes('\\')) return undefined;
  try { const url = new URL(value, 'https://tradebook.invalid'); return url.origin === 'https://tradebook.invalid' ? authenticatedRoutePaths.find((route) => route === url.pathname) : undefined; } catch { return undefined; }
}
export function isSessionCurrent(_now = Date.now(), session = getAuthSession()): boolean { return session !== undefined; }
function serialize(operation: () => Promise<void>): Promise<void> { const next = transition.then(operation, operation); transition = next.catch(() => undefined); return next; }
async function reset(): Promise<void> { await queryClient.cancelQueries(); queryClient.clear(); useUiStore.getState().reset(); }
export function beginSession(session: AuthSession, returnPath?: unknown): Promise<void> { return serialize(async () => { await reset(); useAuthStore.getState().setSession(session); await navigation?.invalidate(); await navigation?.navigateTo(validateInternalReturnPath(returnPath) ?? '/deliveries'); }); }
export function endSession(reason: SessionEndReason, options: { navigate?: boolean } = {}): Promise<void> { return serialize(async () => { await reset(); useAuthStore.getState().clearSession(); msalInstance.setActiveAccount(null); await navigation?.invalidate(); if (reason === 'logout' && options.navigate !== false) { await msalInstance.logoutRedirect(); return; } if (options.navigate !== false) await navigation?.navigateToLogin(); }); }
