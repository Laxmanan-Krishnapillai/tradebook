import { createFileRoute } from '@tanstack/react-router';
import { ContractsPage } from '../../components/contracts/ContractsPage';
import { ListRoutePending } from '../../components/layout/ListRoutePending';
import { initialListQueryOptions } from '../../lib/query/initialListQueryOptions';

export const Route = createFileRoute('/_authenticated/contracts')({
  component: ContractsPage,
  loader: ({ context }) => context.queryClient.ensureQueryData(initialListQueryOptions.contracts()),
  pendingComponent: () => <ListRoutePending columns={5} label="contracts" />,
});
