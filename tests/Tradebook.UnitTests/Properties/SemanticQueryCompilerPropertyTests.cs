using System.Globalization;
using CsCheck;
using Tradebook.Core.Analytics;

namespace Tradebook.UnitTests.Properties;

public sealed class SemanticQueryCompilerPropertyTests
{
    private const string Seed = "task-22-semantic-compiler";
    private static readonly SemanticQueryCompiler Compiler = new(new SemanticModelLoader());

    [Fact]
    public void GeneratedScalarFiltersAgreeWithTheReferenceOracle()
    {
        Gen.Int.Sample(
            value =>
            {
                var compiled = CompileFilter(
                    "revenue_eur",
                    FilterOperator.GreaterThanOrEqual,
                    value
                );

                Assert.Contains(
                    "HAVING SUM(physical_deliveries.revenue_eur) >= @p0",
                    compiled.SqlText,
                    StringComparison.Ordinal
                );
                Assert.Equal((decimal)value, compiled.Parameters["@p0"]);
            },
            seed: Seed
        );
    }

    [Fact]
    public void ChangingAFilterValuePreservesSqlShapeAndChangesOnlyTheParameter()
    {
        Gen.Int.Sample(
            value =>
            {
                var first = CompileFilter(
                    "book_type",
                    FilterOperator.Equals,
                    value.ToString(CultureInfo.InvariantCulture)
                );
                var second = CompileFilter(
                    "book_type",
                    FilterOperator.Equals,
                    (value + 1).ToString(CultureInfo.InvariantCulture)
                );

                Assert.Equal(first.SqlText, second.SqlText);
                Assert.NotEqual(first.Parameters["@p0"], second.Parameters["@p0"]);
            },
            seed: Seed
        );
    }

    [Fact]
    public void AdversarialIdentifiersAreRejectedAndValuesAreAlwaysParameterized()
    {
        var identifiers = new[]
        {
            "id; DROP TABLE physical_deliveries;--",
            "1=1 OR book_type='x'",
            "\"; DELETE FROM physical_deliveries --",
        };
        var values = new[] { "' OR '1'='1", "');DROP TABLE physical_deliveries;--", "--%_\\" };

        Gen.Int.Sample(
            index =>
            {
                var identifier = identifiers[NonNegativeModulo(index, identifiers.Length)];
                var value = values[NonNegativeModulo(index, values.Length)];

                Assert.Throws<SemanticValidationException>(() =>
                    CompileFilter(identifier, FilterOperator.Equals, value)
                );

                var compiled = CompileFilter("book_type", FilterOperator.Equals, value);
                Assert.DoesNotContain(value, compiled.SqlText, StringComparison.Ordinal);
                Assert.Equal(value, compiled.Parameters["@p0"]);
            },
            seed: Seed
        );
    }

    private static CompiledSqlQuery CompileFilter(
        string member,
        FilterOperator filterOperator,
        object value
    ) =>
        Compiler.Compile(
            new JsonQueryAst(
                "delivery_pnl_analytics",
                ["revenue_eur"],
                null,
                null,
                null,
                [new FilterQuery(member, filterOperator, [value])],
                null,
                null,
                null
            )
        );

    private static int NonNegativeModulo(int value, int divisor) =>
        (int)((uint)value % (uint)divisor);
}
