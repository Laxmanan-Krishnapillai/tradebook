import { createLazyFileRoute } from '@tanstack/react-router';
import { InAppAgentPage } from '../../features/agent/InAppAgentPage';

export const Route = createLazyFileRoute('/_authenticated/assistant')({
  component: InAppAgentPage,
});
