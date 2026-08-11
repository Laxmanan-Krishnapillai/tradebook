import { createFileRoute } from '@tanstack/react-router';
import { DeliveriesPage } from '../../components/deliveries/DeliveriesPage';
import { ListRoutePending } from '../../components/layout/ListRoutePending';
import { initialListQueryOptions } from '../../lib/query/initialListQueryOptions';

export const Route = createFileRoute('/_authenticated/deliveries')({
  component: DeliveriesPage,
  loader: ({ context }) => context.queryClient.ensureQueryData(initialListQueryOptions.deliveries()),
  pendingComponent: () => <ListRoutePending columns={6} label="deliveries" />,
});
