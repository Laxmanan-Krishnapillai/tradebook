import { createLazyFileRoute } from '@tanstack/react-router';
import { TaxTariffsPage } from '../../components/domain/DomainEntityPages';

export const Route = createLazyFileRoute('/_authenticated/tax-tariffs')({ component: TaxTariffsPage });
