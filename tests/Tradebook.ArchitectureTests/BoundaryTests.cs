using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Tradebook.ArchitectureTests;

public sealed class BoundaryTests
{
    private const string FeatureNamespacePrefix = "Tradebook.Api.Features.";
    private static readonly string[] FeatureSlices = typeof(Program).Assembly.GetTypes()
        .Select(type => type.Namespace)
        .Where(@namespace => @namespace?.StartsWith(FeatureNamespacePrefix, StringComparison.Ordinal) == true)
        .Select(@namespace => @namespace![FeatureNamespacePrefix.Length..].Split('.')[0])
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

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

    public static IEnumerable<object[]> SiblingFeaturePairs() =>
        from source in FeatureSlices
        from target in FeatureSlices
        where !string.Equals(source, target, StringComparison.Ordinal)
        select new object[] { source, target };

    [Theory]
    [MemberData(nameof(SiblingFeaturePairs))]
    public void Feature_slices_do_not_reference_siblings(string source, string target) =>
        Types().That().ResideInNamespace($"Tradebook.Api.Features.{source}", true)
            .Should().NotDependOnAny(Types().That().ResideInNamespace($"Tradebook.Api.Features.{target}", true))
            .Check(Architecture);
}
