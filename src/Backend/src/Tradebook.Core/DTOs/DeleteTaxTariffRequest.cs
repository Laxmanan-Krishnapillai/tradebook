using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;

namespace Tradebook.Core.DTOs;

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
