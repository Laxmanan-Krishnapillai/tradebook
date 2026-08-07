/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { HedgeDetailsDto } from "./hedge-details-dto";

export interface GetHedgeHistoryResponse {
    items: HedgeDetailsDto[];
    totalCount: number;
    page: number;
    pageSize: number;
    hasNextPage: boolean;
}
