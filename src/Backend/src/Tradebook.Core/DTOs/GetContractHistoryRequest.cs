using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
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

    [TsOptional]
    public CounterpartyId? CounterpartyId { get; init; }

    [TsOptional]
    public string? ProductType { get; init; }

    [TsOptional]
    public string? Action { get; init; }

    [TsOptional]
    public bool? IsActive { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
