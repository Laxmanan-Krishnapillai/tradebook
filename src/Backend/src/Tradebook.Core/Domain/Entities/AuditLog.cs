using Tradebook.Core.Domain.ValueObjects.Ids;

namespace Tradebook.Core.Domain.Entities;

public sealed class AuditLog
{
    public AuditLogId AuditId { get; init; }
    public required string EntityName { get; init; }
    public required string EntityId { get; init; }
    public UserId ActorId { get; init; }
}
