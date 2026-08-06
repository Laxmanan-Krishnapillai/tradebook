using Tradebook.Core.Domain.Entities;

namespace Tradebook.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken);
}
