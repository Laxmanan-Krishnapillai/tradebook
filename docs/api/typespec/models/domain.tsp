import "./common.tsp";

namespace Tradebook;

model ActivityEntryDto {
  auditId: string;
  operation: string;
  actorId: string | null;
  occurredAt: utcDateTime;
  changes: unknown;
}

model GetActivityResponse {
  items: ActivityEntryDto[];
}

model BioticketDetailsDto {
  bioticketId: string;
  contractId: string;
  contractInstanceId: string;
  bookType: string;
  contractMonth: string;
  startDay?: string | null;
  endDay?: string | null;
  volumeNominatedTon?: Money | null;
  volumeRealisedTon?: Money | null;
  volumeTon?: Money | null;
  costEurTon?: Money | null;
  revenueEur?: Money | null;
  vatPct?: Money | null;
  vatEur?: Money | null;
  invoiceAmountEur?: Money | null;
  status: string;
  comment?: string | null;
  version: int32;
  createdAt: string;
  updatedAt: string;
}

model CancelBioticketRequest {
  bioticketId: string;
  reason: string;
  version: int32;
}

model CancelTransferRequest {
  transferId: string;
  reason: string;
  version: int32;
}

model CapacityBookingDetailsDto {
  capacityBookingId: string;
  contractId: string;
  contractInstanceId: string;
  supplyMonth: string;
  counterpartyId?: string | null;
  balancingGroup?: string | null;
  priceMechanism?: string | null;
  startArea?: string | null;
  endArea?: string | null;
  shipFix?: string | null;
  borderPoint?: string | null;
  startDay?: string | null;
  endDay?: string | null;
  capacityMw?: Money | null;
  capacityPriceEurMwh?: Money | null;
  capacityCostEur?: Money | null;
  comments?: string | null;
  version: int32;
  createdAt: string;
  updatedAt: string;
}

model ContractDetailsDto {
  contractId: string;
  contractName: string;
  counterpartyId: string;
  productType: string;
  action: string;
  companyShorthand?: string | null;
  countryCode?: string | null;
  countryDialCode?: int32 | null;
  contractNumber?: int32 | null;
  yearOfContract?: int32 | null;
  sourcingCenter?: string | null;
  salesCenter?: string | null;
  balancingGroup?: string | null;
  gooQuality?: string | null;
  subsidyStatus?: string | null;
  priceMechanismGas?: string | null;
  fixedPriceGasEurMwh?: Money | null;
  contractType: string;
  comment?: string | null;
  isActive: boolean;
  version: int32;
  createdAt: string;
  updatedAt: string;
}

model CreateBioticketRequest {
  contractId: string;
  bookType: string;
  contractMonth: string;
  contractInstanceId?: string | null;
  startDay?: string | null;
  endDay?: string | null;
  volumeNominatedTon?: Money | null;
  volumeRealisedTon?: Money | null;
  volumeTon?: Money | null;
  costEurTon?: Money | null;
  revenueEur?: Money | null;
  vatPct?: Money | null;
  vatEur?: Money | null;
  invoiceAmountEur?: Money | null;
  status?: string | null;
  comment?: string | null;
}

model CreateCapacityBookingRequest {
  contractId: string;
  supplyMonth: string;
  contractInstanceId?: string | null;
  counterpartyId?: string | null;
  balancingGroup?: string | null;
  priceMechanism?: string | null;
  startArea?: string | null;
  endArea?: string | null;
  shipFix?: string | null;
  borderPoint?: string | null;
  startDay?: string | null;
  endDay?: string | null;
  capacityMw?: Money | null;
  capacityPriceEurMwh?: Money | null;
  capacityCostEur?: Money | null;
  comments?: string | null;
}

model CreateContractRequest {
  contractName: string;
  counterpartyId: string;
  productType: string;
  action: string;
  companyShorthand?: string | null;
  countryCode?: string | null;
  countryDialCode?: int32 | null;
  contractNumber?: int32 | null;
  yearOfContract?: int32 | null;
  sourcingCenter?: string | null;
  salesCenter?: string | null;
  balancingGroup?: string | null;
  gooQuality?: string | null;
  subsidyStatus?: string | null;
  priceMechanismGas?: string | null;
  fixedPriceGasEurMwh?: Money | null;
  contractType?: string | null;
  comment?: string | null;
}

model CreateGooCertificateTransactionRequest {
  salesforceTransactionId?: string | null;
  transactionName?: string | null;
  batchType?: string | null;
  certificateTransactionId?: string | null;
  countryOfProduction?: string | null;
  producerContractId?: string | null;
  producerCompany?: string | null;
  producerGooPriceEurMwh?: Money | null;
  productionDate?: string | null;
  customerContractId?: string | null;
  customerCompany?: string | null;
  register?: string | null;
  status?: string | null;
  transactionStartDate?: string | null;
  transactionVolumeMwh?: Money | null;
  volumeMwh?: Money | null;
  energySource?: string | null;
  text?: string | null;
}

