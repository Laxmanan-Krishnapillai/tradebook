using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record DeactivateContractRequest
{
    public DeactivateContractRequest() { }

    [SetsRequiredMembers]
    public DeactivateContractRequest(ContractId ContractId, string Reason, long Version)
    {
        this.ContractId = ContractId;
        this.Reason = Reason;
        this.Version = Version;
    }

    public required ContractId ContractId { get; init; }
    public required string Reason { get; init; }
    public required long Version { get; init; }
}
