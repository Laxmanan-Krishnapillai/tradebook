/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

export interface CreateTaxTariffRequest {
    contractId: string;
    counterpartyId?: string | null;
    periodStart: string;
    periodEnd: string;
    taxLocalCurMwh?: number | null;
    tsoLocalCurMwh?: number | null;
    dsoLocalCurMwh?: number | null;
    dsoTariffLocalCurDay?: number | null;
    admFeeLocalCurMwh?: number | null;
    balFeeLocalCurMwh?: number | null;
    currency: string;
}
