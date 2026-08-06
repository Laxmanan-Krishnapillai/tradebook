namespace Tradebook.Core.Domain.Entities;

public sealed class User
{
    public Guid Id { get; init; }
    public required string Username { get; init; }
    public required string PasswordHash { get; init; }
    public required string[] Roles { get; init; }
    public bool IsActive { get; init; }
}
