using System.Diagnostics.CodeAnalysis;

namespace Tradebook.Core.Analytics;

public sealed record SortQuery
{
    public SortQuery() { }

    [SetsRequiredMembers]
    public SortQuery(string Member, string Direction)
    {
        this.Member = Member;
        this.Direction = Direction;
    }

    public required string Member { get; init; }

    public required string Direction { get; init; }
}
