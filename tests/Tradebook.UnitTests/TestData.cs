using Tradebook.Core.Domain.Entities;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

public static class TestData
{
    public static PhysicalDeliveryDetailsDto Delivery(Guid? deliveryId = null, long version = 1) =>
        new(
            deliveryId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            "TEST45.SG.2601.NOQS-1-2026",
            "Sales",
            new DateOnly(2026, 1, 1),
            null,
            10m,
            9m,
            9m,
            "TTF",
            100m,
            100m,
            25m,
            125m,
            "Awaiting",
            version,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch
        );
}
