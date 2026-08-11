import { createRouter, type RouterHistory } from '@tanstack/react-router';
import { queryClient, QUERY_STALE_TIME_MS } from '../lib/query/queryClient';
import {
  registerSessionNavigation,
  type AuthenticatedRoutePath,
} from '../lib/session/sessionController';
import { sessionAccess, type TradebookRouteContext } from './routeContext';
import { routeTree } from './routeTree.gen';

interface CreateAppRouterOptions {
  history?: RouterHistory;
  context?: Partial<TradebookRouteContext>;
  bindSession?: boolean;
}

export function createAppRouter(options: CreateAppRouterOptions = {}) {
  const context: TradebookRouteContext = {
    queryClient,
    session: sessionAccess,
    realtimeEnabled: true,
    ...options.context,
  };
  const appRouter = createRouter({
    routeTree,
    history: options.history,
    context,
    defaultViewTransition: true,
    defaultPreload: 'intent',
    defaultPreloadStaleTime: QUERY_STALE_TIME_MS,
    defaultPendingMs: 120,
    defaultPendingMinMs: 150,
  });

  if (options.bindSession !== false) {
    registerSessionNavigation({
      invalidate: () => appRouter.invalidate(),
      navigateTo: (path: AuthenticatedRoutePath) => appRouter.navigate({ to: path, replace: true }),
      navigateToLogin: () => appRouter.navigate({ to: '/login', search: {}, replace: true }),
    });
  }

  return appRouter;
}

export const router = createAppRouter();

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router;
  }
}
