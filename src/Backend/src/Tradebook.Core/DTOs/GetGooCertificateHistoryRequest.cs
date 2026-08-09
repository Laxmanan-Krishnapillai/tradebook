using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record GetGooCertificateHistoryRequest
{
    public GetGooCertificateHistoryRequest() { }

    public GetGooCertificateHistoryRequest(
        ContractId? ContractId,
        string? Status,
        DateOnly? FromDate,
        DateOnly? ToDate,
        int Page = 1,
        int PageSize = 50
    )
    {
        this.ContractId = ContractId;
        this.Status = Status;
        this.FromDate = FromDate;
        this.ToDate = ToDate;
        this.Page = Page;
        this.PageSize = PageSize;
    }

    [TsOptional]
    public ContractId? ContractId { get; init; }

    [TsOptional]
    public string? Status { get; init; }

    [TsOptional]
    public DateOnly? FromDate { get; init; }

    [TsOptional]
    public DateOnly? ToDate { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
