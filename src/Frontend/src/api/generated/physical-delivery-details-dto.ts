/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

export interface PhysicalDeliveryDetailsDto {
    deliveryId: string;
    contractId: string;
    contractInstanceId: string;
    bookType: string;
    supplyMonth: string;
    capacityMw?: number | null;
    volumeNominatedMwh?: number | null;
    volumeRealisedMwh?: number | null;
    volumeMwh?: number | null;
    priceMechanism?: string | null;
    revenueEur?: number | null;
    subtotalEur?: number | null;
    vatEur?: number | null;
    invoiceAmountEur?: number | null;
    status: string;
    version: number;
    createdAt: string;
    updatedAt: string;
}
