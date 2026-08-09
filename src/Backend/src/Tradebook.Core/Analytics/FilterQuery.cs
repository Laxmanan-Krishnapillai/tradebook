using System.Diagnostics.CodeAnalysis;

namespace Tradebook.Core.Analytics;

public sealed record FilterQuery
{
    public FilterQuery() { }

    [SetsRequiredMembers]
    public FilterQuery(string Member, FilterOperator Operator, IReadOnlyList<object> Values)
    {
        this.Member = Member;
        this.Operator = Operator;
        this.Values = Values;
    }

    public required string Member { get; init; }

    public required FilterOperator Operator { get; init; }

    public required IReadOnlyList<object> Values { get; init; }
}
