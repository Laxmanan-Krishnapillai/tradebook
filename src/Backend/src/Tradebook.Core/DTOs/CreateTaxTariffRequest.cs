using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.Core.DTOs;

public sealed record CreateTaxTariffRequest
{
    public CreateTaxTariffRequest() { }

    [SetsRequiredMembers]
    public CreateTaxTariffRequest(
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
        string Currency
    )
    {
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
    }

    public required ContractId ContractId { get; init; }

    public CounterpartyId? CounterpartyId { get; init; }
    public required DateOnly PeriodStart { get; init; }
    public required DateOnly PeriodEnd { get; init; }

    public Amount? TaxLocalCurMwh { get; init; }

    public Amount? TsoLocalCurMwh { get; init; }

    public Amount? DsoLocalCurMwh { get; init; }

    public Amount? DsoTariffLocalCurDay { get; init; }

    public Amount? AdmFeeLocalCurMwh { get; init; }

    public Amount? BalFeeLocalCurMwh { get; init; }
    public required string Currency { get; init; }
}
