import { createFileRoute } from '@tanstack/react-router';
import { ListRoutePending } from '../../components/layout/ListRoutePending';
import { initialListQueryOptions } from '../../lib/query/initialListQueryOptions';

export const Route = createFileRoute('/_authenticated/hedges')({
  loader: ({ context }) => context.queryClient.ensureQueryData(initialListQueryOptions.hedges()),
  pendingComponent: () => <ListRoutePending columns={5} label="hedges" />,
});
