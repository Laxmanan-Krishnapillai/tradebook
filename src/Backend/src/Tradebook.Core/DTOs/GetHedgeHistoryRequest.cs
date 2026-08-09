using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record GetHedgeHistoryRequest
{
    public GetHedgeHistoryRequest() { }

    public GetHedgeHistoryRequest(
        ContractId? ContractId,
        DateOnly? FromMonth,
        DateOnly? ToMonth,
        int Page = 1,
        int PageSize = 50
    )
    {
        this.ContractId = ContractId;
        this.FromMonth = FromMonth;
        this.ToMonth = ToMonth;
        this.Page = Page;
        this.PageSize = PageSize;
    }

    [TsOptional]
    public ContractId? ContractId { get; init; }

    [TsOptional]
    public DateOnly? FromMonth { get; init; }

    [TsOptional]
    public DateOnly? ToMonth { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}
