namespace Tradebook.Core.Interfaces;

public interface ICacheService
{
    ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, TimeSpan expiration, CancellationToken cancellationToken);
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken);
}
