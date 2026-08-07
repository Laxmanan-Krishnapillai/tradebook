using System.ComponentModel.DataAnnotations;

namespace Tradebook.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    [Range(1, 100)]
    public int BatchSize { get; set; } = 100;

    [Range(1, 300)]
    public int FallbackPollSeconds { get; set; } = 1;

    [Range(1, 300)]
    public int ErrorBackoffSeconds { get; set; } = 2;
}
