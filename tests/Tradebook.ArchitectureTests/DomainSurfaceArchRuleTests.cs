using System.Reflection;
using Tradebook.Core.DTOs;
using Xunit;

namespace Tradebook.ArchitectureTests;

public sealed class DomainSurfaceArchRuleTests
{
    [Fact]
    public void Domain_and_dtos_expose_no_raw_guid_or_decimal_identifier_money_members()
    {
        var violations = typeof(CreateContractRequest)
            .Assembly.GetExportedTypes()
            .Where(type =>
                type.Namespace?.StartsWith("Tradebook.Core.Domain", StringComparison.Ordinal)
                    == true
                || type.Namespace == "Tradebook.Core.DTOs"
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

    private static bool IsGuardedName(string name) =>
        name.EndsWith("Id", StringComparison.Ordinal)
        || new[]
        {
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
        }.Any(name.Contains);

    private static bool IsRawPrimitive(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) is var underlying
        && (underlying == typeof(Guid) || underlying == typeof(decimal));
}
