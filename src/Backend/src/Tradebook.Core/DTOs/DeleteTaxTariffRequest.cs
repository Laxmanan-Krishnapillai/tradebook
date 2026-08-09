using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record DeleteTaxTariffRequest
{
    public DeleteTaxTariffRequest() { }

    [SetsRequiredMembers]
    public DeleteTaxTariffRequest(TaxTariffId TaxTariffId, string Reason, long Version)
    {
        this.TaxTariffId = TaxTariffId;
        this.Reason = Reason;
        this.Version = Version;
    }

    public required TaxTariffId TaxTariffId { get; init; }
    public required string Reason { get; init; }
    public required long Version { get; init; }
}
