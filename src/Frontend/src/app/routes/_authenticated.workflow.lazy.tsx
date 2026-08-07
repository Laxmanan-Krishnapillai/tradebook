import { createLazyFileRoute } from '@tanstack/react-router';
import { WorkflowCanvas } from '../../components/canvas/WorkflowCanvas';

export const Route = createLazyFileRoute('/_authenticated/workflow')({
  component: WorkflowCanvas,
});
