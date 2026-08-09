using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record GetTaxTariffHistoryRequest
{
    public GetTaxTariffHistoryRequest() { }

    public GetTaxTariffHistoryRequest(
        Guid? ContractId,
        DateOnly? EffectiveOn,
        int Page = 1,
        int PageSize = 50
    )
    {
        this.ContractId = ContractId;
        this.EffectiveOn = EffectiveOn;
        this.Page = Page;
        this.PageSize = PageSize;
    }

    [TsOptional]
    public Guid? ContractId { get; init; }

    [TsOptional]
    public DateOnly? EffectiveOn { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
