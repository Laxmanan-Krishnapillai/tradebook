/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

export interface CreateCapacityBookingRequest {
    contractId: string;
    supplyMonth: string;
    contractInstanceId?: string | null;
    counterpartyId?: string | null;
    balancingGroup?: string | null;
    priceMechanism?: string | null;
    startArea?: string | null;
    endArea?: string | null;
    shipFix?: string | null;
    borderPoint?: string | null;
    startDay?: string | null;
    endDay?: string | null;
    capacityMw?: number | null;
    capacityPriceEurMwh?: number | null;
    capacityCostEur?: number | null;
    comments?: string | null;
}
