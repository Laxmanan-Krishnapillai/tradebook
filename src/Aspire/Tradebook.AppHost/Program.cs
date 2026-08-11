var builder = DistributedApplication.CreateBuilder(args);
const string entraTenantId = "c3e2af84-c62c-4bf5-90ba-8a1ea1c3ab06";
const string entraClientId = "16b47a3c-5cc7-45ce-98c4-f2cc66062d9f";

var postgresPassword = builder.AddParameter(
    "postgres-password",
    "tradebook-local",
    publishValueAsDefault: false,
    secret: true
);
var postgres = builder
    .AddPostgres("pg", password: postgresPassword)
    .WithImage("postgres", "17")
    .WithDataVolume("tradebook-apphost-pgdata-v2");
var tradebook = postgres.AddDatabase("tradebook");

var api = builder
    .AddProject<Projects.Tradebook_Api>("api")
    .WithReference(tradebook)
    .WithEnvironment("Database__ConnectionString", tradebook.Resource.ConnectionStringExpression)
    .WithEnvironment("Entra__TenantId", entraTenantId)
    .WithEnvironment("Entra__ClientId", entraClientId)
    .WithHttpHealthCheck("/health/ready")
    .WaitFor(tradebook);

builder
    .AddProject<Projects.Tradebook_Workers>("workers")
    .WithReference(tradebook)
    .WaitFor(tradebook);

builder
    .AddViteApp("frontend", "../../Frontend")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync().ConfigureAwait(false);
