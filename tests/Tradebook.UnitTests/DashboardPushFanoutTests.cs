using Tradebook.Api.RealTime;
using Tradebook.Core.Domain;

namespace Tradebook.UnitTests;

public sealed class DashboardPushFanoutTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"actorId\":\"not-a-guid\"}")]
    [InlineData("{not-json")]
    public async Task InvalidPrivateDashboardRoutingMetadataFailsFanout(string payload)
    {
        var fanout = new DashboardPushFanout(null!);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fanout.PublishEntityChangedAsync(
                Guid.NewGuid(),
                1,
                OutboxAggregateTypes.WorkspaceDashboard,
                Guid.NewGuid().ToString(),
                "Updated",
                payload,
                default
            )
        );

        Assert.Equal(
            "WorkspaceDashboard outbox payload must contain a UUID actorId.",
            exception.Message
        );
    }
}
