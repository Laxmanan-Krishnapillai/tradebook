using System.Diagnostics.CodeAnalysis;
using Tradebook.Core.Domain.ValueObjects.Ids;

namespace Tradebook.Core.DTOs;

public sealed record LoginResponse
{
    public LoginResponse() { }

    [SetsRequiredMembers]
    public LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, UserId ActorId)
    {
        this.AccessToken = AccessToken;
        this.ExpiresAtUtc = ExpiresAtUtc;
        this.ActorId = ActorId;
    }

    public required string AccessToken { get; init; }

    public required DateTimeOffset ExpiresAtUtc { get; init; }

    public required UserId ActorId { get; init; }
}
