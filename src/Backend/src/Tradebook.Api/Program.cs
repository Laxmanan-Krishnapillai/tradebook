using System.Text.Json.Serialization;
using FastEndpoints;
using Microsoft.Extensions.Options;
using Npgsql;
using Tradebook_Core;
using Tradebook.Api;
using Tradebook.Api.ErrorHandling;
using Tradebook.Api.Features.Health;
using Tradebook.Api.RealTime;
using Tradebook.Api.Security;
using Tradebook.Core.Analytics;
using Tradebook.Core.Interfaces;
using Tradebook.Infrastructure.Caching;
using Tradebook.Infrastructure.Data;
using Tradebook.Infrastructure.DependencyInjection;
using Tradebook.Infrastructure.Options;

var builder = WebApplication.CreateBuilder(args);
VogenTypeHandlers.RegisterAll();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new VogenTypesFactory());
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter())
);
builder.Services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();
builder.Services.AddOptions<DatabaseOptions>().BindConfiguration("Database").ValidateOnStart();
builder.Services.AddTradebookPersistence();
builder.Services.AddHybridCache();
builder.Services.AddSingleton<ICacheService, HybridCacheService>();
var semanticModels = new SemanticModelLoader();
builder.Services.AddSingleton(semanticModels);
builder.Services.AddSingleton<SemanticQueryCompiler>();
builder.Services.AddTradebookAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddTradebookHealthChecks();
builder.Services.AddFastEndpoints();
builder.Services.AddDashboardPush();
builder.Services.AddExceptionHandler<PostgresExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

try
{
    var connection = await app
        .Services.GetRequiredService<INpgsqlConnectionFactory>()
        .OpenConnectionAsync(CancellationToken.None)
        .ConfigureAwait(false);
    await using var _ = connection.ConfigureAwait(false);
    await semanticModels.ValidateDatabaseSchemaAsync(connection).ConfigureAwait(false);
}
catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
{
    // Keep liveness independent from PostgreSQL availability. Readiness repeats the
    // validation and stays unhealthy until the database can be reached, while a
    // reachable database with semantic-model drift still fails startup above.
    ProgramLog.SchemaValidationDeferred(app.Logger, exception);
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints(config =>
    config.Serializer.Options.TypeInfoResolver = AppJsonSerializerContext.Default
);
app.MapTradebookHealthEndpoints();
app.MapDashboardPushHub();

// SPA hosting (Task 02 §3.7): serve the built frontend; unmatched /api/* and /hubs/*
// return 404 instead of index.html.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("{*path:regex(^(?!api|hubs)(.*)$)}", "index.html");

await app.RunAsync().ConfigureAwait(false);

public partial class Program
{
    protected Program() { }
}
