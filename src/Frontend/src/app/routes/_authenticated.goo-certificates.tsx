import { createFileRoute } from '@tanstack/react-router';
import { ListRoutePending } from '../../components/layout/ListRoutePending';
import { initialListQueryOptions } from '../../lib/query/initialListQueryOptions';

export const Route = createFileRoute('/_authenticated/goo-certificates')({
  loader: ({ context }) => context.queryClient.ensureQueryData(initialListQueryOptions.gooCertificates()),
  pendingComponent: () => <ListRoutePending columns={5} label="GOO certificates" />,
});
