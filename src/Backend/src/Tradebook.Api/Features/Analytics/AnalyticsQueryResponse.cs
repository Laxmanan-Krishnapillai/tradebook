using System.Diagnostics.CodeAnalysis;
using Dapper;
using FastEndpoints;
using Tradebook.Core.Analytics;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Api.Features.Analytics;

public sealed record AnalyticsQueryResponse
{
    public AnalyticsQueryResponse() { }

    [SetsRequiredMembers]
    public AnalyticsQueryResponse(
        IReadOnlyList<string> Columns,
        IReadOnlyList<IReadOnlyList<object?>> Rows
    )
    {
        this.Columns = Columns;
        this.Rows = Rows;
    }

    public required IReadOnlyList<string> Columns { get; init; }

    public required IReadOnlyList<IReadOnlyList<object?>> Rows { get; init; }
}
