using System.Text.Json.Serialization;
using FastEndpoints;
using JasperFx;
using JasperFx.Resources;
using Npgsql;
using Tradebook.Api;
using Tradebook.Api.Messaging;
using Tradebook.Api.Security;
using Tradebook.Core.Interfaces;
using Tradebook.Core.Messaging;
using Tradebook.Infrastructure.Caching;
using Tradebook.Infrastructure.Data;
using Tradebook.Infrastructure.DependencyInjection;
using Tradebook.Infrastructure.Options;
using Tradebook.Api.RealTime;
using Tradebook.Core.Analytics;
using Tradebook.Api.Features.Health;
using Tradebook.Api.ErrorHandling;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;

var builder = WebApplication.CreateBuilder(args);
builder.Host.ApplyJasperFxExtensions();
var connectionString = builder.Configuration["Database:ConnectionString"]
    ?? throw new InvalidOperationException("Database:ConnectionString is required.");
builder.Host.UseWolverine(options =>
{
    options.PersistMessagesWithPostgresql(connectionString, "wolverine");
    options.Policies.UseDurableLocalQueues();
    options.LocalQueueFor<EntityChangedDomainEvent>().Sequential();
    options.Policies.AutoApplyTransactions();
    options.Policies.OnException<NpgsqlException>().OrInner<NpgsqlException>()
        .RetryWithCooldown(
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(250));
});
builder.Services.AddResourceSetupOnStartup();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolver = AppJsonSerializerContext.Default);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOptions<DatabaseOptions>().BindConfiguration("Database").ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddTradebookPersistence();
builder.Services.AddHybridCache();
builder.Services.AddSingleton<ICacheService, HybridCacheService>();
builder.Services.AddScoped<ITransactionalEventPublisher, WolverineTransactionalEventPublisher>();
var semanticModels = new SemanticModelLoader();
builder.Services.AddSingleton(semanticModels);
builder.Services.AddSingleton<SemanticQueryCompiler>();
builder.Services.AddTradebookAuthentication(builder.Configuration);
builder.Services.AddTradebookHealthChecks();
builder.Services.AddFastEndpoints();
builder.Services.AddDashboardPush();
builder.Services.AddExceptionHandler<PostgresExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

try
{
    await using var connection = await app.Services
        .GetRequiredService<INpgsqlConnectionFactory>()
        .OpenConnectionAsync(CancellationToken.None);
    await semanticModels.ValidateDatabaseSchemaAsync(connection);
}
catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
{
    // Keep liveness independent from PostgreSQL availability. Readiness repeats the
    // validation and stays unhealthy until the database can be reached, while a
    // reachable database with semantic-model drift still fails startup above.
    app.Logger.LogWarning(
        exception,
        "Semantic schema startup validation was deferred because PostgreSQL is unavailable.");
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints(config => config.Serializer.Options.TypeInfoResolver = AppJsonSerializerContext.Default);
app.MapTradebookHealthEndpoints();
app.MapDashboardPushHub();

// SPA hosting (Task 02 §3.7): serve the built frontend; unmatched /api/* and /hubs/*
// return 404 instead of index.html.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("{*path:regex(^(?!api|hubs)(.*)$)}", "index.html");

if (args.FirstOrDefault() is { } firstArgument &&
    !firstArgument.StartsWith("--", StringComparison.Ordinal))
{
    return await app.RunJasperFxCommands(args);
}

await app.RunAsync();
return 0;

public partial class Program;
