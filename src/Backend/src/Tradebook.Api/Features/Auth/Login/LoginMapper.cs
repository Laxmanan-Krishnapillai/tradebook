using Riok.Mapperly.Abstractions;
using Tradebook.Core.Domain.Entities;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.Auth.Login;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class LoginMapper
{
    [MapProperty(nameof(User.Id), nameof(LoginResponse.ActorId))]
    public static partial LoginResponse ToResponse(
        User user,
        string accessToken,
        DateTimeOffset expiresAtUtc
    );
}
