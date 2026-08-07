import { createFileRoute } from '@tanstack/react-router';
import { MarketPricesPage } from '../../components/market-prices/MarketPricesPage';

export const Route = createFileRoute('/_authenticated/market-prices')({
  component: MarketPricesPage,
});
