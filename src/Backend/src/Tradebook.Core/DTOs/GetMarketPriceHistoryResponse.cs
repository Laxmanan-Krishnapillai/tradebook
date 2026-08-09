using System.Diagnostics.CodeAnalysis;

namespace Tradebook.Core.DTOs;

public sealed record GetMarketPriceHistoryResponse
{
    public GetMarketPriceHistoryResponse() { }

    [SetsRequiredMembers]
    public GetMarketPriceHistoryResponse(
        IReadOnlyList<MarketPriceDetailsDto> Items,
        int TotalCount,
        int Page,
        int PageSize,
        bool HasNextPage
    )
    {
        this.Items = Items;
        this.TotalCount = TotalCount;
        this.Page = Page;
        this.PageSize = PageSize;
        this.HasNextPage = HasNextPage;
    }

    public required IReadOnlyList<MarketPriceDetailsDto> Items { get; init; }

    public required int TotalCount { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required bool HasNextPage { get; init; }
}
