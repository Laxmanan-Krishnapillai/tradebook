using Tradebook.Core.Domain.Entities;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

public sealed class FakeUserRepository : IUserRepository
{
    public IDictionary<string, User> Users { get; } =
        new Dictionary<string, User>(StringComparer.Ordinal);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        Task.FromResult(Users.TryGetValue(username, out var user) ? user : null);
}
