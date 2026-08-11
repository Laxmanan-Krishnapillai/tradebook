using System.Globalization;
using Dapper;
using Tradebook.Core.Analytics;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Api.Features.Analytics;

/// <summary>
/// Executes the canonical, identifier-whitelisted analytics query path for every transport.
/// </summary>
public sealed class AnalyticsQueryRunner(
    SemanticQueryCompiler compiler,
    INpgsqlConnectionFactory connections
)
{
    public async Task<AnalyticsQueryResponse> QueryAsync(
        JsonQueryAst request,
        CancellationToken cancellationToken
    )
    {
        var query = compiler.Compile(request);
        var connection = await connections
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var configuredConnection = connection.ConfigureAwait(false);
        var result = await connection
            .QueryAsync(
                new CommandDefinition(
                    query.SqlText,
                    query.Parameters,
                    cancellationToken: cancellationToken
                )
            )
            .ConfigureAwait(false);
        var rows = result
            .Select(row =>
            {
                var values = (IDictionary<string, object?>)row;
                return (IReadOnlyList<object?>)
                    query
                        .ResultColumnNames.Select(column =>
                            values.TryGetValue(column, out var value)
                                ? NormalizeWireValue(value)
                                : null
                        )
                        .ToArray();
            })
            .ToArray();

        return new AnalyticsQueryResponse(query.ResultColumnNames, rows);
    }

    internal static object? NormalizeWireValue(object? value) =>
        value is decimal number ? number.ToString(CultureInfo.InvariantCulture) : value;
}
