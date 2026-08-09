using System.Text.Json;
using Tradebook.Core.Domain;
using Tradebook.Core.Messaging;

namespace Tradebook.UnitTests;

public sealed class DashboardPushFanoutTests
{
    [Fact]
    public void CreateBuildsPublicEntityPayloadAndPreservesEnvelopeMetadata()
    {
        var aggregateId = Guid.NewGuid().ToString();

        var domainEvent = EntityChangedDomainEvent.Create(
            RealtimeAggregateTypes.PhysicalDelivery,
            aggregateId,
            "Updated",
            7
        );

        Assert.NotEqual(Guid.Empty, domainEvent.EventId);
        Assert.Equal(RealtimeAggregateTypes.PhysicalDelivery, domainEvent.AggregateType);
        Assert.Equal(aggregateId, domainEvent.AggregateId);
        Assert.Equal("Updated", domainEvent.EventType);

        using var payload = JsonDocument.Parse(domainEvent.PayloadJson);
        Assert.Equal(aggregateId, payload.RootElement.GetProperty("aggregateId").GetString());
        Assert.Equal(7, payload.RootElement.GetProperty("version").GetInt64());
        Assert.False(payload.RootElement.TryGetProperty("reason", out _));
        Assert.False(payload.RootElement.TryGetProperty("actorId", out _));
        Assert.False(payload.RootElement.TryGetProperty("dashboardId", out _));
        Assert.Equal(2, payload.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public void CreateIncludesMutationReasonInPublicEntityPayload()
    {
        var domainEvent = EntityChangedDomainEvent.Create(
            RealtimeAggregateTypes.Contract,
            Guid.NewGuid().ToString(),
            "Deactivated",
            3,
            "Expired agreement"
        );

        using var payload = JsonDocument.Parse(domainEvent.PayloadJson);
        Assert.Equal("Expired agreement", payload.RootElement.GetProperty("reason").GetString());
        Assert.Equal(3, payload.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public void CreateBuildsPrivateDashboardPayloadFromActorMetadata()
    {
        var dashboardId = Guid.NewGuid().ToString();
        var actorId = Guid.NewGuid();

        var domainEvent = EntityChangedDomainEvent.Create(
            RealtimeAggregateTypes.WorkspaceDashboard,
            dashboardId,
            "Created",
            1,
            actorId: actorId
        );

        Assert.Equal(RealtimeAggregateTypes.WorkspaceDashboard, domainEvent.AggregateType);
        Assert.Equal(dashboardId, domainEvent.AggregateId);
        Assert.Equal("Created", domainEvent.EventType);

        using var payload = JsonDocument.Parse(domainEvent.PayloadJson);
        Assert.Equal(dashboardId, payload.RootElement.GetProperty("dashboardId").GetString());
        Assert.Equal(actorId, payload.RootElement.GetProperty("actorId").GetGuid());
        Assert.Equal(1, payload.RootElement.GetProperty("version").GetInt64());
        Assert.False(payload.RootElement.TryGetProperty("aggregateId", out _));
        Assert.False(payload.RootElement.TryGetProperty("reason", out _));
        Assert.Equal(3, payload.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public void CreateGeneratesADistinctEventIdForEachDomainEvent()
    {
        var first = EntityChangedDomainEvent.Create(
            RealtimeAggregateTypes.Hedge,
            Guid.NewGuid().ToString(),
            "Created",
            1
        );
        var second = EntityChangedDomainEvent.Create(
            RealtimeAggregateTypes.Hedge,
            Guid.NewGuid().ToString(),
            "Created",
            1
        );

        Assert.NotEqual(Guid.Empty, first.EventId);
        Assert.NotEqual(Guid.Empty, second.EventId);
        Assert.NotEqual(first.EventId, second.EventId);
    }

    [Fact]
    public void CreateRejectsAnUnknownAggregateType()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            EntityChangedDomainEvent.Create("Unknown", "1", "Created", 1)
        );

        Assert.Equal("aggregateType", exception.ParamName);
        Assert.Equal("Unknown", exception.ActualValue);
    }

    [Fact]
    public void CreateRequiresActorMetadataForPrivateDashboards()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            EntityChangedDomainEvent.Create(
                RealtimeAggregateTypes.WorkspaceDashboard,
                Guid.NewGuid().ToString(),
                "Updated",
                2
            )
        );

        Assert.Equal("actorId", exception.ParamName);
    }

    [Fact]
    public void CreateRejectsActorMetadataForPublicEntities()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            EntityChangedDomainEvent.Create(
                RealtimeAggregateTypes.Transfer,
                Guid.NewGuid().ToString(),
                "Updated",
                2,
                actorId: Guid.NewGuid()
            )
        );

        Assert.Equal("actorId", exception.ParamName);
    }
}
