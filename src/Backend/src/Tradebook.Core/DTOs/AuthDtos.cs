using TypeGen.Core.TypeAnnotations;
using Tradebook.Core.Domain.ValueObjects.Ids;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record LoginRequest(string Username, string Password);

[ExportTsInterface]
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, UserId ActorId);
