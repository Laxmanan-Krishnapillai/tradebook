using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record TaxTariffDetailsDto
{
    public TaxTariffDetailsDto() { }

    [SetsRequiredMembers]
    public TaxTariffDetailsDto(
        Guid TaxTariffId,
        Guid ContractId,
        Guid? CounterpartyId,
        DateOnly PeriodStart,
        DateOnly PeriodEnd,
        decimal? TaxLocalCurMwh,
        decimal? TsoLocalCurMwh,
        decimal? DsoLocalCurMwh,
        decimal? DsoTariffLocalCurDay,
        decimal? AdmFeeLocalCurMwh,
        decimal? BalFeeLocalCurMwh,
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

    public required Guid TaxTariffId { get; init; }
    public required Guid ContractId { get; init; }

    [TsOptional]
    public Guid? CounterpartyId { get; init; }
    public required DateOnly PeriodStart { get; init; }
    public required DateOnly PeriodEnd { get; init; }

    [TsOptional]
    public decimal? TaxLocalCurMwh { get; init; }

    [TsOptional]
    public decimal? TsoLocalCurMwh { get; init; }

    [TsOptional]
    public decimal? DsoLocalCurMwh { get; init; }

    [TsOptional]
    public decimal? DsoTariffLocalCurDay { get; init; }

    [TsOptional]
    public decimal? AdmFeeLocalCurMwh { get; init; }

    [TsOptional]
    public decimal? BalFeeLocalCurMwh { get; init; }
    public required string Currency { get; init; }
    public required long Version { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
