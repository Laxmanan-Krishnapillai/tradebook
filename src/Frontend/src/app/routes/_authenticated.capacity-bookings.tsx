import { createFileRoute } from '@tanstack/react-router';
import { ListRoutePending } from '../../components/layout/ListRoutePending';
import { initialListQueryOptions } from '../../lib/query/initialListQueryOptions';

export const Route = createFileRoute('/_authenticated/capacity-bookings')({
  loader: ({ context }) => context.queryClient.ensureQueryData(initialListQueryOptions.capacityBookings()),
  pendingComponent: () => <ListRoutePending columns={5} label="capacity bookings" />,
});
