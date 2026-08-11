import { createFileRoute } from '@tanstack/react-router';
import { MarketPricesPage } from '../../components/market-prices/MarketPricesPage';
import { ListRoutePending } from '../../components/layout/ListRoutePending';
import { initialListQueryOptions } from '../../lib/query/initialListQueryOptions';

export const Route = createFileRoute('/_authenticated/market-prices')({
  component: MarketPricesPage,
  loader: ({ context }) => context.queryClient.ensureQueryData(initialListQueryOptions.marketPrices()),
  pendingComponent: () => <ListRoutePending columns={5} label="market prices" />,
});
