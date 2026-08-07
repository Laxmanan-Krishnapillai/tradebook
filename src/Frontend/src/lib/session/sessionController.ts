import { queryClient } from '../query/queryClient';
import { getAuthSession, useAuthStore, type AuthSession } from '../state/useAuthStore';
import { useUiStore } from '../state/useUiStore';

export const authenticatedRoutePaths = [
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

interface SessionNavigation {
  invalidate: () => Promise<void>;
  navigateTo: (path: AuthenticatedRoutePath) => Promise<void>;
  navigateToLogin: () => Promise<void>;
}

let navigation: SessionNavigation | undefined;
let expiryTimer: ReturnType<typeof setTimeout> | undefined;
let transition: Promise<void> = Promise.resolve();

export function registerSessionNavigation(next: SessionNavigation): void {
  navigation = next;
}

export function validateInternalReturnPath(value: unknown): AuthenticatedRoutePath | undefined {
  if (typeof value !== 'string' || !value.startsWith('/') || value.startsWith('//') || value.includes('\\')) return undefined;
  let pathname: string;
  try {
    const url = new URL(value, 'https://tradebook.invalid');
    if (url.origin !== 'https://tradebook.invalid') return undefined;
    pathname = url.pathname;
  } catch {
    return undefined;
  }
  return authenticatedRoutePaths.find((route) => route === pathname);
}

export function isSessionCurrent(now = Date.now(), session = getAuthSession()): boolean {
  return session !== undefined && Number.isFinite(Date.parse(session.expiresAtUtc)) && Date.parse(session.expiresAtUtc) > now;
}

function cancelExpiryTimer(): void {
  if (expiryTimer !== undefined) clearTimeout(expiryTimer);
  expiryTimer = undefined;
}

function scheduleExpiry(session: AuthSession): void {
  cancelExpiryTimer();
  const delay = Date.parse(session.expiresAtUtc) - Date.now();
  if (delay <= 0) {
    void endSession('expired', { expectedAccessToken: session.accessToken });
    return;
  }
  const maximumTimeout = 2_147_483_647;
  expiryTimer = setTimeout(() => {
    if (delay > maximumTimeout) scheduleExpiry(session);
    else void endSession('expired', { expectedAccessToken: session.accessToken });
  }, Math.min(delay, maximumTimeout));
}

function serialize(operation: () => Promise<void>): Promise<void> {
  const next = transition.then(operation, operation);
  transition = next.catch(() => undefined);
  return next;
}

async function resetAuthenticatedState(): Promise<void> {
  await queryClient.cancelQueries();
  queryClient.clear();
  useUiStore.getState().reset();
}

export function beginSession(session: AuthSession, returnPath?: unknown): Promise<void> {
  return serialize(async () => {
    cancelExpiryTimer();
    await resetAuthenticatedState();
    useAuthStore.getState().setSession(session.accessToken, session.expiresAtUtc, session.actorId);
    scheduleExpiry(session);
    await navigation?.invalidate();
    await navigation?.navigateTo(validateInternalReturnPath(returnPath) ?? '/deliveries');
  });
}

export function endSession(
  reason: SessionEndReason,
  options: { navigate?: boolean; expectedAccessToken?: string } = {},
): Promise<void> {
  return serialize(async () => {
    void reason;
    if (
      options.expectedAccessToken !== undefined
      && useAuthStore.getState().accessToken !== options.expectedAccessToken
    ) return;
    cancelExpiryTimer();
    await resetAuthenticatedState();
    useAuthStore.getState().clearSession();
    await navigation?.invalidate();
    if (options.navigate !== false) await navigation?.navigateToLogin();
  });
}
