import { createLazyFileRoute } from '@tanstack/react-router';
import { CapacityBookingsPage } from '../../components/domain/DomainEntityPages';

export const Route = createLazyFileRoute('/_authenticated/capacity-bookings')({
  component: CapacityBookingsPage,
});
