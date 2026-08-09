using Riok.Mapperly.Abstractions;
using Tradebook.Core.DTOs;

namespace Tradebook.Infrastructure.Data;

[Mapper(AutoUserMappings = false, RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class DeliveryMapper
{
    [MapProperty(
        nameof(DeliveryRow.CreatedAt),
        nameof(PhysicalDeliveryDetailsDto.CreatedAt),
        Use = nameof(ToDateTimeOffset)
    )]
    [MapProperty(
        nameof(DeliveryRow.UpdatedAt),
        nameof(PhysicalDeliveryDetailsDto.UpdatedAt),
        Use = nameof(ToDateTimeOffset)
    )]
    internal static partial PhysicalDeliveryDetailsDto ToDto(DeliveryRow row);

    private static DateTimeOffset ToDateTimeOffset(DateTime value) => new(value);
}
