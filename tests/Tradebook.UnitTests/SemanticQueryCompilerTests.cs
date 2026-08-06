using Tradebook.Core.Analytics;

namespace Tradebook.UnitTests;

public sealed class SemanticQueryCompilerTests
{
    private readonly SemanticQueryCompiler _compiler = new(new SemanticModelLoader());

    [Fact]
    public void Quarter_granularity_is_emitted_in_select_and_group_by()
    {
        var query = _compiler.Compile(new JsonQueryAst("delivery_pnl_analytics", ["revenue_eur"], null, null, [new TimeDimensionQuery("supply_month", "quarter", null)], null, null, null, null));
        Assert.Equal(2, query.SqlText.Split("date_trunc('quarter'", StringSplitOptions.None).Length - 1);
    }

    [Theory]
    [InlineData("unknown_dimension")]
    [InlineData("revenue_eur; drop table physical_deliveries")]
    public void Unknown_dimensions_are_rejected(string member)
    {
        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst("delivery_pnl_analytics", null, null, [member], null, null, null, null, null)));
    }

    [Fact]
    public void Filter_values_are_bound_and_contains_and_not_in_are_implemented()
    {
        const string injection = "'; DROP TABLE physical_deliveries; --";
        var query = _compiler.Compile(new JsonQueryAst("delivery_pnl_analytics", ["revenue_eur"], null, null, null,
            [new FilterQuery("book_type", FilterOperator.Contains, [injection]), new FilterQuery("status", FilterOperator.NotIn, ["Draft", "Cancelled"])], null, null, null));
        Assert.Contains("ILIKE @p0", query.SqlText); Assert.Contains("NOT IN (@p1, @p2)", query.SqlText); Assert.DoesNotContain(injection, query.SqlText); Assert.Equal($"%{injection}%", query.Parameters["@p0"]);
    }

    [Fact]
    public void Counterparty_dimension_uses_deterministic_target_join_chain()
    {
        var query = _compiler.Compile(new JsonQueryAst("delivery_pnl_analytics", null, null, ["counterparty_segment"], null, null, null, null, null));
        Assert.Contains("FROM physical_deliveries", query.SqlText); Assert.True(query.SqlText.IndexOf("JOIN contracts", StringComparison.Ordinal) < query.SqlText.IndexOf("JOIN counterparties", StringComparison.Ordinal));
    }

    [Fact]
    public void Sort_members_must_be_selected()
    {
        Assert.Throws<SemanticValidationException>(() => _compiler.Compile(new JsonQueryAst("delivery_pnl_analytics", ["revenue_eur"], null, null, null, null, [new SortQuery("volume_mwh", "desc")], null, null)));
    }
}
