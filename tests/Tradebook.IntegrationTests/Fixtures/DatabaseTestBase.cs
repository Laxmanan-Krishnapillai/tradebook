namespace Tradebook.IntegrationTests.Fixtures;

public abstract class DatabaseTestBase(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    protected CustomWebApplicationFactory Factory { get; } = factory;
    protected HttpClient Client { get; } = factory.CreateClient();

    public Task InitializeAsync() => Factory.ResetDatabaseAsync();

    public virtual Task DisposeAsync() => Task.CompletedTask;
}
