using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record DeleteTaxTariffRequest
{
    public DeleteTaxTariffRequest() { }

    [SetsRequiredMembers]
    public DeleteTaxTariffRequest(Guid TaxTariffId, string Reason, long Version)
    {
        this.TaxTariffId = TaxTariffId;
        this.Reason = Reason;
        this.Version = Version;
    }

    public required Guid TaxTariffId { get; init; }
    public required string Reason { get; init; }
    public required long Version { get; init; }
}
