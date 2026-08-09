using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateHedgeRequest
{
    public CreateHedgeRequest() { }

    [SetsRequiredMembers]
    public CreateHedgeRequest(
        Guid ContractId,
        DateOnly Month,
        decimal? HedgeAmountMwh,
        decimal? HedgePriceEurMwh
    )
    {
        this.ContractId = ContractId;
        this.Month = Month;
        this.HedgeAmountMwh = HedgeAmountMwh;
        this.HedgePriceEurMwh = HedgePriceEurMwh;
    }

    public required Guid ContractId { get; init; }

    public required DateOnly Month { get; init; }

    [TsOptional]
    public decimal? HedgeAmountMwh { get; init; }

    [TsOptional]
    public decimal? HedgePriceEurMwh { get; init; }
}
