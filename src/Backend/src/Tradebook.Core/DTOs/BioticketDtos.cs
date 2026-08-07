using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface]
public sealed record CreateBioticketRequest(
    Guid ContractId,
    string BookType,
    DateOnly ContractMonth,
    [property: TsOptional] string? ContractInstanceId,
    [property: TsOptional] DateOnly? StartDay,
    [property: TsOptional] DateOnly? EndDay,
    [property: TsOptional] decimal? VolumeNominatedTon,
    [property: TsOptional] decimal? VolumeRealisedTon,
    [property: TsOptional] decimal? VolumeTon,
    [property: TsOptional] decimal? CostEurTon,
    [property: TsOptional] decimal? RevenueEur,
    [property: TsOptional] decimal? VatPct,
    [property: TsOptional] decimal? VatEur,
    [property: TsOptional] decimal? InvoiceAmountEur,
    [property: TsOptional] string? Status,
    [property: TsOptional] string? Comment);

[ExportTsInterface]
public sealed record UpdateBioticketRequest(
    Guid BioticketId,
    [property: TsOptional] decimal? VolumeRealisedTon,
    [property: TsOptional] decimal? VolumeTon,
    [property: TsOptional] decimal? CostEurTon,
    [property: TsOptional] decimal? RevenueEur,
    [property: TsOptional] decimal? VatPct,
    [property: TsOptional] decimal? VatEur,
    [property: TsOptional] decimal? InvoiceAmountEur,
    [property: TsOptional] string? Status,
    [property: TsOptional] string? Comment,
    long Version);

[ExportTsInterface]
public sealed record BioticketDetailsDto(
    Guid BioticketId, Guid ContractId, string ContractInstanceId, string BookType,
    DateOnly ContractMonth,
    [property: TsOptional] DateOnly? StartDay,
    [property: TsOptional] DateOnly? EndDay,
    [property: TsOptional] decimal? VolumeNominatedTon,
    [property: TsOptional] decimal? VolumeRealisedTon,
    [property: TsOptional] decimal? VolumeTon,
    [property: TsOptional] decimal? CostEurTon,
    [property: TsOptional] decimal? RevenueEur,
    [property: TsOptional] decimal? VatPct,
    [property: TsOptional] decimal? VatEur,
    [property: TsOptional] decimal? InvoiceAmountEur,
    string Status,
    [property: TsOptional] string? Comment,
    long Version, DateTime CreatedAt, DateTime UpdatedAt);

[ExportTsInterface]
public sealed record GetBioticketHistoryRequest(
    [property: TsOptional] Guid? ContractId,
    [property: TsOptional] string? BookType,
    [property: TsOptional] string? Status,
    [property: TsOptional] DateOnly? FromMonth,
    [property: TsOptional] DateOnly? ToMonth,
    int Page = 1,
    int PageSize = 50);

[ExportTsInterface]
public sealed record GetBioticketHistoryResponse(
    IReadOnlyList<BioticketDetailsDto> Items, int TotalCount, int Page, int PageSize, bool HasNextPage);

[ExportTsInterface]
public sealed record CancelBioticketRequest(Guid BioticketId, string Reason, long Version);
