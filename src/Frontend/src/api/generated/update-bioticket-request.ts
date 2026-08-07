/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

export interface UpdateBioticketRequest {
    bioticketId: string;
    volumeRealisedTon?: number | null;
    volumeTon?: number | null;
    costEurTon?: number | null;
    revenueEur?: number | null;
    vatPct?: number | null;
    vatEur?: number | null;
    invoiceAmountEur?: number | null;
    status?: string | null;
    comment?: string | null;
    version: number;
}