model CreateHedgeRequest {
  contractId: string;
  month: string;
  hedgeAmountMwh?: Money | null;
  hedgePriceEurMwh?: Money | null;
}

model CreatePhysicalDeliveryRequest {
  contractId: string;
  contractInstanceId?: string | null;
  bookType: string;
  supplyMonth: string;
  capacityMw?: Money | null;
  volumeNominatedMwh?: Money | null;
  volumeRealisedMwh?: Money | null;
  priceMechanism?: string | null;
  startDay?: string | null;
  endDay?: string | null;
}

model CreatePhysicalDeliveryResponse {
  deliveryId: string;
  contractInstanceId: string;
  invoiceAmountEur?: Money | null;
  status: string;
  version: int32;
  createdAt: string;
}

model CreateTaxTariffRequest {
  contractId: string;
  counterpartyId?: string | null;
  periodStart: string;
  periodEnd: string;
  taxLocalCurMwh?: Money | null;
  tsoLocalCurMwh?: Money | null;
  dsoLocalCurMwh?: Money | null;
  dsoTariffLocalCurDay?: Money | null;
  admFeeLocalCurMwh?: Money | null;
  balFeeLocalCurMwh?: Money | null;
  currency: string;
}

model CreateTransferRequest {
  contractId: string;
  supplyMonth: string;
  contractInstanceId?: string | null;
  counterpartyId?: string | null;
  balancingGroup?: string | null;
  tradingArea?: string | null;
  capacityMw?: Money | null;
  bookedCapacityMw?: Money | null;
  volumeMwh?: Money | null;
  balancingEffectMwh?: Money | null;
  startDay?: string | null;
  endDay?: string | null;
  priceMechanism?: string | null;
  transportCostEurMwh?: Money | null;
  capacityCostEurMwh?: Money | null;
  status?: string | null;
  comments?: string | null;
}

model DeactivateContractRequest {
  contractId: string;
  reason: string;
  version: int32;
}

model DeleteCapacityBookingRequest {
  capacityBookingId: string;
  reason: string;
  version: int32;
}

model DeleteGooCertificateTransactionRequest {
  gooCertificateTransactionId: string;
  reason: string;
  version: int32;
}

model DeleteHedgeRequest {
  hedgeId: string;
  reason: string;
  version: int32;
}

model DeleteMarketPriceRequest {
  priceDate: string;
  reason: string;
  version: int32;
}

model DeletePhysicalDeliveryRequest {
  deliveryId: string;
  reason: string;
  version: int32;
}

model DeleteTaxTariffRequest {
  taxTariffId: string;
  reason: string;
  version: int32;
}

model EntityChangedEventDto {
  eventId: string;
  sequenceId: int32;
  aggregateType: string;
  aggregateId: string;
  eventType: string;
  payloadJson: string;
}

model GetBioticketHistoryRequest {
  contractId?: string | null;
  bookType?: string | null;
  status?: string | null;
  fromMonth?: string | null;
  toMonth?: string | null;
  page: int32;
  pageSize: int32;
}

model GetBioticketHistoryResponse {
  items: BioticketDetailsDto[];
  totalCount: int32;
  page: int32;
  pageSize: int32;
  hasNextPage: boolean;
}

model GetCapacityBookingHistoryRequest {
  contractId?: string | null;
  fromMonth?: string | null;
  toMonth?: string | null;
  page: int32;
  pageSize: int32;
}

model GetCapacityBookingHistoryResponse {
  items: CapacityBookingDetailsDto[];
  totalCount: int32;
  page: int32;
  pageSize: int32;
  hasNextPage: boolean;
}

model GetContractHistoryRequest {
  counterpartyId?: string | null;
  productType?: string | null;
  action?: string | null;
  isActive?: boolean | null;
  page: int32;
  pageSize: int32;
}

model GetContractHistoryResponse {
  items: ContractDetailsDto[];
  totalCount: int32;
  page: int32;
  pageSize: int32;
  hasNextPage: boolean;
}

model GetDeliveryHistoryRequest {
  contractId?: string | null;
  contractInstanceId?: string | null;
  bookType?: string | null;
  status?: string | null;
  fromMonth?: string | null;
  toMonth?: string | null;
  page: int32;
  pageSize: int32;
}

model GetDeliveryHistoryResponse {
  items: PhysicalDeliveryDetailsDto[];
  totalCount: int32;
  page: int32;
  pageSize: int32;
  hasNextPage: boolean;
}

