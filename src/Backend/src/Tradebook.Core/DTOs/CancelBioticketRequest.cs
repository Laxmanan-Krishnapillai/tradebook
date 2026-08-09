using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CancelBioticketRequest
{
    public CancelBioticketRequest() { }

    [SetsRequiredMembers]
    public CancelBioticketRequest(Guid BioticketId, string Reason, long Version)
    {
        this.BioticketId = BioticketId;
        this.Reason = Reason;
        this.Version = Version;
    }

    public required Guid BioticketId { get; init; }
    public required string Reason { get; init; }
    public required long Version { get; init; }
}
