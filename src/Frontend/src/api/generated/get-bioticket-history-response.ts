/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { BioticketDetailsDto } from "./bioticket-details-dto";

export interface GetBioticketHistoryResponse {
    items: BioticketDetailsDto[];
    totalCount: number;
    page: number;
    pageSize: number;
    hasNextPage: boolean;
}
