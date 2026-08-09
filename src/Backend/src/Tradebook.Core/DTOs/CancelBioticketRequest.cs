using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CancelBioticketRequest
{
    public CancelBioticketRequest() { }

    [SetsRequiredMembers]
    public CancelBioticketRequest(BioticketDeliveryId BioticketId, string Reason, long Version)
    {
        this.BioticketId = BioticketId;
        this.Reason = Reason;
        this.Version = Version;
    }

    public required BioticketDeliveryId BioticketId { get; init; }
    public required string Reason { get; init; }
    public required long Version { get; init; }
}
