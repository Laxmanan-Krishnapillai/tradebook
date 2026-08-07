using Dapper;
using FastEndpoints;
using Tradebook.Core.Analytics;
using Tradebook.Infrastructure.Data;

namespace Tradebook.Api.Features.Analytics;

public sealed record AnalyticsQueryResponse(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows);

public sealed class AnalyticsQueryEndpoint(SemanticQueryCompiler compiler, INpgsqlConnectionFactory connections) : Endpoint<JsonQueryAst, AnalyticsQueryResponse>
{
    public override void Configure()
    {
        Post("/api/v1/analytics/query");
        Policies("ReadPolicy");
    }

    public override async Task HandleAsync(JsonQueryAst request, CancellationToken cancellationToken)
    {
        CompiledSqlQuery query;
        try { query = compiler.Compile(request); }
        catch (SemanticValidationException exception)
        {
            AddError(exception.Message);
            await SendErrorsAsync(400, cancellationToken);
            return;
        }

        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        var result = await connection.QueryAsync(new CommandDefinition(
            query.SqlText,
            query.Parameters,
            cancellationToken: cancellationToken));
        var rows = result.Select(row =>
        {
            var values = (IDictionary<string, object?>)row;
            return (IReadOnlyList<object?>)query.ResultColumnNames.Select(column => values.TryGetValue(column, out var value) ? value : null).ToArray();
        }).ToArray();
        await SendAsync(new AnalyticsQueryResponse(query.ResultColumnNames, rows), 200, cancellationToken);
    }
}
