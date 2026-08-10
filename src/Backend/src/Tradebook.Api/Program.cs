using System.Text.Json.Serialization;
using FastEndpoints;
using JasperFx;
using JasperFx.Resources;
using Microsoft.Extensions.Options;
using Npgsql;
using Tradebook_Core;
using Tradebook.Api;
using Tradebook.Api.ErrorHandling;
using Tradebook.Api.Features.Health;
using Tradebook.Api.Messaging;
using Tradebook.Api.RealTime;
using Tradebook.Api.Security;
using Tradebook.Api.Serialization;
using Tradebook.Core.Analytics;
using Tradebook.Core.Interfaces;
using Tradebook.Core.Messaging;
using Tradebook.Infrastructure.Caching;
using Tradebook.Infrastructure.Data;
using Tradebook.Infrastructure.DependencyInjection;
using Tradebook.Infrastructure.Options;
using Tradebook.Infrastructure.RealTime;
using Tradebook.ServiceDefaults;
using Wolverine;
using Wolverine.Configuration;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
VogenTypeHandlers.RegisterAll();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new VogenTypesFactory());
    options.SerializerOptions.Converters.Add(new MoneyJsonConverter());
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
builder.Services.AddOpenApi();
builder.Services.AddDashboardPush();
builder.Services.AddExceptionHandler<PostgresExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHostedService<Tradebook.Infrastructure.Migrations.MigrationHostedService>();

builder.Host.UseWolverine(options =>
{
    // Read inside the callback (services phase of Build), not at top-level: the
    // ConfigurationManager is live, and test-harness overrides are merged into it
    // after these top-level statements have already run.
    var wolverineConnectionString =
        builder.Configuration["Database:ConnectionString"]
        ?? throw new InvalidOperationException("Database:ConnectionString is required.");
    options.PersistMessagesWithPostgresql(wolverineConnectionString, "wolverine");
    options.AutoBuildMessageStorageOnStartup = AutoCreate.CreateOrUpdate;
    options.Policies.UseDurableLocalQueues();
    options.LocalQueueFor<EntityChangedDomainEvent>().Sequential();
    options.Policies.AutoApplyTransactions();
    options
        .Policies.OnException<NpgsqlException>()
        .OrInner<NpgsqlException>()
        .RetryWithCooldown(
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(250)
        );
});
builder.Services.AddScoped<ITransactionalEventPublisher, WolverineTransactionalEventPublisher>();
builder.Services.AddScoped<IRealtimeEventReader, PostgresRealtimeEventReader>();

builder.Services.AddResourceSetupOnStartup();

var app = builder.Build();

// Schema validation runs inside MigrationHostedService after the background migration
// pass; semantic-model drift stops the application there instead of racing startup.

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints(config =>
    config.Serializer.Options.TypeInfoResolver = AppJsonSerializerContext.Default
);
app.MapOpenApi().RequireAuthorization();
app.MapTradebookHealthEndpoints();
app.MapDashboardPushHub();

// SPA hosting (Task 02 §3.7): serve the built frontend; unmatched /api/* and /hubs/*
// return 404 instead of index.html.
app.UseDefaultFiles();
app.UseStaticFiles();

// AllowAnonymous: the SPA shell must load on deep-link refresh (F5 on /dashboards/x)
// so MSAL can run; without it the authorization FallbackPolicy returns 401 for the
// HTML document itself. Data still comes from the policy-guarded /api endpoints.
app.MapFallbackToFile("{*path:regex(^(?!api|hubs)(.*)$)}", "index.html").AllowAnonymous();

await app.RunAsync().ConfigureAwait(false);

public partial class Program
{
    protected Program() { }
}
