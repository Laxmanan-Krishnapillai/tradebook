using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

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

    public Amount? TaxLocalCurMwh { get; init; }

    public Amount? TsoLocalCurMwh { get; init; }

    public Amount? DsoLocalCurMwh { get; init; }

    public Amount? DsoTariffLocalCurDay { get; init; }

    public Amount? AdmFeeLocalCurMwh { get; init; }

    public Amount? BalFeeLocalCurMwh { get; init; }
    public required string Currency { get; init; }
    public required long Version { get; init; }
}
