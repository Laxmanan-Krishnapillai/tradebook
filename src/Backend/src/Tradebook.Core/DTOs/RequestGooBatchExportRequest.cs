using System.Diagnostics.CodeAnalysis;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record RequestGooBatchExportRequest
{
    public RequestGooBatchExportRequest() { }

    [SetsRequiredMembers]
    public RequestGooBatchExportRequest(Guid GooCertificateTransactionId, long Version)
    {
        this.GooCertificateTransactionId = GooCertificateTransactionId;
        this.Version = Version;
    }

    public required Guid GooCertificateTransactionId { get; init; }
    public required long Version { get; init; }
}
