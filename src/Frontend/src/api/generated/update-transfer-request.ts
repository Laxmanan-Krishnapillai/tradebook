/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

export interface UpdateTransferRequest {
    transferId: string;
    tradingArea?: string | null;
    capacityMw?: number | null;
    bookedCapacityMw?: number | null;
    volumeMwh?: number | null;
    balancingEffectMwh?: number | null;
    priceMechanism?: string | null;
    transportCostEurMwh?: number | null;
    capacityCostEurMwh?: number | null;
    status?: string | null;
    comments?: string | null;
    version: number;
}
