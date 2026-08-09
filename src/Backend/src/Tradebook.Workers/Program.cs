using Tradebook.ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
await builder.Build().RunAsync().ConfigureAwait(false);
