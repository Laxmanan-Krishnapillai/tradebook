import { createLazyFileRoute } from '@tanstack/react-router';
import { TransfersPage } from '../../components/domain/DomainEntityPages';

export const Route = createLazyFileRoute('/_authenticated/transfers')({ component: TransfersPage });
