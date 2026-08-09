import "@typespec/http";
import "@typespec/openapi3";
import "@typespec/openapi";
import "./models/domain.tsp";

using TypeSpec.Http;
using TypeSpec.OpenAPI;

@service(#{ title: "Tradebook API" })
@TypeSpec.OpenAPI.info(#{ version: "1.0.0" })
@useAuth(TypeSpec.Http.BearerAuth)
namespace Tradebook;


alias ApiResult<T> = T | ProblemDetails;

@useAuth(TypeSpec.Http.NoAuth)
@route("/health") interface Health {
  @get @route("/live") live(): string;
  @get @route("/ready") ready(): string;
}

@useAuth(TypeSpec.Http.NoAuth)
@route("/api/v1/auth") interface Auth {
  @post @route("/login") login(@body request: LoginRequest): ApiResult<LoginResponse>;
}

@route("/api/v1/contracts") interface Contracts {
  @get list(...GetContractHistoryRequest): ApiResult<GetContractHistoryResponse>;
  @get @route("/{contractId}") read(@path contractId: string): ApiResult<ContractDetailsDto>;
  @post create(@body request: CreateContractRequest): ApiResult<ContractDetailsDto>;
  @put @route("/{contractId}") update(@path contractId: string, @body request: UpdateContractRequest): ApiResult<ContractDetailsDto>;
  @delete @route("/{contractId}") deactivate(@path contractId: string, @body request: DeactivateContractRequest): void | ProblemDetails;
}

@route("/api/v1/deliveries") interface Deliveries {
  @get list(...GetDeliveryHistoryRequest): ApiResult<GetDeliveryHistoryResponse>;
  @get @route("/{deliveryId}") read(@path deliveryId: string): ApiResult<PhysicalDeliveryDetailsDto>;
  @post create(@body request: CreatePhysicalDeliveryRequest): ApiResult<CreatePhysicalDeliveryResponse>;
  @put @route("/{deliveryId}") update(@path deliveryId: string, @body request: UpdatePhysicalDeliveryRequest): ApiResult<PhysicalDeliveryDetailsDto>;
  @delete @route("/{deliveryId}") delete(@path deliveryId: string, @body request: DeletePhysicalDeliveryRequest): void | ProblemDetails;
}

@route("/api/v1/capacity-bookings") interface CapacityBookings {
  @get list(...GetCapacityBookingHistoryRequest): ApiResult<GetCapacityBookingHistoryResponse>;
  @get @route("/{capacityBookingId}") read(@path capacityBookingId: string): ApiResult<CapacityBookingDetailsDto>;
  @post create(@body request: CreateCapacityBookingRequest): ApiResult<CapacityBookingDetailsDto>;
  @put @route("/{capacityBookingId}") update(@path capacityBookingId: string, @body request: UpdateCapacityBookingRequest): ApiResult<CapacityBookingDetailsDto>;
  @delete @route("/{capacityBookingId}") delete(@path capacityBookingId: string, @body request: DeleteCapacityBookingRequest): void | ProblemDetails;
}

@route("/api/v1/transfers") interface Transfers {
  @get list(...GetTransferHistoryRequest): ApiResult<GetTransferHistoryResponse>;
  @get @route("/{transferId}") read(@path transferId: string): ApiResult<TransferDetailsDto>;
  @post create(@body request: CreateTransferRequest): ApiResult<TransferDetailsDto>;
  @put @route("/{transferId}") update(@path transferId: string, @body request: UpdateTransferRequest): ApiResult<TransferDetailsDto>;
  @delete @route("/{transferId}") delete(@path transferId: string, @body request: CancelTransferRequest): void | ProblemDetails;
}

@route("/api/v1/biotickets") interface Biotickets {
  @get list(...GetBioticketHistoryRequest): ApiResult<GetBioticketHistoryResponse>;
  @get @route("/{bioticketId}") read(@path bioticketId: string): ApiResult<BioticketDetailsDto>;
  @post create(@body request: CreateBioticketRequest): ApiResult<BioticketDetailsDto>;
  @put @route("/{bioticketId}") update(@path bioticketId: string, @body request: UpdateBioticketRequest): ApiResult<BioticketDetailsDto>;
  @delete @route("/{bioticketId}") delete(@path bioticketId: string, @body request: CancelBioticketRequest): void | ProblemDetails;
}

@route("/api/v1/hedges") interface Hedges {
  @get list(...GetHedgeHistoryRequest): ApiResult<GetHedgeHistoryResponse>;
  @get @route("/{hedgeId}") read(@path hedgeId: string): ApiResult<HedgeDetailsDto>;
  @post create(@body request: CreateHedgeRequest): ApiResult<HedgeDetailsDto>;
  @put @route("/{hedgeId}") update(@path hedgeId: string, @body request: UpdateHedgeRequest): ApiResult<HedgeDetailsDto>;
  @delete @route("/{hedgeId}") delete(@path hedgeId: string, @body request: DeleteHedgeRequest): void | ProblemDetails;
}

@route("/api/v1/tax-tariffs") interface TaxTariffs {
  @get list(...GetTaxTariffHistoryRequest): ApiResult<GetTaxTariffHistoryResponse>;
  @get @route("/{taxTariffId}") read(@path taxTariffId: string): ApiResult<TaxTariffDetailsDto>;
  @post create(@body request: CreateTaxTariffRequest): ApiResult<TaxTariffDetailsDto>;
  @put @route("/{taxTariffId}") update(@path taxTariffId: string, @body request: UpdateTaxTariffRequest): ApiResult<TaxTariffDetailsDto>;
  @delete @route("/{taxTariffId}") delete(@path taxTariffId: string, @body request: DeleteTaxTariffRequest): void | ProblemDetails;
}

@route("/api/v1/market-prices") interface MarketPrices {
  @get list(...GetMarketPriceHistoryRequest): ApiResult<GetMarketPriceHistoryResponse>;
  @get @route("/{priceDate}") read(@path priceDate: string): ApiResult<MarketPriceDetailsDto>;
  @put @route("/{priceDate}") upsert(@path priceDate: string, @body request: UpsertMarketPriceRequest): ApiResult<MarketPriceDetailsDto>;
  @delete @route("/{priceDate}") delete(@path priceDate: string, @body request: DeleteMarketPriceRequest): void | ProblemDetails;
}

@route("/api/v1/goo-certificates") interface GooCertificates {
  @get list(...GetGooCertificateHistoryRequest): ApiResult<GetGooCertificateHistoryResponse>;
  @get @route("/{gooCertificateTransactionId}") read(@path gooCertificateTransactionId: string): ApiResult<GooCertificateTransactionDetailsDto>;
  @post create(@body request: CreateGooCertificateTransactionRequest): ApiResult<GooCertificateTransactionDetailsDto>;
  @put @route("/{gooCertificateTransactionId}") update(@path gooCertificateTransactionId: string, @body request: UpdateGooCertificateTransactionRequest): ApiResult<GooCertificateTransactionDetailsDto>;
  @delete @route("/{gooCertificateTransactionId}") delete(@path gooCertificateTransactionId: string, @body request: DeleteGooCertificateTransactionRequest): void | ProblemDetails;
  @post @route("/{gooCertificateTransactionId}/request-batch-export") requestBatchExport(@path gooCertificateTransactionId: string, @body request: RequestGooBatchExportRequest): ApiResult<GooCertificateTransactionDetailsDto>;
}

model GetEventsSinceRequest { @query after: int64; }
@route("/api/v1/events") interface Events { @get list(...GetEventsSinceRequest): ApiResult<GetEventsSinceResponse>; }

model DashboardPayload { dashboardId: string; version: int64; layout: unknown; }
@route("/api/v1/dashboards") interface Dashboards {
  @get @route("/{dashboardId}") read(@path dashboardId: string): ApiResult<DashboardPayload>;
  @put @route("/{dashboardId}") save(@path dashboardId: string, @body request: DashboardPayload): ApiResult<DashboardPayload>;
}

model TimeDimensionQuery { dimension: string; granularity: string; dateRange?: string[] | null; }
model FilterQuery { member: string; operator: string; values: unknown[]; }
model SortQuery { member: string; direction: string; }
model JsonQueryAst { modelName: string; measures?: string[] | null; metrics?: string[] | null; dimensions?: string[] | null; timeDimensions?: TimeDimensionQuery[] | null; filters?: FilterQuery[] | null; sorts?: SortQuery[] | null; limit?: int32 | null; offset?: int32 | null; }
model AnalyticsQueryResponse { columns: string[]; rows: unknown[][]; }
@route("/api/v1/analytics/query") interface Analytics { @post query(@body request: JsonQueryAst): ApiResult<AnalyticsQueryResponse>; }
