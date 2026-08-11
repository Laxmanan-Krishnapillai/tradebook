using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Tradebook.Core.Domain.ValueObjects.Ids;

namespace Tradebook.Core.DTOs;

public sealed record ActivityEntryDto
{
    public ActivityEntryDto() { }

    [SetsRequiredMembers]
    public ActivityEntryDto(
        AuditLogId AuditId,
        string Operation,
        UserId? ActorId,
        DateTimeOffset OccurredAt,
        JsonElement Changes
    )
    {
        this.AuditId = AuditId;
        this.Operation = Operation;
        this.ActorId = ActorId;
        this.OccurredAt = OccurredAt;
        this.Changes = Changes;
    }

    public required AuditLogId AuditId { get; init; }

    public required string Operation { get; init; }

    public UserId? ActorId { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required JsonElement Changes { get; init; }
}
