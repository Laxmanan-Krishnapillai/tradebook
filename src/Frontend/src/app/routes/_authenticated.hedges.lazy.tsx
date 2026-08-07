import { createLazyFileRoute } from '@tanstack/react-router';
import { HedgesPage } from '../../components/domain/DomainEntityPages';

export const Route = createLazyFileRoute('/_authenticated/hedges')({ component: HedgesPage });
