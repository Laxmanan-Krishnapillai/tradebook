import { createRootRouteWithContext, Outlet } from '@tanstack/react-router';
import type { TradebookRouteContext } from '../routeContext';

function NotFoundView() {
  return (
    <main className="login-shell">
      <section className="login-card">
        <p className="eyebrow">404</p>
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
