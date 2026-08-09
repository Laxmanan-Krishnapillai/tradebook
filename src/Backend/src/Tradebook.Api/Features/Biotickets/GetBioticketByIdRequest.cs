using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Biotickets;

public sealed record GetBioticketByIdRequest
{
    public GetBioticketByIdRequest() { }

    [SetsRequiredMembers]
    public GetBioticketByIdRequest(Guid BioticketId) => this.BioticketId = BioticketId;

    public required Guid BioticketId { get; init; }
}
