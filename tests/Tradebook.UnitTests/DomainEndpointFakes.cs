using System.Security.Claims;
using Tradebook.Core.DTOs;
using Tradebook.Core.Interfaces;

namespace Tradebook.UnitTests;

internal static class DomainEndpointTestData
{
    private static readonly DateTime CreatedAt = new(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime UpdatedAt = new(2026, 1, 3, 11, 30, 0, DateTimeKind.Utc);

    public static ClaimsPrincipal Principal(Guid actorId) =>
        new(new ClaimsIdentity([new Claim("oid", actorId.ToString()), new("tid", "11111111-1111-1111-1111-111111111111"), new("tradebook_tenant", "11111111-1111-1111-1111-111111111111")], "test"));

    public static BioticketDetailsDto Bioticket(Guid? id = null, long version = 4, string status = "Awaiting") => new(
        id ?? Guid.NewGuid(),
        Guid.NewGuid(),
        "CRSB45.ST.2401.CO2E-1-2026",
        "Sales",
        new DateOnly(2026, 1, 1),
        new DateOnly(2026, 1, 1),
        new DateOnly(2026, 1, 31),
        12m,
        11m,
        10m,
        20m,
        200m,
        0.25m,
        50m,
        250m,
        status,
        "fixture bioticket",
        version,
        CreatedAt,
        UpdatedAt);

    public static CapacityBookingDetailsDto CapacityBooking(Guid? id = null, long version = 4) => new(
        id ?? Guid.NewGuid(),
        Guid.NewGuid(),
        "NRGD.49.GAS.THE.CBC.MON-1-2026",
        new DateOnly(2026, 1, 1),
        Guid.NewGuid(),
        "NRGD",
        "GTF/THE - Monthly",
        "GTF",
        "THE",
        "GTF-ELLUND-THE",
        "ELLUND",
        new DateOnly(2026, 1, 1),
        new DateOnly(2026, 1, 31),
        10m,
        2m,
        20m,
        "fixture booking",
        version,
        CreatedAt,
        UpdatedAt);

    public static ContractDetailsDto Contract(Guid? id = null, long version = 4, bool isActive = true) => new(
        id ?? Guid.NewGuid(),
        "ARLA45.SC.2601.ETSS",
        Guid.NewGuid(),
        "GoO",
        "Sell",
        "ARLA",
        "DK",
        45,
        1,
        2026,
        null,
        Guid.NewGuid(),
        "BGEM",
        "ETS",
        "SUB",
        "TTF",
        31.25m,
        "External",
        "fixture contract",
        isActive,
        version,
        CreatedAt,
        UpdatedAt);

    public static GooCertificateTransactionDetailsDto GooCertificate(
        Guid? id = null,
        long version = 4,
        string status = "Processing") => new(
        id ?? Guid.NewGuid(),
        "a07TG00000PMLtSYAX",
        "7265-17552",
        "Dena-Internal transaction",
        "847513",
        "NL",
        Guid.NewGuid(),
        "Producer AS",
        2.75m,
        new DateOnly(2026, 1, 1),
        null,
        null,
        "Dena",
        status,
        new DateOnly(2026, 1, 2),
        100m,
        100m,
        "Biogas",
        "fixture certificate",
        version,
        CreatedAt,
        UpdatedAt);
}

internal sealed class FakeBioticketEndpointRepository : IBioticketRepository
{
    public BioticketDetailsDto CreateResult { get; set; } = DomainEndpointTestData.Bioticket();
    public BioticketDetailsDto? UpdateResult { get; set; }
    public BioticketDetailsDto? GetByIdResult { get; set; }
    public MutationOutcome? CancelResult { get; set; }
    public GetBioticketHistoryResponse HistoryResult { get; set; } =
        new([DomainEndpointTestData.Bioticket()], 1, 1, 50, false);

    public List<(Guid Id, CancellationToken Token)> GetByIdCalls { get; } = [];
    public List<(GetBioticketHistoryRequest Request, CancellationToken Token)> HistoryCalls { get; } = [];
    public List<(CreateBioticketRequest Request, Guid ActorId, CancellationToken Token)> CreateCalls { get; } = [];
    public List<(UpdateBioticketRequest Request, Guid ActorId, CancellationToken Token)> UpdateCalls { get; } = [];
    public List<(Guid Id, long Version, string Reason, Guid ActorId, CancellationToken Token)> CancelCalls { get; } = [];

