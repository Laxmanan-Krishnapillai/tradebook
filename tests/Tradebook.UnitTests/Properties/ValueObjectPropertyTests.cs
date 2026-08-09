using CsCheck;
using Tradebook.Core.Domain;
using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;

namespace Tradebook.UnitTests.Properties;

public sealed class ValueObjectPropertyTests
{
    private const string Seed = "task-22-value-objects";

    [Fact]
    public void DeliveryIdFromValueRoundTripsForEveryNonEmptyGuid()
    {
        Gen.Int.Sample(
            value =>
            {
                var raw = Guid.CreateVersion7(
                    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMilliseconds(
                        (uint)value
                    )
                );
                var deliveryId = DeliveryId.From(raw);

                Assert.Equal(raw, deliveryId.Value);
                Assert.Equal(deliveryId, DeliveryId.From(deliveryId.Value));
            },
            seed: Seed
        );
    }

    [Fact]
    public void PriceValidationAcceptsExactlyNonNegativeValuesWithAtMostFourDecimals()
    {
        Gen.Int.Sample(
            value =>
            {
                var valid = (uint)value / 10_000m;
                Assert.Equal(valid, Price.From(valid).Value);

                var invalidScale = valid + 0.00001m;
                Assert.Throws<TradebookDomainException>(() => Price.From(invalidScale));

                if (valid > 0m)
                {
                    Assert.Throws<TradebookDomainException>(() => Price.From(-valid));
                }
            },
            seed: Seed
        );
    }
}
