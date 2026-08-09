import { createFileRoute, Outlet, redirect } from '@tanstack/react-router';
import { AppShell } from '../../components/layout/AppShell';
import {
  RealtimeQuerySyncProvider,
  useRealtimeQuerySync,
} from '../../hooks/useRealtimeQuerySync';
import { CommandStackProvider } from '../../lib/commands/CommandStackContext';
import { endSession, isSessionCurrent } from '../../lib/session/sessionController';

export const Route = createFileRoute('/_authenticated')({
  beforeLoad: async ({ context, location }) => {
    if (isSessionCurrent(Date.now(), context.session.get())) return;
    if (context.session.get()) await endSession('expired', { navigate: false });
    throw redirect({
      to: '/login',
      search: { redirect: location.pathname },
      replace: true,
    });
  },
  component: AuthenticatedLayout,
});

function AuthenticatedLayout() {
  const { realtimeEnabled, session } = Route.useRouteContext();
  const currentSession = session.get();
  if (!currentSession) return null;
  const sessionKey = currentSession.accountKey;
  return <SessionScopedAuthenticatedLayout key={sessionKey} realtimeEnabled={realtimeEnabled} sessionKey={sessionKey} />;
}

function SessionScopedAuthenticatedLayout({ realtimeEnabled, sessionKey }: { realtimeEnabled: boolean; sessionKey: string }) {
  const lastEvent = useRealtimeQuerySync(realtimeEnabled, sessionKey);
  return (
    <CommandStackProvider>
      <RealtimeQuerySyncProvider lastEvent={lastEvent}>
        <AppShell>
          <Outlet />
        </AppShell>
      </RealtimeQuerySyncProvider>
    </CommandStackProvider>
  );
}
