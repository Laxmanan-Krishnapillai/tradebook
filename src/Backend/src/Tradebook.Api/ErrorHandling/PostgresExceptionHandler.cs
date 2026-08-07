using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Npgsql;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.ErrorHandling;

internal sealed class PostgresExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not PostgresException postgresException)
        {
            return false;
        }

        var statusCode = StatusCodeFor(postgresException.SqlState);
        if (statusCode is null)
        {
            return false;
        }

        var response = statusCode == StatusCodes.Status409Conflict
            ? new ProblemDetailsResponse(
                "about:blank",
                "Conflict",
                statusCode.Value,
                "The request conflicts with an existing resource.")
            : new ProblemDetailsResponse(
                "about:blank",
                "Bad Request",
                statusCode.Value,
                "The request violates a data constraint.");

        httpContext.Response.StatusCode = statusCode.Value;
        httpContext.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            response,
            AppJsonSerializerContext.Default.ProblemDetailsResponse,
            cancellationToken);
        return true;
    }

    private static int? StatusCodeFor(string sqlState)
    {
        if (sqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ExclusionViolation)
        {
            return StatusCodes.Status409Conflict;
        }

        return sqlState.StartsWith("22", StringComparison.Ordinal) ||
               sqlState.StartsWith("23", StringComparison.Ordinal)
            ? StatusCodes.Status400BadRequest
            : null;
    }
}
