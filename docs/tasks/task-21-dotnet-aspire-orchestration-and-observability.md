# Task 21: .NET Aspire Local Orchestration, Observability & Deployment

> **GREENFIELD MODERNIZATION TASK (2026-08-07)** — Adopt .NET Aspire 13.x to orchestrate PostgreSQL 17 + `Tradebook.Api` + the Wolverine workers + the Vite/React frontend under one `aspire run`, add OpenTelemetry (traces/metrics/logs) surfaced through the Aspire dashboard, adopt `Aspire.Hosting.Testing` for full-graph integration tests, and add an Azure Container Apps deploy path plus a reproducible devcontainer. This complements the Task 07 infrastructure — Aspire generates deploy artifacts; it is not the production runtime. Record the adoption in `docs/architecture/decision-log.md`.

- **Phase**: Modernization — Platform & Developer Experience
- **Type**: Greenfield adoption (repo-wide, committed)
- **Complexity**: High
- **Prerequisites**: Task 13 (.NET 10), Task 17 (Wolverine workers); relates to Task 07 (infra)
- **Status**: Specified
- **Target Files**:
  - `src/Aspire/Tradebook.AppHost/` (new) — orchestration graph
  - `src/Aspire/Tradebook.ServiceDefaults/` (new) — OTel + service discovery + health checks
  - `src/Backend/src/Tradebook.Api/Program.cs` — call `AddServiceDefaults()` + OTel
  - `.devcontainer/devcontainer.json` (new) — pinned .NET 10 SDK + Node + tooling
  - `.config/dotnet-tools.json` — restore inside the devcontainer
  - `docker-compose.yml` — superseded for local run
  - `infra/` — reconcile the Aspire deploy path with Task 07 Terraform
  - `src/Backend/Tradebook.sln` — add the two Aspire projects

---

## 1. Context & Objective

### 1.1 Problem Statement

Local development spans several disconnected processes: `docker-compose.yml` brings up PostgreSQL 17, the developer starts `Tradebook.Api` (FastEndpoints + SignalR) by hand, the Wolverine workers (Task 17) separately again, and the React 19 + Vite frontend (`src/Frontend`, served in production by the API via `MapFallbackToFile`) with its own dev server. Nothing ties these processes together, there is no single observable surface, and connection strings are wired manually. No reproducible one-command boot exists for humans or coding agents, and no shared telemetry lets a single trace follow a request across the API, a Wolverine handler, and a SignalR push.

### 1.2 Required Outcomes

- Add `Tradebook.AppHost` so a single `aspire run` boots PostgreSQL 17, `Tradebook.Api`, the Wolverine worker(s), and the Vite SPA with service discovery and injected connection strings.
- Add `Tradebook.ServiceDefaults` so the API and workers emit OpenTelemetry traces, metrics, and logs with correlation, viewable in the Aspire dashboard.
- Adopt `Aspire.Hosting.Testing` for full-graph integration tests while keeping the Testcontainers + Respawn suites for isolated per-test databases.
- Add an `aspire deploy` / `azd up` path that emits Azure Container Apps + PostgreSQL Flexible Server 17 artifacts reconciled with `infra/`.
- Add `.devcontainer/devcontainer.json` giving humans and agents a reproducible environment pinned to the .NET 10 SDK, Node, the Postgres client, and the `.config/dotnet-tools.json` tools.

### 1.3 In Scope

- The two new Aspire projects and their entries in `src/Backend/Tradebook.sln`, plus OTel wiring in `Program.cs` for the API and the Wolverine worker host.
- The Vite/npm app resource and its service-discovery reference to the API.
- The devcontainer, the supersession of `docker-compose.yml` for local app orchestration, and the Aspire-generated ACA deploy artifacts reconciled with Task 07.

### 1.4 Out of Scope

- Rewriting the Task 07 Terraform modules — Task 07 owns infrastructure; this task only adds the Aspire deploy path that targets it.
- Changing business logic, endpoints, or the Wolverine message contracts; replacing the Testcontainers + Respawn database tests; or making Aspire the production runtime.

## 2. Current State

- **Backend**: .NET 10, `src/Backend/Tradebook.sln` — `Tradebook.Api` (FastEndpoints + SignalR), `Tradebook.Infrastructure` (Dapper/Npgsql), and the Wolverine workers from Task 17.
- **Database**: PostgreSQL 17, today provisioned locally through `docker-compose.yml`.
- **Frontend**: `src/Frontend`, React 19 + Vite; the built SPA is served by the API via `MapFallbackToFile`.
- **Tests**: integration tests use Testcontainers (PG17) + Respawn for isolation.
- **Deploy**: Azure Container Apps + PostgreSQL Flexible Server 17, provisioned by `infra/` Terraform (Task 07).
- **Tooling**: Stryker and the remaining CLI tools are declared in `.config/dotnet-tools.json`.

