/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { CapacityBookingDetailsDto } from "./capacity-booking-details-dto";

export interface GetCapacityBookingHistoryResponse {
    items: CapacityBookingDetailsDto[];
    totalCount: number;
    page: number;
    pageSize: number;
    hasNextPage: boolean;
}
