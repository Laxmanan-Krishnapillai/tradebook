import { createFileRoute } from '@tanstack/react-router';
import { ContractsPage } from '../../components/contracts/ContractsPage';

export const Route = createFileRoute('/_authenticated/contracts')({
  component: ContractsPage,
});
