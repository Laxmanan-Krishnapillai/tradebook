using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record TaxTariffDetailsDto
{
    public TaxTariffDetailsDto() { }

    [SetsRequiredMembers]
    public TaxTariffDetailsDto(
        TaxTariffId TaxTariffId,
        ContractId ContractId,
        CounterpartyId? CounterpartyId,
        DateOnly PeriodStart,
        DateOnly PeriodEnd,
        Amount? TaxLocalCurMwh,
        Amount? TsoLocalCurMwh,
        Amount? DsoLocalCurMwh,
        Amount? DsoTariffLocalCurDay,
        Amount? AdmFeeLocalCurMwh,
        Amount? BalFeeLocalCurMwh,
        string Currency,
        long Version,
        DateTime CreatedAt,
        DateTime UpdatedAt
    )
    {
        this.TaxTariffId = TaxTariffId;
        this.ContractId = ContractId;
        this.CounterpartyId = CounterpartyId;
        this.PeriodStart = PeriodStart;
        this.PeriodEnd = PeriodEnd;
        this.TaxLocalCurMwh = TaxLocalCurMwh;
        this.TsoLocalCurMwh = TsoLocalCurMwh;
        this.DsoLocalCurMwh = DsoLocalCurMwh;
        this.DsoTariffLocalCurDay = DsoTariffLocalCurDay;
        this.AdmFeeLocalCurMwh = AdmFeeLocalCurMwh;
        this.BalFeeLocalCurMwh = BalFeeLocalCurMwh;
        this.Currency = Currency;
        this.Version = Version;
        this.CreatedAt = CreatedAt;
        this.UpdatedAt = UpdatedAt;
    }

    public required TaxTariffId TaxTariffId { get; init; }
    public required ContractId ContractId { get; init; }

    [TsOptional]
    public CounterpartyId? CounterpartyId { get; init; }
    public required DateOnly PeriodStart { get; init; }
    public required DateOnly PeriodEnd { get; init; }

    [TsOptional]
    public Amount? TaxLocalCurMwh { get; init; }

    [TsOptional]
    public Amount? TsoLocalCurMwh { get; init; }

    [TsOptional]
    public Amount? DsoLocalCurMwh { get; init; }

    [TsOptional]
    public Amount? DsoTariffLocalCurDay { get; init; }

    [TsOptional]
    public Amount? AdmFeeLocalCurMwh { get; init; }

    [TsOptional]
    public Amount? BalFeeLocalCurMwh { get; init; }
    public required string Currency { get; init; }
    public required long Version { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
