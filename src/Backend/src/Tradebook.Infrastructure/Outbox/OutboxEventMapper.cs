using Riok.Mapperly.Abstractions;
using Tradebook.Core.DTOs;

namespace Tradebook.Infrastructure.Outbox;

[Mapper]
internal static partial class OutboxEventMapper
{
    [MapProperty(nameof(OutboxEventRecord.Payload), nameof(EntityChangedEventDto.PayloadJson))]
    internal static partial EntityChangedEventDto ToDto(OutboxEventRecord source);
}