    public Task<BioticketDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        GetByIdCalls.Add((id, ct));
        return Task.FromResult(GetByIdResult);
    }

    public Task<GetBioticketHistoryResponse> GetHistoryAsync(GetBioticketHistoryRequest request, CancellationToken ct)
    {
        HistoryCalls.Add((request, ct));
        return Task.FromResult(HistoryResult);
    }

    public Task<BioticketDetailsDto> CreateAtomicAsync(CreateBioticketRequest request, Guid actorId, CancellationToken ct)
    {
        CreateCalls.Add((request, actorId, ct));
        return Task.FromResult(CreateResult);
    }

    public Task<BioticketDetailsDto?> UpdateAtomicAsync(UpdateBioticketRequest request, Guid actorId, CancellationToken ct)
    {
        UpdateCalls.Add((request, actorId, ct));
        return Task.FromResult(UpdateResult);
    }

    public Task<MutationOutcome?> CancelAtomicAsync(Guid id, long version, string reason, Guid actorId, CancellationToken ct)
    {
        CancelCalls.Add((id, version, reason, actorId, ct));
        return Task.FromResult(CancelResult);
    }
}

internal sealed class FakeCapacityBookingEndpointRepository : ICapacityBookingRepository
{
    public CapacityBookingDetailsDto CreateResult { get; set; } = DomainEndpointTestData.CapacityBooking();
    public CapacityBookingDetailsDto? UpdateResult { get; set; }
    public CapacityBookingDetailsDto? GetByIdResult { get; set; }
    public MutationOutcome? DeleteResult { get; set; }
    public GetCapacityBookingHistoryResponse HistoryResult { get; set; } =
        new([DomainEndpointTestData.CapacityBooking()], 1, 1, 50, false);

    public List<(Guid Id, CancellationToken Token)> GetByIdCalls { get; } = [];
    public List<(GetCapacityBookingHistoryRequest Request, CancellationToken Token)> HistoryCalls { get; } = [];
    public List<(CreateCapacityBookingRequest Request, Guid ActorId, CancellationToken Token)> CreateCalls { get; } = [];
    public List<(UpdateCapacityBookingRequest Request, Guid ActorId, CancellationToken Token)> UpdateCalls { get; } = [];
    public List<(Guid Id, long Version, string Reason, Guid ActorId, CancellationToken Token)> DeleteCalls { get; } = [];

    public Task<CapacityBookingDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        GetByIdCalls.Add((id, ct));
        return Task.FromResult(GetByIdResult);
    }

    public Task<GetCapacityBookingHistoryResponse> GetHistoryAsync(GetCapacityBookingHistoryRequest request, CancellationToken ct)
    {
        HistoryCalls.Add((request, ct));
        return Task.FromResult(HistoryResult);
    }

    public Task<CapacityBookingDetailsDto> CreateAtomicAsync(CreateCapacityBookingRequest request, Guid actorId, CancellationToken ct)
    {
        CreateCalls.Add((request, actorId, ct));
        return Task.FromResult(CreateResult);
    }

    public Task<CapacityBookingDetailsDto?> UpdateAtomicAsync(UpdateCapacityBookingRequest request, Guid actorId, CancellationToken ct)
    {
        UpdateCalls.Add((request, actorId, ct));
        return Task.FromResult(UpdateResult);
    }

    public Task<MutationOutcome?> DeleteAtomicAsync(Guid id, long version, string reason, Guid actorId, CancellationToken ct)
    {
        DeleteCalls.Add((id, version, reason, actorId, ct));
        return Task.FromResult(DeleteResult);
    }
}

internal sealed class FakeContractEndpointRepository : IContractRepository
{
    public ContractDetailsDto CreateResult { get; set; } = DomainEndpointTestData.Contract();
    public ContractDetailsDto? UpdateResult { get; set; }
    public ContractDetailsDto? GetByIdResult { get; set; }
    public MutationOutcome? DeactivateResult { get; set; }
    public GetContractHistoryResponse HistoryResult { get; set; } =
        new([DomainEndpointTestData.Contract()], 1, 1, 50, false);

    public List<(Guid Id, CancellationToken Token)> GetByIdCalls { get; } = [];
    public List<(GetContractHistoryRequest Request, CancellationToken Token)> HistoryCalls { get; } = [];
    public List<(CreateContractRequest Request, Guid ActorId, CancellationToken Token)> CreateCalls { get; } = [];
    public List<(UpdateContractRequest Request, Guid ActorId, CancellationToken Token)> UpdateCalls { get; } = [];
    public List<(Guid Id, long Version, string Reason, Guid ActorId, CancellationToken Token)> DeactivateCalls { get; } = [];

