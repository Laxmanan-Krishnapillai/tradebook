import { createRootRouteWithContext, Outlet } from '@tanstack/react-router';
import type { TradebookRouteContext } from '../routeContext';

function NotFoundView() {
  return (
    <main className="flex min-h-screen items-center justify-center p-4">
      <section className="grid w-full max-w-md gap-4 rounded-card border border-gray-200 bg-white p-8 shadow-xl">
        <p className="mb-1 text-xs font-extrabold uppercase tracking-widest text-gray-600">404</p>
        <h1>Page not found</h1>
        <p>The requested Tradebook page does not exist.</p>
      </section>
    </main>
  );
}

export const Route = createRootRouteWithContext<TradebookRouteContext>()({
  component: Outlet,
  pendingComponent: () => <p>Loading Tradebook…</p>,
  errorComponent: ({ error }) => <p role="alert">Unable to load this page: {error.message}</p>,
  notFoundComponent: NotFoundView,
});
