using Microsoft.Extensions.Options;

namespace Tradebook.Infrastructure.Outbox;

[OptionsValidator]
public sealed partial class OutboxOptionsValidator : IValidateOptions<OutboxOptions> { }