    public Task<ContractDetailsDto?> GetByIdAsync(Guid contractId, CancellationToken ct)
    {
        GetByIdCalls.Add((contractId, ct));
        return Task.FromResult(GetByIdResult);
    }

    public Task<GetContractHistoryResponse> GetHistoryAsync(GetContractHistoryRequest request, CancellationToken ct)
    {
        HistoryCalls.Add((request, ct));
        return Task.FromResult(HistoryResult);
    }

    public Task<ContractDetailsDto> CreateAtomicAsync(CreateContractRequest request, Guid actorId, CancellationToken ct)
    {
        CreateCalls.Add((request, actorId, ct));
        return Task.FromResult(CreateResult);
    }

    public Task<ContractDetailsDto?> UpdateAtomicAsync(UpdateContractRequest request, Guid actorId, CancellationToken ct)
    {
        UpdateCalls.Add((request, actorId, ct));
        return Task.FromResult(UpdateResult);
    }

    public Task<MutationOutcome?> DeactivateAtomicAsync(Guid contractId, long version, string reason, Guid actorId, CancellationToken ct)
    {
        DeactivateCalls.Add((contractId, version, reason, actorId, ct));
        return Task.FromResult(DeactivateResult);
    }
}

internal sealed class FakeGooCertificateEndpointRepository : IGooCertificateRepository
{
    public GooCertificateTransactionDetailsDto CreateResult { get; set; } = DomainEndpointTestData.GooCertificate();
    public GooCertificateTransactionDetailsDto? UpdateResult { get; set; }
    public GooCertificateTransactionDetailsDto? BatchExportResult { get; set; }
    public GooCertificateTransactionDetailsDto? GetByIdResult { get; set; }
    public MutationOutcome? DeleteResult { get; set; }
    public GetGooCertificateHistoryResponse HistoryResult { get; set; } =
        new([DomainEndpointTestData.GooCertificate()], 1, 1, 50, false);

    public List<(Guid Id, CancellationToken Token)> GetByIdCalls { get; } = [];
    public List<(GetGooCertificateHistoryRequest Request, CancellationToken Token)> HistoryCalls { get; } = [];
    public List<(CreateGooCertificateTransactionRequest Request, Guid ActorId, CancellationToken Token)> CreateCalls { get; } = [];
    public List<(UpdateGooCertificateTransactionRequest Request, Guid ActorId, CancellationToken Token)> UpdateCalls { get; } = [];
    public List<(Guid Id, long Version, Guid ActorId, CancellationToken Token)> BatchExportCalls { get; } = [];
    public List<(Guid Id, long Version, string Reason, Guid ActorId, CancellationToken Token)> DeleteCalls { get; } = [];

    public Task<GooCertificateTransactionDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        GetByIdCalls.Add((id, ct));
        return Task.FromResult(GetByIdResult);
    }

    public Task<GetGooCertificateHistoryResponse> GetHistoryAsync(GetGooCertificateHistoryRequest request, CancellationToken ct)
    {
        HistoryCalls.Add((request, ct));
        return Task.FromResult(HistoryResult);
    }

    public Task<GooCertificateTransactionDetailsDto> CreateAtomicAsync(CreateGooCertificateTransactionRequest request, Guid actorId, CancellationToken ct)
    {
        CreateCalls.Add((request, actorId, ct));
        return Task.FromResult(CreateResult);
    }

    public Task<GooCertificateTransactionDetailsDto?> UpdateAtomicAsync(UpdateGooCertificateTransactionRequest request, Guid actorId, CancellationToken ct)
    {
        UpdateCalls.Add((request, actorId, ct));
        return Task.FromResult(UpdateResult);
    }

    public Task<GooCertificateTransactionDetailsDto?> RequestBatchExportAtomicAsync(Guid id, long version, Guid actorId, CancellationToken ct)
    {
        BatchExportCalls.Add((id, version, actorId, ct));
        return Task.FromResult(BatchExportResult);
    }

    public Task<MutationOutcome?> DeleteAtomicAsync(Guid id, long version, string reason, Guid actorId, CancellationToken ct)
    {
        DeleteCalls.Add((id, version, reason, actorId, ct));
        return Task.FromResult(DeleteResult);
    }
}
