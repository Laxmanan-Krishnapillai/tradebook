/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

export interface CreatePhysicalDeliveryRequest {
    contractId: string;
    contractInstanceId?: string | null;
    bookType: string;
    supplyMonth: string;
    capacityMw?: number | null;
    volumeNominatedMwh?: number | null;
    volumeRealisedMwh?: number | null;
    priceMechanism?: string | null;
    startDay?: string | null;
    endDay?: string | null;
}
