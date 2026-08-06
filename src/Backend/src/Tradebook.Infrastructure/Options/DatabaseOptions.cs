using System.ComponentModel.DataAnnotations;

namespace Tradebook.Infrastructure.Options;

public sealed class DatabaseOptions
{
    [Required]
    public string ConnectionString { get; init; } = string.Empty;
}
