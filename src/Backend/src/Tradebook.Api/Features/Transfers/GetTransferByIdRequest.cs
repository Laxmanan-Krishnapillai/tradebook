using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.Transfers;

public sealed record GetTransferByIdRequest
{
    public GetTransferByIdRequest() { }

    [SetsRequiredMembers]
    public GetTransferByIdRequest(Guid TransferId) => this.TransferId = TransferId;

    public required Guid TransferId { get; init; }
}
