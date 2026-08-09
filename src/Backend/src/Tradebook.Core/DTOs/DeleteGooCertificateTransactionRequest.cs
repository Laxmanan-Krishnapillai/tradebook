using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record DeleteGooCertificateTransactionRequest
{
    public DeleteGooCertificateTransactionRequest() { }

    [SetsRequiredMembers]
    public DeleteGooCertificateTransactionRequest(
        Guid GooCertificateTransactionId,
        string Reason,
        long Version
    )
    {
        this.GooCertificateTransactionId = GooCertificateTransactionId;
        this.Reason = Reason;
        this.Version = Version;
    }

    public required Guid GooCertificateTransactionId { get; init; }
    public required string Reason { get; init; }
    public required long Version { get; init; }
}
