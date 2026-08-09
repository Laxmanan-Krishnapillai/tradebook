using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record GetMarketPriceHistoryRequest
{
    public GetMarketPriceHistoryRequest() { }

    public GetMarketPriceHistoryRequest(
        DateOnly? FromDate,
        DateOnly? ToDate,
        int Page = 1,
        int PageSize = 100
    )
    {
        this.FromDate = FromDate;
        this.ToDate = ToDate;
        this.Page = Page;
        this.PageSize = PageSize;
    }

    [TsOptional]
    public DateOnly? FromDate { get; init; }

    [TsOptional]
    public DateOnly? ToDate { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 100;
}
