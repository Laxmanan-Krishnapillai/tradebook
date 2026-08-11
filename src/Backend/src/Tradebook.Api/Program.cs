using System.Text.Json.Serialization;
using FastEndpoints;
using JasperFx;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Tradebook_Core;
using Tradebook.Api;
using Tradebook.Api.AgentTools;
using Tradebook.Api.ErrorHandling;
using Tradebook.Api.Features.Analytics;
using Tradebook.Api.Features.Health;
using Tradebook.Api.Messaging;
using Tradebook.Api.Options;
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
builder.Services.AddTradebookNetworking();
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
builder.Services.AddScoped<AnalyticsQueryRunner>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IValidateOptions<InAppAgentOptions>, InAppAgentOptionsValidator>();
builder
    .Services.AddOptions<InAppAgentOptions>()
    .BindConfiguration(InAppAgentOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddAGUIServer();
builder.Services.AddSingleton<AnalyticsAgentTool>();
builder.Services.AddSingleton<AIAgent>(TradebookInAppAgent.Create);
builder
    .Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<AnalyticsMcpTools>();
builder.Services.AddDashboardPush();
builder.Services.AddExceptionHandler<PostgresExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHostedService<Tradebook.Infrastructure.Migrations.MigrationHostedService>();

var hostedServicesBeforeWolverine = builder
    .Services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
    .ToList();
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

// No AddResourceSetupOnStartup(), and Wolverine's own hosted services start on a
// background retry loop: an unreachable database at boot must defer messaging, not
// abort the host — liveness stays 200 while readiness reports 503 (task-02 contract).
// AutoBuildMessageStorageOnStartup provisions the envelope schema once the database
// is reachable.
builder.Services.MakeLateHostedServicesResilient(hostedServicesBeforeWolverine);
builder.Services.AddScoped<ITransactionalEventPublisher, WolverineTransactionalEventPublisher>();
builder.Services.AddScoped<IRealtimeEventReader, PostgresRealtimeEventReader>();

var app = builder.Build();

// Schema validation runs inside MigrationHostedService after the background migration
// pass; semantic-model drift stops the application there instead of racing startup.

app.UseForwardedHeaders();
app.UseExceptionHandler();

// Static assets must run before the authorization fallback policy so the SPA shell can
// load MSAL and acquire a token. API and real-time endpoints remain policy guarded.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints(config =>
    config.Serializer.Options.TypeInfoResolver = AppJsonSerializerContext.Default
);
app.MapOpenApi().RequireAuthorization();
app.MapTradebookHealthEndpoints();
app.MapDashboardPushHub();
app.MapMcp("/mcp").RequireAuthorization("ReadPolicy");
if (app.Services.GetRequiredService<IOptions<InAppAgentOptions>>().Value.Enabled)
{
    app.MapAGUIServer("/api/v1/agent/run", app.Services.GetRequiredService<AIAgent>())
        .RequireAuthorization("ReadPolicy");
}

// SPA hosting (Task 02 §3.7): serve the built frontend; unmatched /api/* and /hubs/*
// return 404 instead of index.html.
// AllowAnonymous: the SPA shell must load on deep-link refresh (F5 on /dashboards/x)
// so MSAL can run; without it the authorization FallbackPolicy returns 401 for the
// HTML document itself. Data still comes from the policy-guarded /api endpoints.
app.MapFallbackToFile("{*path:regex(^(?!api|hubs)(.*)$)}", "index.html").AllowAnonymous();

await app.RunAsync().ConfigureAwait(false);

public partial class Program
{
    protected Program() { }
}
