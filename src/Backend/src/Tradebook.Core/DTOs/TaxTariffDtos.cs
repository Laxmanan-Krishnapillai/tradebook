using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateTaxTariffRequest(
    Guid ContractId,
    [property: TsOptional] Guid? CounterpartyId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    [property: TsOptional] decimal? TaxLocalCurMwh,
    [property: TsOptional] decimal? TsoLocalCurMwh,
    [property: TsOptional] decimal? DsoLocalCurMwh,
    [property: TsOptional] decimal? DsoTariffLocalCurDay,
    [property: TsOptional] decimal? AdmFeeLocalCurMwh,
    [property: TsOptional] decimal? BalFeeLocalCurMwh,
    string Currency);

[ExportTsInterface]
public sealed record UpdateTaxTariffRequest(
    Guid TaxTariffId,
    [property: TsOptional] decimal? TaxLocalCurMwh,
    [property: TsOptional] decimal? TsoLocalCurMwh,
    [property: TsOptional] decimal? DsoLocalCurMwh,
    [property: TsOptional] decimal? DsoTariffLocalCurDay,
    [property: TsOptional] decimal? AdmFeeLocalCurMwh,
    [property: TsOptional] decimal? BalFeeLocalCurMwh,
    string Currency,
    long Version);

[ExportTsInterface]
public sealed record TaxTariffDetailsDto(
    Guid TaxTariffId, Guid ContractId,
    [property: TsOptional] Guid? CounterpartyId,
    DateOnly PeriodStart, DateOnly PeriodEnd,
    [property: TsOptional] decimal? TaxLocalCurMwh,
    [property: TsOptional] decimal? TsoLocalCurMwh,
    [property: TsOptional] decimal? DsoLocalCurMwh,
    [property: TsOptional] decimal? DsoTariffLocalCurDay,
    [property: TsOptional] decimal? AdmFeeLocalCurMwh,
    [property: TsOptional] decimal? BalFeeLocalCurMwh,
    string Currency, long Version, DateTime CreatedAt, DateTime UpdatedAt);

[ExportTsInterface]
public sealed record GetTaxTariffHistoryRequest(
    [property: TsOptional] Guid? ContractId,
    [property: TsOptional] DateOnly? EffectiveOn,
    int Page = 1,
    int PageSize = 50);

[ExportTsInterface]
public sealed record GetTaxTariffHistoryResponse(
    IReadOnlyList<TaxTariffDetailsDto> Items, int TotalCount, int Page, int PageSize, bool HasNextPage);

[ExportTsInterface]
public sealed record DeleteTaxTariffRequest(Guid TaxTariffId, string Reason, long Version);
