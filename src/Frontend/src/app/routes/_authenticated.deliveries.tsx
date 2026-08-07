import { createFileRoute } from '@tanstack/react-router';
import { DeliveriesPage } from '../../components/deliveries/DeliveriesPage';

export const Route = createFileRoute('/_authenticated/deliveries')({
  component: DeliveriesPage,
});
