using Tradebook.Core.Domain.ValueObjects.Ids;
using Tradebook.Core.Domain.ValueObjects.Money;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateBioticketRequest(
    ContractId ContractId,
    string BookType,
    DateOnly ContractMonth,
    [property: TsOptional] string? ContractInstanceId,
    [property: TsOptional] DateOnly? StartDay,
    [property: TsOptional] DateOnly? EndDay,
    [property: TsOptional] Quantity? VolumeNominatedTon,
    [property: TsOptional] Quantity? VolumeRealisedTon,
    [property: TsOptional] Quantity? VolumeTon,
    [property: TsOptional] Amount? CostEurTon,
    [property: TsOptional] Amount? RevenueEur,
    [property: TsOptional] Amount? VatPct,
    [property: TsOptional] Amount? VatEur,
    [property: TsOptional] Amount? InvoiceAmountEur,
    [property: TsOptional] string? Status,
    [property: TsOptional] string? Comment
);

[ExportTsInterface]
public sealed record UpdateBioticketRequest(
    BioticketDeliveryId BioticketId,
    [property: TsOptional] Quantity? VolumeRealisedTon,
    [property: TsOptional] Quantity? VolumeTon,
    [property: TsOptional] Amount? CostEurTon,
    [property: TsOptional] Amount? RevenueEur,
    [property: TsOptional] Amount? VatPct,
    [property: TsOptional] Amount? VatEur,
    [property: TsOptional] Amount? InvoiceAmountEur,
    [property: TsOptional] string? Status,
    [property: TsOptional] string? Comment,
    long Version
);

[ExportTsInterface]
public sealed record BioticketDetailsDto(
    BioticketDeliveryId BioticketId,
    ContractId ContractId,
    string ContractInstanceId,
    string BookType,
    DateOnly ContractMonth,
    [property: TsOptional] DateOnly? StartDay,
    [property: TsOptional] DateOnly? EndDay,
    [property: TsOptional] Quantity? VolumeNominatedTon,
    [property: TsOptional] Quantity? VolumeRealisedTon,
    [property: TsOptional] Quantity? VolumeTon,
    [property: TsOptional] Amount? CostEurTon,
    [property: TsOptional] Amount? RevenueEur,
    [property: TsOptional] Amount? VatPct,
    [property: TsOptional] Amount? VatEur,
    [property: TsOptional] Amount? InvoiceAmountEur,
    string Status,
    [property: TsOptional] string? Comment,
    long Version,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

[ExportTsInterface]
public sealed record GetBioticketHistoryRequest(
    [property: TsOptional] ContractId? ContractId,
    [property: TsOptional] string? BookType,
    [property: TsOptional] string? Status,
    [property: TsOptional] DateOnly? FromMonth,
    [property: TsOptional] DateOnly? ToMonth,
    int Page = 1,
    int PageSize = 50
);

[ExportTsInterface]
public sealed record GetBioticketHistoryResponse(
    IReadOnlyList<BioticketDetailsDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    bool HasNextPage
);

[ExportTsInterface]
public sealed record CancelBioticketRequest(
    BioticketDeliveryId BioticketId,
    string Reason,
    long Version
);
