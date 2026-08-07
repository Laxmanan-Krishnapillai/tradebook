import { createFileRoute, redirect } from '@tanstack/react-router';
import { isSessionCurrent } from '../../lib/session/sessionController';

export const Route = createFileRoute('/')({
  beforeLoad: ({ context }) => {
    throw redirect({
      to: isSessionCurrent(Date.now(), context.session.get()) ? '/deliveries' : '/login',
      replace: true,
    });
  },
});
