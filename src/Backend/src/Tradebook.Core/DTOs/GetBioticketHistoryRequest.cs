using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;

namespace Tradebook.Core.DTOs;

public sealed record GetBioticketHistoryRequest
{
    public GetBioticketHistoryRequest() { }

    public GetBioticketHistoryRequest(
        ContractId? ContractId,
        string? BookType,
        string? Status,
        DateOnly? FromMonth,
        DateOnly? ToMonth,
        int Page = 1,
        int PageSize = 50
    )
    {
        this.ContractId = ContractId;
        this.BookType = BookType;
        this.Status = Status;
        this.FromMonth = FromMonth;
        this.ToMonth = ToMonth;
        this.Page = Page;
        this.PageSize = PageSize;
    }

    public ContractId? ContractId { get; init; }

    public string? BookType { get; init; }

    public string? Status { get; init; }

    public DateOnly? FromMonth { get; init; }

    public DateOnly? ToMonth { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
