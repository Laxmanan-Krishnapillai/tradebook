using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record GetDeliveryHistoryResponse
{
    public GetDeliveryHistoryResponse() { }

    [SetsRequiredMembers]
    public GetDeliveryHistoryResponse(
        IReadOnlyList<PhysicalDeliveryDetailsDto> Items,
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

    public required IReadOnlyList<PhysicalDeliveryDetailsDto> Items { get; init; }

    public required int TotalCount { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required bool HasNextPage { get; init; }
}
