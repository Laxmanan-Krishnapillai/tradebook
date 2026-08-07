/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { TransferDetailsDto } from "./transfer-details-dto";

export interface GetTransferHistoryResponse {
    items: TransferDetailsDto[];
    totalCount: number;
    page: number;
    pageSize: number;
    hasNextPage: boolean;
}
