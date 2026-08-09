using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Tradebook.Core.Interfaces;
using Tradebook.Infrastructure.Caching;

namespace Tradebook.UnitTests;

public sealed class HybridCacheServiceTests
{
    [Fact]
    public async Task RepeatedReadsExecuteTheFactoryOnce()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        await using var provider = services.BuildServiceProvider();
        var cache = new HybridCacheService(provider.GetRequiredService<HybridCache>());
        var calls = 0;
        ValueTask<int> FactoryAsync(CancellationToken _) => new(Interlocked.Increment(ref calls));

        Assert.Equal(
            1,
            await cache.GetOrCreateAsync(
                "delivery:test",
                FactoryAsync,
                TimeSpan.FromMinutes(5),
                CancellationToken.None
            )
        );
        Assert.Equal(
            1,
            await cache.GetOrCreateAsync(
                "delivery:test",
                FactoryAsync,
                TimeSpan.FromMinutes(5),
                CancellationToken.None
            )
        );
        Assert.Equal(1, calls);
    }
}
