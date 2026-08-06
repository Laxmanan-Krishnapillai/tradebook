using Microsoft.Extensions.Caching.Hybrid;
using Tradebook.Core.Interfaces;

namespace Tradebook.Infrastructure.Caching;

public sealed class HybridCacheService(HybridCache cache) : ICacheService
{
    public ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, TimeSpan expiration, CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(key, async ct => await factory(ct), new HybridCacheEntryOptions { Expiration = expiration, LocalCacheExpiration = expiration }, cancellationToken: cancellationToken);

    public ValueTask RemoveAsync(string key, CancellationToken cancellationToken) => cache.RemoveAsync(key, cancellationToken);
}
