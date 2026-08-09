using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record HedgeDetailsDto
{
    public HedgeDetailsDto() { }

    [SetsRequiredMembers]
    public HedgeDetailsDto(
        Guid HedgeId,
        Guid ContractId,
        DateOnly Month,
        decimal? HedgeAmountMwh,
        decimal? HedgePriceEurMwh,
        long Version,
        DateTime CreatedAt,
        DateTime UpdatedAt
    )
    {
        this.HedgeId = HedgeId;
        this.ContractId = ContractId;
        this.Month = Month;
        this.HedgeAmountMwh = HedgeAmountMwh;
        this.HedgePriceEurMwh = HedgePriceEurMwh;
        this.Version = Version;
        this.CreatedAt = CreatedAt;
        this.UpdatedAt = UpdatedAt;
    }

    public required Guid HedgeId { get; init; }

    public required Guid ContractId { get; init; }

    public required DateOnly Month { get; init; }

    [TsOptional]
    public decimal? HedgeAmountMwh { get; init; }

    [TsOptional]
    public decimal? HedgePriceEurMwh { get; init; }

    public required long Version { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required DateTime UpdatedAt { get; init; }
}
