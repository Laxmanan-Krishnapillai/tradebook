using Tradebook.Core.Domain.Entities;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

public sealed class FakeCacheService : ICacheService
{
    public IList<string> RequestedKeys { get; } = [];
    public IList<string> RemovedKeys { get; } = [];

    public async ValueTask<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        TimeSpan expiration,
        CancellationToken cancellationToken
    )
    {
        RequestedKeys.Add(key);
        return await factory(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken)
    {
        RemovedKeys.Add(key);
        return ValueTask.CompletedTask;
    }
}
