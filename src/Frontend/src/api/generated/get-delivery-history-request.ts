/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

export interface GetDeliveryHistoryRequest {
    contractId?: string | null;
    contractInstanceId?: string | null;
    bookType?: string | null;
    status?: string | null;
    fromMonth?: string | null;
    toMonth?: string | null;
    page: number;
    pageSize: number;
}
