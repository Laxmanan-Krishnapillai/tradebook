namespace Tradebook.IntegrationTests.Fixtures;

public abstract class DatabaseTestBase(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>,
        IAsyncLifetime
{
    protected CustomWebApplicationFactory Factory { get; } = factory;
    protected HttpClient Client { get; } = factory.CreateClient();

    public ValueTask InitializeAsync() => new(Factory.ResetDatabaseAsync());

    public virtual ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
