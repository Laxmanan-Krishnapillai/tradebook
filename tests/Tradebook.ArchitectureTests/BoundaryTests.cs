using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Tradebook.ArchitectureTests;

public sealed class BoundaryTests
{
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(Program).Assembly,
            typeof(Tradebook.Core.DTOs.CreatePhysicalDeliveryRequest).Assembly,
            typeof(Tradebook.Infrastructure.Data.DeliveryRepository).Assembly)
        .Build();

    [Fact]
    public void Core_depends_on_neither_api_nor_infrastructure() =>
        Types().That().ResideInNamespace("Tradebook.Core", true)
            .Should().NotDependOnAny(Types().That().ResideInNamespace("Tradebook.Api", true))
            .AndShould().NotDependOnAny(Types().That().ResideInNamespace("Tradebook.Infrastructure", true))
            .Check(Architecture);

    [Fact]
    public void Api_endpoints_do_not_reference_npgsql() =>
        Classes().That().ResideInNamespace("Tradebook.Api.Features", true)
            .Should().NotDependOnAny(Types().That().ResideInNamespace("Npgsql", true))
            .Check(Architecture);

    [Fact]
    public void Physical_delivery_slice_does_not_reference_market_price_slice() =>
        Types().That().ResideInNamespace("Tradebook.Api.Features.PhysicalDeliveries", true)
            .Should().NotDependOnAny(Types().That().ResideInNamespace("Tradebook.Api.Features.MarketPrices", true))
            .Check(Architecture);
}
