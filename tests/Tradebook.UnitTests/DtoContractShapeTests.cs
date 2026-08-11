using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Tradebook.Api.Features.Analytics;
using Tradebook.Api.Features.Biotickets;
using Tradebook.Api.Features.CapacityBookings;
using Tradebook.Api.Features.Contracts;
using Tradebook.Api.Features.Dashboards;
using Tradebook.Api.Features.Events;
using Tradebook.Api.Features.GooCertificates;
using Tradebook.Api.Features.Hedges;
using Tradebook.Api.Features.MarketPrices;
using Tradebook.Api.Features.PhysicalDeliveries.GetDeliveryById;
using Tradebook.Api.Features.TaxTariffs;
using Tradebook.Api.Features.Transfers;
using Tradebook.Core.Analytics;
using Tradebook.Core.DTOs;

namespace Tradebook.UnitTests;

public sealed class DtoContractShapeTests
{
    private static readonly NullabilityInfoContext Nullability = new();

    [Fact]
    public void All79AuditedRecordContractsArePropertyBodiedAndSealed()
    {
        var contracts = AuditedContracts();

        Assert.Equal(79, contracts.Length);
        Assert.Equal(79, contracts.Distinct().Count());
        foreach (var contract in contracts)
        {
            Assert.True(contract.IsSealed, $"{contract.FullName} must be sealed.");
            Assert.NotNull(
                contract.GetProperty(
                    "EqualityContract",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )
            );
            Assert.DoesNotContain(
                contract.GetMethods(BindingFlags.Instance | BindingFlags.Public),
                method =>
                    string.Equals(method.Name, "Deconstruct", StringComparison.Ordinal)
                    && method.DeclaringType == contract
            );
        }
    }

    [Fact]
    public void ContractPropertiesAreInitOnlyAndNonOptionalMembersAreRequired()
    {
        foreach (var contract in AuditedContracts())
        {
            foreach (
                var property in contract.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly
                )
            )
            {
                var setter = Assert.IsAssignableFrom<MethodInfo>(property.SetMethod);
                Assert.Contains(
                    typeof(IsExternalInit),
                    setter.ReturnParameter.GetRequiredCustomModifiers()
                );

                var required = property.IsDefined(typeof(RequiredMemberAttribute), false);
                Assert.Equal(!IsOptional(contract, property), required);
            }

            AssertConstructorParametersMatchDeclaredProperties(contract);

            if (
                contract
                    .GetProperties()
                    .Any(property => property.IsDefined(typeof(RequiredMemberAttribute), false))
            )
            {
                foreach (
                    var constructor in contract
                        .GetConstructors()
                        .Where(constructor => constructor.GetParameters().Length > 0)
                )
                {
                    Assert.True(
                        constructor.IsDefined(typeof(SetsRequiredMembersAttribute), false),
                        $"{contract.Name} compatibility constructor must set required members."
                    );
                }
            }
        }
    }

    private static void AssertConstructorParametersMatchDeclaredProperties(Type contract)
    {
        var declaredPropertyNames = contract
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var constructor in contract.GetConstructors())
        {
            foreach (var parameter in constructor.GetParameters())
            {
                Assert.True(
                    parameter.Name is not null && declaredPropertyNames.Contains(parameter.Name),
                    $"{contract.Name} constructor parameter '{parameter.Name}' must exactly match a declared public property."
                );
            }
        }
    }

    [Fact]
    public void PaginationAndCursorDefaultsArePreserved()
    {
        Assert.Equal(1, new GetDeliveryHistoryRequest(null, null, null, null, null, null).Page);
        Assert.Equal(
            50,
            new GetDeliveryHistoryRequest(null, null, null, null, null, null).PageSize
        );
        Assert.Equal(100, new GetMarketPriceHistoryRequest(null, null).PageSize);
        Assert.Equal(
            0,
            new UpsertMarketPriceRequest(
                default,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            ).Version
        );
        Assert.Equal(500, new GetEventsSinceRequest { AfterSequence = 0 }.Limit);
    }

    [Fact]
    public void ForbiddenCommercialMapperAndMediatorNamesAreAbsentFromSourceAndPackages()
    {
        var root = FindRepositoryRoot();
        var forbidden = new[]
        {
            string.Concat("Auto", "Mapper"),
            string.Concat("Media", "tR"),
            string.Concat("Mass", "Transit"),
        };
        var files = Directory
            .EnumerateFiles(Path.Combine(root, "src", "Backend"), "*", SearchOption.AllDirectories)
            .Concat(
                Directory.EnumerateFiles(
                    Path.Combine(root, "tests"),
                    "*",
                    SearchOption.AllDirectories
                )
            )
            .Where(path =>
                !path.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .Where(path =>
                !path.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .Where(path => Path.GetExtension(path) is ".cs" or ".csproj")
            .Append(Path.Combine(root, "Directory.Packages.props"));

        var violations = files
            .SelectMany(path =>
                forbidden
                    .Where(name =>
                        File.ReadAllText(path).Contains(name, StringComparison.OrdinalIgnoreCase)
                    )
                    .Select(name => $"{name}: {Path.GetRelativePath(root, path)}")
            )
            .ToArray();

        Assert.Empty(violations);
    }

    private static Type[] AuditedContracts()
    {
        var coreDtos = typeof(LoginRequest)
            .Assembly.GetTypes()
            .Where(type =>
                string.Equals(
                    type.Namespace,
                    typeof(LoginRequest).Namespace,
                    StringComparison.Ordinal
                )
            )
            .Where(type => type.IsPublic && type.Name is not "FilterOperator")
            .ToArray();
        Assert.Equal(64, coreDtos.Length);

        return coreDtos
            .Concat([
                typeof(JsonQueryAst),
                typeof(TimeDimensionQuery),
                typeof(FilterQuery),
                typeof(SortQuery),
                typeof(AnalyticsQueryResponse),
                typeof(GetBioticketByIdRequest),
                typeof(GetCapacityBookingByIdRequest),
                typeof(GetContractByIdRequest),
                typeof(GetDashboardRequest),
                typeof(GetGooCertificateByIdRequest),
                typeof(GetHedgeByIdRequest),
                typeof(GetMarketPriceByDateRequest),
                typeof(GetDeliveryByIdRequest),
                typeof(GetTaxTariffByIdRequest),
                typeof(GetTransferByIdRequest),
            ])
            .ToArray();
    }

    private static bool IsOptional(Type contract, PropertyInfo property)
    {
        if (Nullable.GetUnderlyingType(property.PropertyType) is not null)
            return true;
        if (
            !property.PropertyType.IsValueType
            && Nullability.Create(property).ReadState == NullabilityState.Nullable
        )
            return true;
        if (
            contract.Name.EndsWith("HistoryRequest", StringComparison.Ordinal)
            && property.Name is "Page" or "PageSize"
        )
            return true;
        return (contract, property.Name)
            is
                (
                    { Name: nameof(UpsertMarketPriceRequest) },
                    nameof(UpsertMarketPriceRequest.Version)
                )
                or
                ({ Name: nameof(GetEventsSinceRequest) }, nameof(GetEventsSinceRequest.Limit));
    }

    private static string FindRepositoryRoot()
    {
        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
