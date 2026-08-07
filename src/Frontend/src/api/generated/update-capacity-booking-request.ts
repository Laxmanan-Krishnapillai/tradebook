/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

export interface UpdateCapacityBookingRequest {
    capacityBookingId: string;
    balancingGroup?: string | null;
    priceMechanism?: string | null;
    startArea?: string | null;
    endArea?: string | null;
    startDay?: string | null;
    endDay?: string | null;
    capacityMw?: number | null;
    capacityPriceEurMwh?: number | null;
    capacityCostEur?: number | null;
    comments?: string | null;
    version: number;
}
