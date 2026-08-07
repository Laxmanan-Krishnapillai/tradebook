/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

export interface BioticketDetailsDto {
    bioticketId: string;
    contractId: string;
    contractInstanceId: string;
    bookType: string;
    contractMonth: string;
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
    status: string;
    comment?: string | null;
    version: number;
    createdAt: string;
    updatedAt: string;
}
