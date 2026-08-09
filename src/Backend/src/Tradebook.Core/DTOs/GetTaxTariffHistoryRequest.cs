using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;

namespace Tradebook.Core.DTOs;

public sealed record GetTaxTariffHistoryRequest
{
    public GetTaxTariffHistoryRequest() { }

    public GetTaxTariffHistoryRequest(
        ContractId? ContractId,
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

    public ContractId? ContractId { get; init; }

    public DateOnly? EffectiveOn { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
