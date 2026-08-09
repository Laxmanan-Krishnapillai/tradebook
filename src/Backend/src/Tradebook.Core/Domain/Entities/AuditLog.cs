namespace Tradebook.Core.Domain.Entities;

public sealed class AuditLog
{
    public Guid AuditId { get; init; }
    public required string EntityName { get; init; }
    public required string EntityId { get; init; }
    public Guid ActorId { get; init; }
}
