using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record UpdateTaxTariffRequest
{
    public UpdateTaxTariffRequest() { }

    [SetsRequiredMembers]
    public UpdateTaxTariffRequest(
        TaxTariffId TaxTariffId,
        Amount? TaxLocalCurMwh,
        Amount? TsoLocalCurMwh,
        Amount? DsoLocalCurMwh,
        Amount? DsoTariffLocalCurDay,
        Amount? AdmFeeLocalCurMwh,
        Amount? BalFeeLocalCurMwh,
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

    public required TaxTariffId TaxTariffId { get; init; }

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
}
