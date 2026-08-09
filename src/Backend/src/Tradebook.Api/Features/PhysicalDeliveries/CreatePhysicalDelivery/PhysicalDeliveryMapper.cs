using Riok.Mapperly.Abstractions;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.PhysicalDeliveries.CreatePhysicalDelivery;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class PhysicalDeliveryMapper
{
    public static partial CreatePhysicalDeliveryResponse ToResponse(
        PhysicalDeliveryDetailsDto delivery
    );
}
