/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { TaxTariffDetailsDto } from "./tax-tariff-details-dto";

export interface GetTaxTariffHistoryResponse {
    items: TaxTariffDetailsDto[];
    totalCount: number;
    page: number;
    pageSize: number;
    hasNextPage: boolean;
}
