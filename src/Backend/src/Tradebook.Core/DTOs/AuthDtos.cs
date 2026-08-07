using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record LoginRequest(string Username, string Password);

[ExportTsInterface]
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, Guid ActorId);
