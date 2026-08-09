using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;

namespace Tradebook.Core.DTOs;

public sealed record RequestGooBatchExportRequest
{
    public RequestGooBatchExportRequest() { }

    [SetsRequiredMembers]
    public RequestGooBatchExportRequest(
        GooCertificateTransactionId GooCertificateTransactionId,
        long Version
    )
    {
        this.GooCertificateTransactionId = GooCertificateTransactionId;
        this.Version = Version;
    }

    public required GooCertificateTransactionId GooCertificateTransactionId { get; init; }
    public required long Version { get; init; }
}