## 3. Target Design

Add an Aspire orchestration graph in `src/Aspire/Tradebook.AppHost` and shared cross-cutting defaults in `src/Aspire/Tradebook.ServiceDefaults`; reference both from `src/Backend/Tradebook.sln`.

### 3.1 AppHost orchestration graph

```csharp
// src/Aspire/Tradebook.AppHost/AppHost.cs
var builder = DistributedApplication.CreateBuilder(args);
// PostgreSQL 17 — matches prod Flexible Server 17; volume persists data across runs.
var postgres = builder.AddPostgres("pg").WithImage("postgres", "17").WithDataVolume();
var tradebookDb = postgres.AddDatabase("tradebook");
// FastEndpoints + SignalR API — connection string injected, never hardcoded.
var api = builder.AddProject<Projects.Tradebook_Api>("api")
    .WithReference(tradebookDb).WaitFor(tradebookDb);
// Wolverine background workers (Task 17) — same DB reference via service discovery.
builder.AddProject<Projects.Tradebook_Workers>("workers")
    .WithReference(tradebookDb).WaitFor(tradebookDb);
// React 19 + Vite SPA — CommunityToolkit AddViteApp is Vite-aware; the built-in
// AddNpmApp("frontend", "../../Frontend", "dev") is the framework-native equivalent.
builder.AddViteApp("frontend", "../../Frontend").WithReference(api).WaitFor(api);
builder.Build().Run();
```

### 3.2 ServiceDefaults & OpenTelemetry

```csharp
// src/Aspire/Tradebook.ServiceDefaults/Extensions.cs
public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
    where TBuilder : IHostApplicationBuilder
{
    builder.ConfigureOpenTelemetry();
    builder.AddDefaultHealthChecks();
    builder.Services.AddServiceDiscovery();
    return builder;
}
public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
    where TBuilder : IHostApplicationBuilder
{
    builder.Logging.AddOpenTelemetry(o => { o.IncludeFormattedMessage = true; o.IncludeScopes = true; });
    builder.Services.AddOpenTelemetry()
        .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation())
        .WithTracing(t => t
            .AddSource("Wolverine")              // Task 17 handler spans
            .AddAspNetCoreInstrumentation()      // API + SignalR spans
            .AddHttpClientInstrumentation()
            .AddNpgsql());                        // Dapper/Npgsql DB spans
    builder.AddOpenTelemetryExporters();          // OTLP -> Aspire dashboard
    return builder;
}
```

Call `builder.AddServiceDefaults()` from `src/Backend/src/Tradebook.Api/Program.cs` (before building the app) and from the Wolverine worker host. The shared `AddSource("Wolverine")` plus ASP.NET Core instrumentation makes one trace span an HTTP request → Wolverine handler → SignalR push.

### 3.3 Full-graph integration testing

```csharp
// src/Backend/tests/Tradebook.IntegrationTests/AppHostSmokeTests.cs
[Fact, Trait("Category", "Aspire")]
public async Task Api_is_healthy_when_the_graph_boots()
{
    var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Tradebook_AppHost>();
    await using var app = await appHost.BuildAsync();
    var notifier = app.Services.GetRequiredService<ResourceNotificationService>();
    await app.StartAsync();
    var client = app.CreateHttpClient("api");
    await notifier.WaitForResourceHealthyAsync("api").WaitAsync(TimeSpan.FromSeconds(120));
    var response = await client.GetAsync("/health");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

Keep the Testcontainers + Respawn suites unchanged for isolated per-test databases; the two approaches are complementary.

### 3.4 CLI, dashboard & deploy

```bash
# one command boots Postgres 17 + API + Wolverine workers + Vite SPA + dashboard
aspire run

# emit Azure Container Apps artifacts (built SPA behind YARP, secrets via Key Vault)
aspire publish -o ./artifacts/aspire

