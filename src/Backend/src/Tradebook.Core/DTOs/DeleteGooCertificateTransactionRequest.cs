using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record DeleteGooCertificateTransactionRequest
{
    public DeleteGooCertificateTransactionRequest() { }

    [SetsRequiredMembers]
    public DeleteGooCertificateTransactionRequest(
        GooCertificateTransactionId GooCertificateTransactionId,
        string Reason,
        long Version
    )
    {
        this.GooCertificateTransactionId = GooCertificateTransactionId;
        this.Reason = Reason;
        this.Version = Version;
    }

    public required GooCertificateTransactionId GooCertificateTransactionId { get; init; }
    public required string Reason { get; init; }
    public required long Version { get; init; }
}
