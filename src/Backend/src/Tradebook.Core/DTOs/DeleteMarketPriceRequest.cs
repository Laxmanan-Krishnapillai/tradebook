using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record DeleteMarketPriceRequest
{
    public DeleteMarketPriceRequest() { }

    [SetsRequiredMembers]
    public DeleteMarketPriceRequest(DateOnly PriceDate, string Reason, long Version)
    {
        this.PriceDate = PriceDate;
        this.Reason = Reason;
        this.Version = Version;
    }

    public required DateOnly PriceDate { get; init; }

    public required string Reason { get; init; }

    public required long Version { get; init; }
}
