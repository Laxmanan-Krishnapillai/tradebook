namespace Tradebook.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public int BatchSize { get; set; } = 100;
    public int FallbackPollSeconds { get; set; } = 1;
    public int ErrorBackoffSeconds { get; set; } = 2;
}
