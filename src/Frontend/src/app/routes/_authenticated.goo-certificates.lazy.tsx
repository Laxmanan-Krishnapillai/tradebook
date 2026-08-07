import { createLazyFileRoute } from '@tanstack/react-router';
import { GooCertificatesPage } from '../../components/domain/DomainEntityPages';

export const Route = createLazyFileRoute('/_authenticated/goo-certificates')({ component: GooCertificatesPage });
