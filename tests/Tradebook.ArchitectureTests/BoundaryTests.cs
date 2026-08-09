using System.Reflection;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Microsoft.AspNetCore.SignalR;
using Tradebook.Api.RealTime.Handlers;
using Tradebook.Core.Messaging;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Tradebook.ArchitectureTests;

public sealed class BoundaryTests
{
    private const string FeatureNamespacePrefix = "Tradebook.Api.Features.";
    private static readonly string[] FeatureSlices = typeof(Program)
        .Assembly.GetTypes()
        .Select(type => type.Namespace)
        .Where(@namespace =>
            @namespace?.StartsWith(FeatureNamespacePrefix, StringComparison.Ordinal) == true
        )
        .Select(@namespace => @namespace![FeatureNamespacePrefix.Length..].Split('.')[0])
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(Program).Assembly,
            typeof(Tradebook.Core.DTOs.CreatePhysicalDeliveryRequest).Assembly,
            typeof(Tradebook.Infrastructure.Data.DeliveryRepository).Assembly
        )
        .Build();

    [Fact]
    public void Core_depends_on_neither_api_nor_infrastructure() =>
        Types()
            .That()
            .ResideInNamespace("Tradebook.Core", true)
            .Should()
            .NotDependOnAny(Types().That().ResideInNamespace("Tradebook.Api", true))
            .AndShould()
            .NotDependOnAny(Types().That().ResideInNamespace("Tradebook.Infrastructure", true))
            .Check(Architecture);

    [Fact]
    public void Api_endpoints_do_not_reference_npgsql() =>
        Classes()
            .That()
            .ResideInNamespace("Tradebook.Api.Features", true)
            .Should()
            .NotDependOnAny(Types().That().ResideInNamespace("Npgsql", true))
            .Check(Architecture);

    [Fact]
    public void Msg07_signalr_hub_context_dependencies_exist_only_in_realtime_handlers()
    {
        const string handlerNamespace = "Tradebook.Api.RealTime.Handlers";
        var hubContextConsumers = typeof(Program)
            .Assembly.GetTypes()
            .Where(DependsOnGenericHubContext)
            .ToArray();

        var consumer = Assert.Single(hubContextConsumers);
        Assert.Equal(typeof(EntityChangedRealtimeHandler), consumer);
        Assert.Equal(handlerNamespace, consumer.Namespace);
        Assert.NotNull(
            consumer.GetMethod(
                nameof(EntityChangedRealtimeHandler.Handle),
                [typeof(EntityChangedDomainEvent), typeof(CancellationToken)]
            )
        );
    }

    public static IEnumerable<object[]> SiblingFeaturePairs() =>
        from source in FeatureSlices
        from target in FeatureSlices
        where !string.Equals(source, target, StringComparison.Ordinal)
        select new object[] { source, target };

    [Theory]
    [MemberData(nameof(SiblingFeaturePairs))]
    public void Feature_slices_do_not_reference_siblings(string source, string target) =>
        Types()
            .That()
            .ResideInNamespace($"Tradebook.Api.Features.{source}", true)
            .Should()
            .NotDependOnAny(
                Types().That().ResideInNamespace($"Tradebook.Api.Features.{target}", true)
            )
            .Check(Architecture);

    private static bool DependsOnGenericHubContext(System.Type type)
    {
        const BindingFlags allMembers =
            BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.DeclaredOnly;

        return type.GetConstructors(allMembers)
                .SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => ContainsGenericHubContext(parameter.ParameterType))
            || type.GetFields(allMembers).Any(field => ContainsGenericHubContext(field.FieldType))
            || type.GetProperties(allMembers)
                .Any(property => ContainsGenericHubContext(property.PropertyType))
            || type.GetMethods(allMembers)
                .Any(method =>
                    ContainsGenericHubContext(method.ReturnType)
                    || method
                        .GetParameters()
                        .Any(parameter => ContainsGenericHubContext(parameter.ParameterType))
                );
    }

    private static bool ContainsGenericHubContext(System.Type type) =>
        type.IsGenericType
        && (
            type.GetGenericTypeDefinition() == typeof(IHubContext<>)
            || type.GetGenericTypeDefinition() == typeof(IHubContext<,>)
            || type.GetGenericArguments().Any(ContainsGenericHubContext)
        );
}
