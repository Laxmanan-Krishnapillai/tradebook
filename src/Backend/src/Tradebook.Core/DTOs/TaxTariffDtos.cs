using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateTaxTariffRequest(
    ContractId ContractId,
    [property: TsOptional] CounterpartyId? CounterpartyId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    [property: TsOptional] Amount? TaxLocalCurMwh,
    [property: TsOptional] Amount? TsoLocalCurMwh,
    [property: TsOptional] Amount? DsoLocalCurMwh,
    [property: TsOptional] Amount? DsoTariffLocalCurDay,
    [property: TsOptional] Amount? AdmFeeLocalCurMwh,
    [property: TsOptional] Amount? BalFeeLocalCurMwh,
    string Currency
);

[ExportTsInterface]
public sealed record UpdateTaxTariffRequest(
    TaxTariffId TaxTariffId,
    [property: TsOptional] Amount? TaxLocalCurMwh,
    [property: TsOptional] Amount? TsoLocalCurMwh,
    [property: TsOptional] Amount? DsoLocalCurMwh,
    [property: TsOptional] Amount? DsoTariffLocalCurDay,
    [property: TsOptional] Amount? AdmFeeLocalCurMwh,
    [property: TsOptional] Amount? BalFeeLocalCurMwh,
    string Currency,
    long Version
);

[ExportTsInterface]
public sealed record TaxTariffDetailsDto(
    TaxTariffId TaxTariffId,
    ContractId ContractId,
    [property: TsOptional] CounterpartyId? CounterpartyId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    [property: TsOptional] Amount? TaxLocalCurMwh,
    [property: TsOptional] Amount? TsoLocalCurMwh,
    [property: TsOptional] Amount? DsoLocalCurMwh,
    [property: TsOptional] Amount? DsoTariffLocalCurDay,
    [property: TsOptional] Amount? AdmFeeLocalCurMwh,
    [property: TsOptional] Amount? BalFeeLocalCurMwh,
    string Currency,
    long Version,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

[ExportTsInterface]
public sealed record GetTaxTariffHistoryRequest(
    [property: TsOptional] ContractId? ContractId,
    [property: TsOptional] DateOnly? EffectiveOn,
    int Page = 1,
    int PageSize = 50
);

[ExportTsInterface]
public sealed record GetTaxTariffHistoryResponse(
    IReadOnlyList<TaxTariffDetailsDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    bool HasNextPage
);

[ExportTsInterface]
public sealed record DeleteTaxTariffRequest(TaxTariffId TaxTariffId, string Reason, long Version);
