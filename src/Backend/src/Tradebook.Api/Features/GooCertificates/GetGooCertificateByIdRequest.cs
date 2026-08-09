using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Tradebook.Api.Security;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.Api.Features.GooCertificates;

public sealed record GetGooCertificateByIdRequest
{
    public GetGooCertificateByIdRequest() { }

    [SetsRequiredMembers]
    public GetGooCertificateByIdRequest(Guid GooCertificateTransactionId) =>
        this.GooCertificateTransactionId = GooCertificateTransactionId;

    public required Guid GooCertificateTransactionId { get; init; }
}
