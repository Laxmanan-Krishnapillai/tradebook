using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Contracts;

public sealed record GetContractByIdRequest
{
    public GetContractByIdRequest() { }

    [SetsRequiredMembers]
    public GetContractByIdRequest(Guid ContractId) => this.ContractId = ContractId;

    public required Guid ContractId { get; init; }
}
