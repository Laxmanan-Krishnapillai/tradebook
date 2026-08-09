using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record GetCapacityBookingHistoryRequest
{
    public GetCapacityBookingHistoryRequest() { }

    public GetCapacityBookingHistoryRequest(
        Guid? ContractId,
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
    public Guid? ContractId { get; init; }

    [TsOptional]
    public DateOnly? FromMonth { get; init; }

    [TsOptional]
    public DateOnly? ToMonth { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
