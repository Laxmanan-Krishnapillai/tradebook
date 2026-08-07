/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

export interface CreateBioticketRequest {
    contractId: string;
    bookType: string;
    contractMonth: string;
    contractInstanceId?: string | null;
    startDay?: string | null;
    endDay?: string | null;
    volumeNominatedTon?: number | null;
    volumeRealisedTon?: number | null;
    volumeTon?: number | null;
    costEurTon?: number | null;
    revenueEur?: number | null;
    vatPct?: number | null;
    vatEur?: number | null;
    invoiceAmountEur?: number | null;
    status?: string | null;
    comment?: string | null;
}
