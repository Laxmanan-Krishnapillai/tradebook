var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("pg").WithImage("postgres", "17").WithDataVolume();
var tradebook = postgres.AddDatabase("tradebook");

var api = builder
    .AddProject<Projects.Tradebook_Api>("api")
    .WithReference(tradebook)
    .WithEnvironment("Database__ConnectionString", tradebook.Resource.ConnectionStringExpression)
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
