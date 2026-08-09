using System.Diagnostics.CodeAnalysis;
using Dapper;
using FastEndpoints;
using Tradebook.Core.Analytics;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Api.Features.Analytics;

public sealed class AnalyticsQueryEndpoint(
    SemanticQueryCompiler compiler,
    INpgsqlConnectionFactory connections
) : Endpoint<JsonQueryAst, AnalyticsQueryResponse>
{
    public override void Configure()
    {
        Post("/api/v1/analytics/query");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(JsonQueryAst req, CancellationToken ct)
    {
        CompiledSqlQuery query;
        try
        {
            query = compiler.Compile(req);
        }
        catch (SemanticValidationException exception)
        {
            AddError(exception.Message);
            await (Send.ErrorsAsync(400, cancellation: ct)).ConfigureAwait(false);
            return;
        }

        var connection = await (connections.OpenConnectionAsync(ct)).ConfigureAwait(false);
        await using var configuredConnection = connection.ConfigureAwait(false);
        var result = await (
            connection.QueryAsync(
                new CommandDefinition(query.SqlText, query.Parameters, cancellationToken: ct)
            )
        ).ConfigureAwait(false);
        var rows = result
            .Select(row =>
            {
                var values = (IDictionary<string, object?>)row;
                return (IReadOnlyList<object?>)
                    query
                        .ResultColumnNames.Select(column =>
                            values.TryGetValue(column, out var value) ? value : null
                        )
                        .ToArray();
            })
            .ToArray();
        await (
            Send.ResponseAsync(
                new AnalyticsQueryResponse(query.ResultColumnNames, rows),
                200,
                cancellation: ct
            )
        ).ConfigureAwait(false);
    }
}
