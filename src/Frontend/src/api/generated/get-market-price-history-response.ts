/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { MarketPriceDetailsDto } from "./market-price-details-dto";

export interface GetMarketPriceHistoryResponse {
    items: MarketPriceDetailsDto[];
    totalCount: number;
    page: number;
    pageSize: number;
    hasNextPage: boolean;
}
