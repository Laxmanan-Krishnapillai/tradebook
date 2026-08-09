using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record UpdateTaxTariffRequest
{
    public UpdateTaxTariffRequest() { }

    [SetsRequiredMembers]
    public UpdateTaxTariffRequest(
        Guid TaxTariffId,
        decimal? TaxLocalCurMwh,
        decimal? TsoLocalCurMwh,
        decimal? DsoLocalCurMwh,
        decimal? DsoTariffLocalCurDay,
        decimal? AdmFeeLocalCurMwh,
        decimal? BalFeeLocalCurMwh,
        string Currency,
        long Version
    )
    {
        this.TaxTariffId = TaxTariffId;
        this.TaxLocalCurMwh = TaxLocalCurMwh;
        this.TsoLocalCurMwh = TsoLocalCurMwh;
        this.DsoLocalCurMwh = DsoLocalCurMwh;
        this.DsoTariffLocalCurDay = DsoTariffLocalCurDay;
        this.AdmFeeLocalCurMwh = AdmFeeLocalCurMwh;
        this.BalFeeLocalCurMwh = BalFeeLocalCurMwh;
        this.Currency = Currency;
        this.Version = Version;
    }

    public required Guid TaxTariffId { get; init; }

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
}
