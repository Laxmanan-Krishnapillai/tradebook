using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;

namespace Tradebook.Core.DTOs;

public sealed record GetContractHistoryRequest
{
    public GetContractHistoryRequest() { }

    public GetContractHistoryRequest(
        CounterpartyId? CounterpartyId,
        string? ProductType,
        string? Action,
        bool? IsActive,
        int Page = 1,
        int PageSize = 50
    )
    {
        this.CounterpartyId = CounterpartyId;
        this.ProductType = ProductType;
        this.Action = Action;
        this.IsActive = IsActive;
        this.Page = Page;
        this.PageSize = PageSize;
    }

    public CounterpartyId? CounterpartyId { get; init; }

    public string? ProductType { get; init; }

    public string? Action { get; init; }

    public bool? IsActive { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