model GetEventsSinceResponse {
  events: EntityChangedEventDto[];
  latestSequence: int32;
}

model GetGooCertificateHistoryRequest {
  contractId?: string | null;
  status?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
  page: int32;
  pageSize: int32;
}

model GetGooCertificateHistoryResponse {
  items: GooCertificateTransactionDetailsDto[];
  totalCount: int32;
  page: int32;
  pageSize: int32;
  hasNextPage: boolean;
}

model GetHedgeHistoryRequest {
  contractId?: string | null;
  fromMonth?: string | null;
  toMonth?: string | null;
  page: int32;
  pageSize: int32;
}

model GetHedgeHistoryResponse {
  items: HedgeDetailsDto[];
  totalCount: int32;
  page: int32;
  pageSize: int32;
  hasNextPage: boolean;
}

model GetMarketPriceHistoryRequest {
  fromDate?: string | null;
  toDate?: string | null;
  page: int32;
  pageSize: int32;
}

model GetMarketPriceHistoryResponse {
  items: MarketPriceDetailsDto[];
  totalCount: int32;
  page: int32;
  pageSize: int32;
  hasNextPage: boolean;
}

model GetTaxTariffHistoryRequest {
  contractId?: string | null;
  effectiveOn?: string | null;
  page: int32;
  pageSize: int32;
}

model GetTaxTariffHistoryResponse {
  items: TaxTariffDetailsDto[];
  totalCount: int32;
  page: int32;
  pageSize: int32;
  hasNextPage: boolean;
}

model GetTransferHistoryRequest {
  contractId?: string | null;
  status?: string | null;
  fromMonth?: string | null;
  toMonth?: string | null;
  page: int32;
  pageSize: int32;
}

model GetTransferHistoryResponse {
  items: TransferDetailsDto[];
  totalCount: int32;
  page: int32;
  pageSize: int32;
  hasNextPage: boolean;
}

model GooCertificateTransactionDetailsDto {
  gooCertificateTransactionId: string;
  salesforceTransactionId?: string | null;
  transactionName?: string | null;
  batchType?: string | null;
  certificateTransactionId?: string | null;
  countryOfProduction?: string | null;
  producerContractId?: string | null;
  producerCompany?: string | null;
  producerGooPriceEurMwh?: Money | null;
  productionDate?: string | null;
  customerContractId?: string | null;
  customerCompany?: string | null;
  register?: string | null;
  status?: string | null;
  transactionStartDate?: string | null;
  transactionVolumeMwh?: Money | null;
  volumeMwh?: Money | null;
  energySource?: string | null;
  text?: string | null;
  version: int32;
  createdAt: string;
  updatedAt: string;
}

model HedgeDetailsDto {
  hedgeId: string;
  contractId: string;
  month: string;
  hedgeAmountMwh?: Money | null;
  hedgePriceEurMwh?: Money | null;
  version: int32;
  createdAt: string;
  updatedAt: string;
}

model LoginRequest {
  username: string;
  password: string;
}

model LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  actorId: string;
}

model MarketPriceDetailsDto {
  priceDate: string;
  ttfEurMwh?: Money | null;
  egsiEtfEurMwh?: Money | null;
  theEurMwh?: Money | null;
  bgoEurMwh?: Money | null;
  pgoEurMwh?: Money | null;
  euaEurMwh?: Money | null;
  withinDayMktEurMwh?: Money | null;
  eurSek?: Money | null;
  eurChf?: Money | null;
  eurGbp?: Money | null;
  eurUsd?: Money | null;
  eurDkk?: Money | null;
  version: int32;
  createdAt: string;
}

model PhysicalDeliveryDetailsDto {
  deliveryId: string;
  contractId: string;
  contractInstanceId: string;
  bookType: string;
  supplyMonth: string;
  capacityMw?: Money | null;
  volumeNominatedMwh?: Money | null;
  volumeRealisedMwh?: Money | null;
  volumeMwh?: Money | null;
  priceMechanism?: string | null;
  revenueEur?: Money | null;
  subtotalEur?: Money | null;
  vatEur?: Money | null;
  invoiceAmountEur?: Money | null;
  status: string;
  version: int32;
  createdAt: string;
  updatedAt: string;
}

model ProblemDetailsResponse {
  type: string;
  title: string;
  status: int32;
  detail: string | null;
}

model RequestGooBatchExportRequest {
  gooCertificateTransactionId: string;
  version: int32;
}

model SaveDashboardRequest {
  dashboardId: string;
  version: int32;
  layout: unknown;
}

model SaveDashboardResponse {
  dashboardId: string;
  version: int32;
  layout: unknown;
}