# provision/update through the generated artifacts (reconciled with infra/ Terraform)
aspire deploy        # or: azd up
```

### 3.5 Version matrix

| Component | Version | Notes |
|-----------|---------|-------|
| .NET SDK | 10.0 (LTS) | Task 13 baseline |
| .NET Aspire | 13.x | Latest supported channel (short support window) |
| Aspire.Hosting.PostgreSQL | 13.x | Postgres 17 container resource |
| Aspire.Hosting.NodeJs | 13.x | `AddNpmApp` / CommunityToolkit `AddViteApp` |
| Aspire.Hosting.Testing | 13.x | `DistributedApplicationTestingBuilder` |
| PostgreSQL / Node.js | 17 / 22 LTS | matches prod Flexible Server 17; Vite 7 build |

### 3.6 References

- Aspire overview — https://learn.microsoft.com/dotnet/aspire/get-started/aspire-overview
- Service defaults — https://learn.microsoft.com/dotnet/aspire/fundamentals/service-defaults
- Dashboard — https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/overview
- Node.js / Vite apps — https://learn.microsoft.com/dotnet/aspire/get-started/build-aspire-apps-with-nodejs
- Testing & deployment — https://learn.microsoft.com/dotnet/aspire/testing/overview

## 4. Implementation Plan

1. Add `src/Aspire/Tradebook.AppHost` and `src/Aspire/Tradebook.ServiceDefaults`; register both in `src/Backend/Tradebook.sln`.
2. Reference `Tradebook.ServiceDefaults` from `Tradebook.Api` and the Wolverine worker host; call `AddServiceDefaults()` in each `Program.cs`, routing Wolverine and Npgsql spans into the shared OTel pipeline.
3. Model the graph in `AppHost.cs`: Postgres 17 with a data volume, the API, the workers, and the Vite SPA, all wired with `WithReference` and `WaitFor`.
4. Add the `Aspire.Hosting.Testing` smoke test to `Tradebook.IntegrationTests` under the `Aspire` trait; keep the Testcontainers + Respawn suites intact.
5. Add `.devcontainer/devcontainer.json` pinning the .NET 10 SDK, Node, the Postgres client, and `dotnet tool restore` for `.config/dotnet-tools.json` (dotnet-stryker, dotnet-csharpier, sqlc, and the rest).
6. Wire `aspire publish`/`aspire deploy` to emit ACA + Postgres Flexible Server 17 artifacts, reconcile them with `infra/`, source secrets from Key Vault, and mark `docker-compose.yml` superseded for local app orchestration.

## 5. Validation & Acceptance

### 5.1 Commands

```bash
# restore the Aspire CLI/workload + repo-local tools (.config/dotnet-tools.json)
dotnet workload update && dotnet tool restore
# boot the full graph; the dashboard URL prints on stdout
aspire run
# full-graph integration test (Aspire.Hosting.Testing)
dotnet test src/Backend/tests/Tradebook.IntegrationTests --filter Category=Aspire
# isolated per-test DB suite still runs on Testcontainers + Respawn
dotnet test src/Backend/tests/Tradebook.IntegrationTests --filter Category=Database
# devcontainer build check + deploy-artifact generation (ACA)
devcontainer build --workspace-folder .
aspire publish -o ./artifacts/aspire
```

### 5.2 Acceptance criteria

| ID | Acceptance criterion | Verification |
|----|----------------------|--------------|
| ASPIRE-01 | `aspire run` boots Postgres 17, `Tradebook.Api`, the Wolverine workers, and the Vite SPA, all Healthy | Dashboard + `WaitForResourceHealthyAsync` |
| ASPIRE-02 | One dashboard trace spans HTTP request → Wolverine handler → SignalR push | Dashboard traces view |
| ASPIRE-03 | No connection string is hardcoded; API + workers resolve Postgres via references/service discovery | Code review + config scan |
| ASPIRE-04 | A `DistributedApplicationTestingBuilder` integration test passes in CI | `dotnet test --filter Category=Aspire` |
| ASPIRE-05 | Testcontainers + Respawn isolated DB tests stay green, unchanged | `dotnet test --filter Category=Database` |
| ASPIRE-06 | `.devcontainer/devcontainer.json` builds and restores `.config/dotnet-tools.json` | `devcontainer build` |
| ASPIRE-07 | `aspire publish`/`aspire deploy` emits valid ACA + Postgres Flexible Server 17 artifacts reconciled with `infra/` | Artifact diff review |
| ASPIRE-08 | No secrets committed; deploy reads secrets from Key Vault only | Secret scan + review |

## 6. Guardrails

1. Treat Aspire as dev-orchestration and a deploy-artifact generator, not the production runtime — ACA + PostgreSQL Flexible Server 17 provisioned by `infra/` Terraform (Task 07) stay the production source of truth.
2. Never hardcode connection strings or endpoints; resolve every dependency through Aspire references (`WithReference`) and service discovery.
3. Keep Testcontainers + Respawn for isolated per-test databases — `Aspire.Hosting.Testing` complements them and must not replace them.
4. Stay on the latest supported Aspire release; Aspire ships on short support windows, so pin exact versions and track the release channel.
5. Never commit secrets — use user-secrets/Aspire parameters locally and Azure Key Vault for deploy.
6. Do not rewrite the Task 07 Terraform modules; the Aspire deploy path must reconcile with `infra/`, which retains infrastructure ownership.
7. `docker-compose.yml` is superseded for local app orchestration; keep compose only for containers Aspire does not model.
