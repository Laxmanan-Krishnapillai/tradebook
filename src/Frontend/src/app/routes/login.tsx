import { createFileRoute, redirect } from '@tanstack/react-router';
import { LoginForm } from '../../components/auth/LoginForm';
import {
  isSessionCurrent,
  validateInternalReturnPath,
} from '../../lib/session/sessionController';

interface LoginSearch {
  redirect?: string;
}

export const Route = createFileRoute('/login')({
  validateSearch: (search: Record<string, unknown>): LoginSearch => ({
    redirect: validateInternalReturnPath(search.redirect),
  }),
  beforeLoad: ({ context, search }) => {
    if (isSessionCurrent(Date.now(), context.session.get())) {
      throw redirect({
        to: validateInternalReturnPath(search.redirect) ?? '/deliveries',
        replace: true,
      });
    }
  },
  component: LoginRoute,
});

function LoginRoute() {
  const { redirect: returnPath } = Route.useSearch();
  return <LoginForm returnPath={returnPath} />;
}