model TaxTariffDetailsDto {
  taxTariffId: string;
  contractId: string;
  counterpartyId?: string | null;
  periodStart: string;
  periodEnd: string;
  taxLocalCurMwh?: Money | null;
  tsoLocalCurMwh?: Money | null;
  dsoLocalCurMwh?: Money | null;
  dsoTariffLocalCurDay?: Money | null;
  admFeeLocalCurMwh?: Money | null;
  balFeeLocalCurMwh?: Money | null;
  currency: string;
  version: int32;
  createdAt: string;
  updatedAt: string;
}

model TransferDetailsDto {
  transferId: string;
  contractId: string;
  contractInstanceId: string;
  supplyMonth: string;
  counterpartyId?: string | null;
  balancingGroup?: string | null;
  tradingArea?: string | null;
  capacityMw?: Money | null;
  bookedCapacityMw?: Money | null;
  volumeMwh?: Money | null;
  balancingEffectMwh?: Money | null;
  startDay?: string | null;
  endDay?: string | null;
  priceMechanism?: string | null;
  transportCostEurMwh?: Money | null;
  capacityCostEurMwh?: Money | null;
  status?: string | null;
  comments?: string | null;
  version: int32;
  createdAt: string;
  updatedAt: string;
}

model UpdateBioticketRequest {
  bioticketId: string;
  volumeRealisedTon?: Money | null;
  volumeTon?: Money | null;
  costEurTon?: Money | null;
  revenueEur?: Money | null;
  vatPct?: Money | null;
  vatEur?: Money | null;
  invoiceAmountEur?: Money | null;
  status?: string | null;
  comment?: string | null;
  version: int32;
}

model UpdateCapacityBookingRequest {
  capacityBookingId: string;
  balancingGroup?: string | null;
  priceMechanism?: string | null;
  startArea?: string | null;
  endArea?: string | null;
  startDay?: string | null;
  endDay?: string | null;
  capacityMw?: Money | null;
  capacityPriceEurMwh?: Money | null;
  capacityCostEur?: Money | null;
  comments?: string | null;
  version: int32;
}

model UpdateContractRequest {
  contractId: string;
  contractName: string;
  counterpartyId: string;
  productType: string;
  action: string;
  companyShorthand?: string | null;
  countryCode?: string | null;
  countryDialCode?: int32 | null;
  sourcingCenter?: string | null;
  salesCenter?: string | null;
  balancingGroup?: string | null;
  gooQuality?: string | null;
  subsidyStatus?: string | null;
  priceMechanismGas?: string | null;
  fixedPriceGasEurMwh?: Money | null;
  contractType?: string | null;
  comment?: string | null;
  isActive?: boolean | null;
  version: int32;
}

model UpdateGooCertificateTransactionRequest {
  gooCertificateTransactionId: string;
  batchType?: string | null;
  producerContractId?: string | null;
  customerContractId?: string | null;
  register?: string | null;
  status?: string | null;
  transactionStartDate?: string | null;
  transactionVolumeMwh?: Money | null;
  volumeMwh?: Money | null;
  text?: string | null;
  version: int32;
}

model UpdateHedgeRequest {
  hedgeId: string;
  hedgeAmountMwh?: Money | null;
  hedgePriceEurMwh?: Money | null;
  version: int32;
}

model UpdatePhysicalDeliveryRequest {
  deliveryId: string;
  volumeRealisedMwh?: Money | null;
  status?: string | null;
  version: int32;
}

model UpdateTaxTariffRequest {
  taxTariffId: string;
  taxLocalCurMwh?: Money | null;
  tsoLocalCurMwh?: Money | null;
  dsoLocalCurMwh?: Money | null;
  dsoTariffLocalCurDay?: Money | null;
  admFeeLocalCurMwh?: Money | null;
  balFeeLocalCurMwh?: Money | null;
  currency: string;
  version: int32;
}

model UpdateTransferRequest {
  transferId: string;
  tradingArea?: string | null;
  capacityMw?: Money | null;
  bookedCapacityMw?: Money | null;
  volumeMwh?: Money | null;
  balancingEffectMwh?: Money | null;
  priceMechanism?: string | null;
  transportCostEurMwh?: Money | null;
  capacityCostEurMwh?: Money | null;
  status?: string | null;
  comments?: string | null;
  version: int32;
}

model UpsertMarketPriceRequest {
  priceDate: string;
  ttfEurMwh?: Money | null;
  egsiEtfEurMwh?: Money | null;
  theEurMwh?: Money | null;
  bgoEurMwh?: Money | null;
  pgoEurMwh?: Money | null;
  euaEurMwh?: Money | null;
  withinDayMktEurMwh?: Money | null;
  eurSek?: Money | null;
  eurChf?: Money | null;
  eurGbp?: Money | null;
  eurUsd?: Money | null;
  eurDkk?: Money | null;
  version: int32;
}
