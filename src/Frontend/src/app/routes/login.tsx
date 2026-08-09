import { createFileRoute, redirect } from '@tanstack/react-router';
import { LoginForm } from '../../components/auth/LoginForm';
import { z } from 'zod';
import {
  isSessionCurrent,
  validateInternalReturnPath,
} from '../../lib/session/sessionController';

import { internalPath } from '../../lib/validation/return-url';

export const loginSearchSchema = z.object({ redirect: internalPath.optional().catch(undefined) });

export const Route = createFileRoute('/login')({
  validateSearch: loginSearchSchema,
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
