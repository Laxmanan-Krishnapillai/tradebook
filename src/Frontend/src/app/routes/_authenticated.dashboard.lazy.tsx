import { createLazyFileRoute } from '@tanstack/react-router';
import { DashboardPage } from '../../components/dashboard/DashboardPage';
import { registerDefaultAdapters } from '../../lib/charts/registerDefaultAdapters';

registerDefaultAdapters();

export const Route = createLazyFileRoute('/_authenticated/dashboard')({
  component: DashboardPage,
});
