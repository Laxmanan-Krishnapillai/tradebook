import { createLazyFileRoute } from '@tanstack/react-router';
import { BioticketsPage } from '../../components/domain/DomainEntityPages';

export const Route = createLazyFileRoute('/_authenticated/biotickets')({ component: BioticketsPage });
