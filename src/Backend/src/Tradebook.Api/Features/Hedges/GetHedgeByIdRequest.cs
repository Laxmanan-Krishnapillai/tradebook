using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Hedges;

public sealed record GetHedgeByIdRequest
{
    public GetHedgeByIdRequest() { }

    [SetsRequiredMembers]
    public GetHedgeByIdRequest(Guid HedgeId) => this.HedgeId = HedgeId;

    public required Guid HedgeId { get; init; }
}
