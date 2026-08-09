using System.Reflection;
using Tradebook.Core.DTOs;
using Xunit;

namespace Tradebook.ArchitectureTests;

public sealed class DomainSurfaceArchRuleTests
{
    [Fact]
    public void DomainAndDtosExposeNoRawGuidOrDecimalIdentifierMoneyMembers()
    {
        var violations = typeof(CreateContractRequest)
            .Assembly.GetExportedTypes()
            .Where(type =>
                type.Namespace?.StartsWith("Tradebook.Core.Domain", StringComparison.Ordinal)
                    == true
                || string.Equals(type.Namespace, "Tradebook.Core.DTOs", StringComparison.Ordinal)
            )
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Where(property =>
                IsGuardedName(property.Name) && IsRawPrimitive(property.PropertyType)
            )
            .Select(property =>
                $"{property.DeclaringType!.FullName}.{property.Name}: {property.PropertyType.Name}"
            )
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Raw domain primitives escaped the public surface:\n" + string.Join("\n", violations)
        );
    }

    private static readonly string[] GuardedValueNames =
    [
        "Price",
        "Quantity",
        "Amount",
        "Total",
        "Notional",
        "Rate",
        "Volume",
        "Capacity",
        "Cost",
        "Revenue",
        "Vat",
        "Tariff",
        "Fee",
        "Eur",
    ];

    private static bool IsGuardedName(string name) =>
        name.EndsWith("Id", StringComparison.Ordinal) || GuardedValueNames.Any(name.Contains);

    private static bool IsRawPrimitive(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) is var underlying
        && (underlying == typeof(Guid) || underlying == typeof(decimal));
}
