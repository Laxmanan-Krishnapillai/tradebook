using Microsoft.Extensions.Options;

namespace Tradebook.Infrastructure.Options;

[OptionsValidator]
public sealed partial class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions> { }
