extern alias apphost;

using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Tradebook.IntegrationTests;

public sealed class AppHostSmokeTests
{
    [Fact]
    [Trait("Category", "Aspire")]
    public async Task ApiIsHealthyWhenTheGraphBoots()
    {
        // Every phase shares one hard deadline: an unbounded CreateAsync/StartAsync has
        // hung this suite (and a CI runner) for hours when DCP never came up.
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var cancellationToken = deadline.Token;
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<apphost::Projects.Tradebook_AppHost>(cancellationToken)
            .ConfigureAwait(true);
#pragma warning disable MA0004 // Preserve the concrete application type across await disposal.
        await using var app = await appHost.BuildAsync(cancellationToken).ConfigureAwait(true);
#pragma warning restore MA0004
        await app.StartAsync(cancellationToken).ConfigureAwait(true);

        var notifier = app.Services.GetRequiredService<ResourceNotificationService>();
        await notifier
            .WaitForResourceHealthyAsync("api", cancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(120), cancellationToken)
            .ConfigureAwait(true);

        using var client = app.CreateHttpClient("api");
        using var response = await client
            .GetAsync(new Uri("/health/ready", UriKind.Relative), cancellationToken)
            .ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
