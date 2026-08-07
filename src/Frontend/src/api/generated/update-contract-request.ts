/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

export interface UpdateContractRequest {
    contractId: string;
    contractName: string;
    counterpartyId: string;
    productType: string;
    action: string;
    companyShorthand?: string | null;
    countryCode?: string | null;
    countryDialCode?: number | null;
    sourcingCenter?: string | null;
    salesCenter?: string | null;
    balancingGroup?: string | null;
    gooQuality?: string | null;
    subsidyStatus?: string | null;
    priceMechanismGas?: string | null;
    fixedPriceGasEurMwh?: number | null;
    contractType?: string | null;
    comment?: string | null;
    isActive?: boolean | null;
    version: number;
}
