# Task 02: .NET 9 FastEndpoints REPR Backend Architecture & Vertical Slices

- **Phase**: Application Core Engine
- **Lead / Owner**: Backend Lead Architect
- **Complexity**: High
- **Prerequisites**: Task 01: Core Database Architecture, Entity Model & TimescaleDB Bi-Temporal Audit Setup
- **Target Files**:
  - `src/Backend/Tradebook.sln`
  - `src/Backend/Tradebook.Api/Tradebook.Api.csproj`
  - `src/Backend/Tradebook.Api/Program.cs`
  - `src/Backend/Tradebook.Api/AppJsonSerializerContext.cs`
  - `src/Backend/Tradebook.Api/Features/PhysicalDeliveries/CreatePhysicalDelivery/CreatePhysicalDeliveryEndpoint.cs`
  - `src/Backend/Tradebook.Api/Features/PhysicalDeliveries/CreatePhysicalDelivery/CreatePhysicalDeliveryValidator.cs`
  - `src/Backend/Tradebook.Api/Features/PhysicalDeliveries/GetDeliveryHistory/GetDeliveryHistoryEndpoint.cs`
  - `src/Backend/Tradebook.Api/Features/PhysicalDeliveries/GetDeliveryById/GetDeliveryByIdEndpoint.cs`
  - `src/Backend/Tradebook.Api/Features/PhysicalDeliveries/UpdatePhysicalDelivery/UpdatePhysicalDeliveryEndpoint.cs`
  - `src/Backend/Tradebook.Api/Features/PhysicalDeliveries/DeletePhysicalDelivery/DeletePhysicalDeliveryEndpoint.cs`
  - `src/Backend/Tradebook.Core/Tradebook.Core.csproj`
  - `src/Backend/Tradebook.Core/Domain/Entities/PhysicalDelivery.cs`
  - `src/Backend/Tradebook.Core/Domain/Entities/Contract.cs`
  - `src/Backend/Tradebook.Core/Domain/Entities/Counterparty.cs`
  - `src/Backend/Tradebook.Core/Domain/Entities/AuditLog.cs`
  - `src/Backend/Tradebook.Core/Domain/Entities/OutboxEvent.cs`
  - `src/Backend/Tradebook.Core/DTOs/DeliveryDtos.cs`
  - `src/Backend/Tradebook.Core/DTOs/CommonDtos.cs`
  - `src/Backend/Tradebook.Core/Interfaces/IDeliveryRepository.cs`
  - `src/Backend/Tradebook.Core/Interfaces/ICacheService.cs`
  - `src/Backend/Tradebook.Core/Infrastructure/Data/NpgsqlConnectionFactory.cs`
  - `src/Backend/Tradebook.Core/Infrastructure/Data/DeliveryRepository.cs`
  - `src/Backend/Tradebook.Core/Infrastructure/Caching/HybridCacheService.cs`
  - `src/Backend/Tradebook.Core/Infrastructure/Options/DatabaseOptions.cs`
  - `src/Backend/Tradebook.Tests/Tradebook.Tests.csproj`
  - `src/Backend/Tradebook.Tests/Unit/CreatePhysicalDeliveryValidatorTests.cs`
  - `src/Backend/Tradebook.Tests/Integration/DeliveryEndpointIntegrationTests.cs`
  - `src/Backend/Tradebook.Tests/Architecture/SliceBoundaryTests.cs`

---

## 1. Objectives, Scope & Feature Coverage

### 1.1 Objectives
Task 02 establishes the high-performance .NET 9 Web API backend for the **Tradebook** gas, GoO and bioticket trading platform: a **Modular Monolith** using the **REPR (Request-Endpoint-Response)** pattern via **FastEndpoints**, compiled with **Native AOT (`<PublishAot>true</PublishAot>`)**, backed by **PostgreSQL 17 / TimescaleDB** through **Npgsql** and **Dapper**.

Key objectives:
1. **Native AOT Performance**: Eliminate JIT overhead — cold starts **<5ms**, idle memory footprint **<30MB**.
2. **FastEndpoints REPR Architecture**: Organize API endpoints into vertical feature slices over the real domain (`Features/PhysicalDeliveries`, `Features/Contracts`, `Features/CapacityBookings`, `Features/Transfers`, `Features/Biotickets`, `Features/GoOCertificates`, `Features/MarketPrices`, `Features/TaxTariffs`), each encapsulating Request DTO, Response DTO, Validation Rules, and Execution Logic in dedicated files.
3. **Optimized Data Access**: Zero-allocation database access via `NpgsqlDataSource` with Dapper for read-heavy operations, multiplexing connections over PostgreSQL 17. SQL targets the Task 01 entity-model tables (`physical_deliveries`, `contracts`, `counterparties`, `capacity_bookings`, `transfers`, `bioticket_deliveries`, `tax_tariffs`, `hedges`, `market_prices`, `goo_certificate_transactions`, `invoice_line_items`, `external_cogs`).
4. **Multi-Tier `HybridCache`**: .NET 9 `HybridCache` combines L1 sub-microsecond in-memory caching with L2 invalidation hooks.
5. **Contract First & TypeGen Integration**: C# request/response record DTOs carry TypeGen attributes (`[ExportTsInterface]`, `[TsType]`) for deterministic C#-to-TypeScript contract generation for the React 19 frontend.
6. **Atomic Transactional Outbox & Bi-Temporal Audit**: Every domain mutation runs in a single atomic PostgreSQL transaction that writes the entity change, records the bi-temporal audit entry (via Task 01's generic trigger + `app.actor_id` session setting), and enqueues a transactional outbox event.

### 1.2 Scope
- Solution setup: `src/Backend/Tradebook.sln` containing `Tradebook.Api`, `Tradebook.Core`, and `Tradebook.Tests`.
- Full C# domain models (`PhysicalDelivery`, `Contract`, `Counterparty`, `AuditLog`, `OutboxEvent`) mapped to entity-model.md.
- Complete vertical slice implementations for the `PhysicalDeliveries` domain (the flagship example — all other slices follow the identical pattern):
  - `CreatePhysicalDeliveryEndpoint`: Validates request, writes atomic Postgres transaction, invalidates cache, returns `201 Created`.
  - `GetDeliveryHistoryEndpoint`: Executes paginated, filtered, keyset/offset Dapper queries with JSONB metadata filters.
  - `GetDeliveryByIdEndpoint`: Utilizes `HybridCache` with 5-minute TTL to return delivery details in <0.5ms.
  - `UpdatePhysicalDeliveryEndpoint`: Updates delivery state (e.g. `volume_realised_mwh`, invoice status) with optimistic concurrency check (`xmin` column).
  - `DeletePhysicalDeliveryEndpoint`: Performs bi-temporal deletion and audit logging.
- `System.Text.Json` Source Generator (`AppJsonSerializerContext`) for reflection-free AOT serialization.
- Comprehensive test harness (`Tradebook.Tests`): Unit tests for validators, Integration tests using `Testcontainers.PostgreSql`, and `ArchUnitNET` slice boundary assertions.

### 1.3 Prerequisites & Dependencies
- **Prerequisites**: Task 01 (`tasks/task-01-database-and-timescaledb-setup.md`) — PostgreSQL 17 entity-model schema (`physical_deliveries`, `contracts`, `counterparties`, `audit_log`, `outbox_events`, …), the generic bi-temporal audit trigger, and `get_entity_state_as_of`.
- **SDK**: .NET 9.0 SDK (native AOT build tools installed: `clang`/`msvc`).
- **NuGet Packages**:
  - `FastEndpoints` (v5.30+)
  - `FastEndpoints.Validation`
  - `Npgsql` (v9.0+)
  - `Dapper` (v2.1+)
  - `Microsoft.Extensions.Caching.Hybrid` (v9.0+)
  - `TypeGen` (v4.0+)
  - `xunit`, `FluentAssertions`, `Testcontainers.PostgreSql`, `ArchUnitNET.xUnit`

---

## 2. Solution & Project Directory Structure

The backend follows a Modular Monolith layout: vertical feature slices inside `Tradebook.Api`, shared domain infrastructure inside `Tradebook.Core`.

```
src/Backend/
├── Tradebook.sln
├── Tradebook.Api/
│   ├── Tradebook.Api.csproj
│   ├── Program.cs
│   ├── AppJsonSerializerContext.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── Features/
│       ├── PhysicalDeliveries/
│       │   ├── CreatePhysicalDelivery/
│       │   │   ├── CreatePhysicalDeliveryEndpoint.cs
│       │   │   └── CreatePhysicalDeliveryValidator.cs
│       │   ├── GetDeliveryHistory/
│       │   │   └── GetDeliveryHistoryEndpoint.cs
│       │   ├── GetDeliveryById/
│       │   │   └── GetDeliveryByIdEndpoint.cs
│       │   ├── UpdatePhysicalDelivery/
│       │   │   ├── UpdatePhysicalDeliveryEndpoint.cs
│       │   │   └── UpdatePhysicalDeliveryValidator.cs
│       │   └── DeletePhysicalDelivery/
│       │       └── DeletePhysicalDeliveryEndpoint.cs
│       ├── Contracts/
│       │   └── (CreateContract, GetContractById, GetContractHistory — same REPR pattern)
│       ├── CapacityBookings/
│       │   └── (CreateCapacityBooking, GetCapacityBookingHistory — same REPR pattern)
│       ├── Transfers/
│       │   └── (CreateTransfer, GetTransferHistory — same REPR pattern)
│       ├── Biotickets/
│       │   └── (CreateBioticketDelivery, GetBioticketHistory — same REPR pattern)
│       ├── GoOCertificates/
│       │   └── (GetGoOCertificateTransactions, BatchExport — same REPR pattern)
│       ├── MarketPrices/
│       │   └── (UpsertMarketPrices, GetMarketPriceHistory — same REPR pattern)
│       └── TaxTariffs/
│           └── (UpsertTaxTariffs, GetTaxTariffs — same REPR pattern)
├── Tradebook.Core/
│   ├── Tradebook.Core.csproj
│   ├── Domain/
│   │   ├── Entities/
│   │   │   ├── PhysicalDelivery.cs
│   │   │   ├── Contract.cs
│   │   │   ├── Counterparty.cs
│   │   │   ├── AuditLog.cs
│   │   │   └── OutboxEvent.cs
│   │   └── Enums/
│   │       ├── BookType.cs
│   │       ├── ProductType.cs
│   │       ├── ReportStatus.cs
│   │       └── GasPriceMechanism.cs
│   ├── DTOs/
│   │   ├── DeliveryDtos.cs
│   │   ├── ContractDtos.cs
│   │   └── CommonDtos.cs
│   ├── Interfaces/
│   │   ├── IDeliveryRepository.cs
│   │   ├── IContractRepository.cs
│   │   ├── IAuditLogger.cs
│   │   └── ICacheService.cs
│   ├── Infrastructure/
│   │   ├── Data/
│   │   │   ├── NpgsqlConnectionFactory.cs
│   │   │   ├── DeliveryRepository.cs
│   │   │   └── DapperTypeHandlers.cs
│   │   ├── Caching/
│   │   │   └── HybridCacheService.cs
│   │   └── Options/
│   │       └── DatabaseOptions.cs
│   └── Services/
│       └── DeliveryService.cs
└── Tradebook.Tests/
    ├── Tradebook.Tests.csproj
    ├── Unit/
    │   ├── CreatePhysicalDeliveryValidatorTests.cs
    │   └── DeliveryServiceTests.cs
    ├── Integration/
    │   ├── DeliveryEndpointIntegrationTests.cs
    │   └── PostgresTestFixture.cs
    └── Architecture/
        └── SliceBoundaryTests.cs
```

### Layer Responsibilities
1. **`Tradebook.Api`**: Host application, FastEndpoints config, HTTP routing, middleware pipeline, REPR endpoints, `FluentValidation` rules, `System.Text.Json` source generator context for Native AOT.
2. **`Tradebook.Core`**: Domain models, C# record DTOs with TypeGen attributes, Dapper SQL queries, `NpgsqlDataSource` connection factory, `HybridCache` abstraction, core business services.
3. **`Tradebook.Tests`**: Unit test suite, hermetic Postgres integration tests via `Testcontainers`, ArchUnitNET boundary rules enforcing slice isolation.

---

## 3. Architecture & Code Contract Blueprints

### 3.1 Project File Configurations (`.csproj`)

#### `src/Backend/Tradebook.Api/Tradebook.Api.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <PublishAot>true</PublishAot>
    <EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>
    <JsonSerializerIsReflectionDisabledByDefault>true</JsonSerializerIsReflectionDisabledByDefault>
    <IlcGenerateCompleteTypeMetadata>true</IlcGenerateCompleteTypeMetadata>
    <OptimizationPreference>Speed</OptimizationPreference>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="FastEndpoints" Version="5.30.0" />
    <PackageReference Include="FastEndpoints.Validation" Version="5.30.0" />
    <PackageReference Include="Microsoft.Extensions.Caching.Hybrid" Version="9.0.0-preview.9.24556.5" />
    <PackageReference Include="Npgsql" Version="9.0.2" />
    <PackageReference Include="Dapper" Version="2.1.35" />
    <PackageReference Include="TypeGen" Version="4.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Tradebook.Core\Tradebook.Core.csproj" />
  </ItemGroup>

</Project>
```

#### `src/Backend/Tradebook.Core/Tradebook.Core.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsAotCompatible>true</IsAotCompatible>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Npgsql" Version="9.0.2" />
    <PackageReference Include="Dapper" Version="2.1.35" />
    <PackageReference Include="Microsoft.Extensions.Caching.Hybrid" Version="9.0.0-preview.9.24556.5" />
    <PackageReference Include="TypeGen" Version="4.0.0" />
  </ItemGroup>

</Project>
```

---

### 3.2 Request/Response C# Record DTOs with TypeGen Attributes

All DTOs are declared as immutable C# `record` types and annotated with `TypeGen` attributes for automatic TypeScript interface generation. Field names mirror the `physical_deliveries` columns in `architecture/entity-model.md` §2.5.

#### `src/Backend/Tradebook.Core/DTOs/DeliveryDtos.cs`
```csharp
using System;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface(OutputDir = "../Frontend/src/types/generated")]
public record CreatePhysicalDeliveryRequest(
    [property: TsType(TsType.String)] Guid ContractId,
    string ContractInstanceId,
    string BookType,                        // book_type_enum: Sourcing | Sales | Intercompany
    DateTime SupplyMonth,                   // first day of the delivery month
    [property: TsOptional] decimal? CapacityMw,
    [property: TsOptional] decimal? VolumeNominatedMwh,
    [property: TsOptional] decimal? VolumeRealisedMwh,
    [property: TsOptional] decimal? PriceEurMwh,
    [property: TsOptional] string? PriceMechanism,  // gas_price_mech_enum
    [property: TsOptional] DateTime? StartDay,
    [property: TsOptional] DateTime? EndDay,
    [property: TsOptional, TsType(TsType.Any)] string? CustomFieldsJson
);

[ExportTsInterface(OutputDir = "../Frontend/src/types/generated")]
public record CreatePhysicalDeliveryResponse(
    [property: TsType(TsType.String)] Guid DeliveryId,
    string ContractInstanceId,
    [property: TsOptional] decimal? InvoiceAmountEur,
    string Status,
    DateTime CreatedAt
);

[ExportTsInterface(OutputDir = "../Frontend/src/types/generated")]
public record PhysicalDeliveryDetailsDto(
    [property: TsType(TsType.String)] Guid DeliveryId,
    [property: TsType(TsType.String)] Guid ContractId,
    string ContractInstanceId,
    string BookType,
    DateTime SupplyMonth,
    [property: TsOptional] decimal? VolumeNominatedMwh,
    [property: TsOptional] decimal? VolumeRealisedMwh,
    [property: TsOptional] decimal? VolumeMwh,
    [property: TsOptional] decimal? PriceEurMwh,
    [property: TsOptional] decimal? RevenueEur,
    [property: TsOptional] decimal? SubtotalEur,
    [property: TsOptional] decimal? VatEur,
    [property: TsOptional] decimal? InvoiceAmountEur,
    string Status,
    string CustomFieldsJson,
    uint Version,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

[ExportTsInterface(OutputDir = "../Frontend/src/types/generated")]
public record GetDeliveryHistoryRequest(
    [property: TsOptional, TsType(TsType.String)] Guid? ContractId,
    [property: TsOptional] string? ContractInstanceId,
    [property: TsOptional] string? BookType,
    [property: TsOptional] string? Status,
    [property: TsOptional] DateTime? FromMonth,
    [property: TsOptional] DateTime? ToMonth,
    [property: TsOptional] int Page = 1,
    [property: TsOptional] int PageSize = 50
);

[ExportTsInterface(OutputDir = "../Frontend/src/types/generated")]
public record GetDeliveryHistoryResponse(
    IReadOnlyList<PhysicalDeliveryDetailsDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    bool HasNextPage
);

[ExportTsInterface(OutputDir = "../Frontend/src/types/generated")]
public record UpdatePhysicalDeliveryRequest(
    [property: TsType(TsType.String)] Guid DeliveryId,
    [property: TsOptional] decimal? VolumeRealisedMwh,
    [property: TsOptional] decimal? PriceEurMwh,
    [property: TsOptional] string? Status,
    [property: TsOptional, TsType(TsType.Any)] string? CustomFieldsJson,
    uint Version
);

[ExportTsInterface(OutputDir = "../Frontend/src/types/generated")]
public record DeletePhysicalDeliveryRequest(
    [property: TsType(TsType.String)] Guid DeliveryId,
    [property: TsType(TsType.String)] Guid ActorId,
    string Reason
);
```

#### `src/Backend/Tradebook.Core/DTOs/CommonDtos.cs`
```csharp
using System.Collections.Generic;
using TypeGen.Core.TypeAnnotations;

namespace Tradebook.Core.DTOs;

[ExportTsInterface(OutputDir = "../Frontend/src/types/generated")]
public record ProblemDetailsResponse(
    string Type,
    string Title,
    int Status,
    string Detail,
    string Instance,
    IDictionary<string, string[]> Errors
);
```

---

### 3.3 Data Access Layer (Npgsql / Dapper & Atomic Transactions)

#### `src/Backend/Tradebook.Core/Infrastructure/Data/NpgsqlConnectionFactory.cs`
```csharp
using System.Data;
using Microsoft.Extensions.Options;
using Npgsql;
using Tradebook.Core.Infrastructure.Options;

namespace Tradebook.Core.Infrastructure.Data;

public interface INpgsqlConnectionFactory
{
    ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
    NpgsqlDataSource DataSource { get; }
}

public sealed class NpgsqlConnectionFactory : INpgsqlConnectionFactory
{
    public NpgsqlDataSource DataSource { get; }

    public NpgsqlConnectionFactory(IOptions<DatabaseOptions> options)
    {
        var connectionString = options.Value.ConnectionString;
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);

        // Register Npgsql enum mappings for entity-model enums (book_type, gas_price_mech, ...)
        // e.g. dataSourceBuilder.MapEnum<BookType>("book_type_enum");
        DataSource = dataSourceBuilder.Build();
    }

    public async ValueTask<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        return await DataSource.OpenConnectionAsync(cancellationToken);
    }
}
```

#### `src/Backend/Tradebook.Core/Infrastructure/Data/DeliveryRepository.cs`
```csharp
using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Tradebook.Core.DTOs;

namespace Tradebook.Core.Infrastructure.Data;

public interface IDeliveryRepository
{
    Task<PhysicalDeliveryDetailsDto?> GetByIdAsync(Guid deliveryId, CancellationToken ct);
    Task<GetDeliveryHistoryResponse> GetHistoryAsync(GetDeliveryHistoryRequest request, CancellationToken ct);
    Task<PhysicalDeliveryDetailsDto> CreateAtomicAsync(CreatePhysicalDeliveryRequest request, Guid actorId, CancellationToken ct);
    Task<PhysicalDeliveryDetailsDto?> UpdateAtomicAsync(UpdatePhysicalDeliveryRequest request, Guid actorId, CancellationToken ct);
    Task<bool> DeleteAtomicAsync(DeletePhysicalDeliveryRequest request, CancellationToken ct);
}

public sealed class DeliveryRepository : IDeliveryRepository
{
    private readonly INpgsqlConnectionFactory _connectionFactory;

    public DeliveryRepository(INpgsqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PhysicalDeliveryDetailsDto?> GetByIdAsync(Guid deliveryId, CancellationToken ct)
    {
        const string sql = """
            SELECT
                id AS DeliveryId,
                contract_id AS ContractId,
                contract_instance_id AS ContractInstanceId,
                book_type AS BookType,
                supply_month AS SupplyMonth,
                volume_nominated_mwh AS VolumeNominatedMwh,
                volume_realised_mwh AS VolumeRealisedMwh,
                volume_mwh AS VolumeMwh,
                price_mechanism AS PriceMechanism,
                revenue_eur AS RevenueEur,
                subtotal_eur AS SubtotalEur,
                vat_eur AS VatEur,
                invoice_amount_eur AS InvoiceAmountEur,
                status AS Status,
                custom_fields::text AS CustomFieldsJson,
                xmin::text::uint4 AS Version,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM physical_deliveries
            WHERE id = @DeliveryId;
            """;

        await using var conn = await _connectionFactory.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<PhysicalDeliveryDetailsDto>(
            new CommandDefinition(sql, new { DeliveryId = deliveryId }, cancellationToken: ct));
    }

    public async Task<GetDeliveryHistoryResponse> GetHistoryAsync(GetDeliveryHistoryRequest request, CancellationToken ct)
    {
        var builder = new SqlBuilder();
        var selector = builder.AddTemplate("""
            SELECT
                id AS DeliveryId, contract_id AS ContractId, contract_instance_id AS ContractInstanceId,
                book_type AS BookType, supply_month AS SupplyMonth,
                volume_nominated_mwh AS VolumeNominatedMwh, volume_realised_mwh AS VolumeRealisedMwh,
                volume_mwh AS VolumeMwh, price_mechanism AS PriceMechanism,
                revenue_eur AS RevenueEur, subtotal_eur AS SubtotalEur, vat_eur AS VatEur,
                invoice_amount_eur AS InvoiceAmountEur, status AS Status,
                custom_fields::text AS CustomFieldsJson, xmin::text::uint4 AS Version,
                created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM physical_deliveries
            /**where**/
            ORDER BY supply_month DESC, contract_instance_id
            LIMIT @Limit OFFSET @Offset
            """);

        var countTemplate = builder.AddTemplate("SELECT COUNT(1) FROM physical_deliveries /**where**/");

        if (request.ContractId.HasValue)
            builder.Where("contract_id = @ContractId", new { ContractId = request.ContractId.Value });

        if (!string.IsNullOrWhiteSpace(request.ContractInstanceId))
            builder.Where("contract_instance_id = @ContractInstanceId", new { request.ContractInstanceId });

        if (!string.IsNullOrWhiteSpace(request.BookType))
            builder.Where("book_type = @BookType", new { request.BookType });

        if (!string.IsNullOrWhiteSpace(request.Status))
            builder.Where("status = @Status", new { request.Status });

        if (request.FromMonth.HasValue)
            builder.Where("supply_month >= @FromMonth", new { FromMonth = request.FromMonth.Value });

        if (request.ToMonth.HasValue)
            builder.Where("supply_month <= @ToMonth", new { ToMonth = request.ToMonth.Value });

        int offset = (request.Page - 1) * request.PageSize;
        var dynamicParams = selector.Parameters;
        dynamicParams.Add("Limit", request.PageSize);
        dynamicParams.Add("Offset", offset);

        await using var conn = await _connectionFactory.OpenConnectionAsync(ct);

        var items = (await conn.QueryAsync<PhysicalDeliveryDetailsDto>(
            new CommandDefinition(selector.RawSql, dynamicParams, cancellationToken: ct))).AsList();
        int totalCount = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(countTemplate.RawSql, dynamicParams, cancellationToken: ct));

        bool hasNextPage = offset + items.Count < totalCount;

        return new GetDeliveryHistoryResponse(items, totalCount, request.Page, request.PageSize, hasNextPage);
    }

    public async Task<PhysicalDeliveryDetailsDto> CreateAtomicAsync(CreatePhysicalDeliveryRequest request, Guid actorId, CancellationToken ct)
    {
        await using var conn = await _connectionFactory.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        try
        {
            var deliveryId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var customFieldsJson = request.CustomFieldsJson ?? "{}";

            // 1. Set actor context for the generic bi-temporal audit trigger (Task 01)
            await conn.ExecuteAsync(
                "SELECT set_config('app.actor_id', @ActorId, true);",
                new { ActorId = actorId.ToString() }, transaction: tx, cancellationToken: ct);

            // 2. Insert delivery record; the AFTER trigger writes audit_log atomically
            const string insertSql = """
                INSERT INTO physical_deliveries (
                    id, contract_id, contract_instance_id, book_type, supply_month,
                    capacity_mw, volume_nominated_mwh, volume_realised_mwh, price_mechanism,
                    start_day, end_day, status, custom_fields, created_at, updated_at
                ) VALUES (
                    @DeliveryId, @ContractId, @ContractInstanceId, @BookType, @SupplyMonth,
                    @CapacityMw, @VolumeNominatedMwh, @VolumeRealisedMwh, @PriceMechanism,
                    @StartDay, @EndDay, @Status, @CustomFields::jsonb, @CreatedAt, @UpdatedAt
                ) RETURNING xmin::text::uint4;
                """;

            var version = await conn.ExecuteScalarAsync<uint>(new CommandDefinition(insertSql, new
            {
                DeliveryId = deliveryId,
                request.ContractId,
                request.ContractInstanceId,
                request.BookType,
                request.SupplyMonth,
                request.CapacityMw,
                request.VolumeNominatedMwh,
                request.VolumeRealisedMwh,
                request.PriceMechanism,
                request.StartDay,
                request.EndDay,
                Status = "Pending - No Invoice",
                CustomFields = customFieldsJson,
                CreatedAt = now,
                UpdatedAt = now
            }, transaction: tx, cancellationToken: ct));

            // 3. Enqueue transactional outbox event (NATS consumer = Task 03)
            const string insertOutboxSql = """
                INSERT INTO outbox_events (aggregate_type, aggregate_id, event_type, payload)
                VALUES ('physical_deliveries', @AggregateId, 'INSERT', @Payload::jsonb);
                """;

            await conn.ExecuteAsync(new CommandDefinition(insertOutboxSql, new
            {
                AggregateId = deliveryId.ToString(),
                Payload = JsonSerializer.Serialize(new
                {
                    deliveryId,
                    request.ContractId,
                    request.ContractInstanceId,
                    request.BookType,
                    request.SupplyMonth
                })
            }, transaction: tx, cancellationToken: ct));

            await tx.CommitAsync(ct);

            return new PhysicalDeliveryDetailsDto(
                deliveryId, request.ContractId, request.ContractInstanceId, request.BookType,
                request.SupplyMonth, request.VolumeNominatedMwh, request.VolumeRealisedMwh, null,
                request.PriceEurMwh, null, null, null, null, "Pending - No Invoice",
                customFieldsJson, version, now, now
            );
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<PhysicalDeliveryDetailsDto?> UpdateAtomicAsync(UpdatePhysicalDeliveryRequest request, Guid actorId, CancellationToken ct)
    {
        await using var conn = await _connectionFactory.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        try
        {
            var now = DateTime.UtcNow;

            await conn.ExecuteAsync(
                "SELECT set_config('app.actor_id', @ActorId, true);",
                new { ActorId = actorId.ToString() }, transaction: tx, cancellationToken: ct);

            const string updateSql = """
                UPDATE physical_deliveries SET
                    volume_realised_mwh = COALESCE(@VolumeRealisedMwh, volume_realised_mwh),
                    volume_mwh = @VolumeRealisedMwh,
                    price_mechanism = COALESCE(@PriceEurMwh, price_mechanism),
                    status = COALESCE(@Status, status),
                    custom_fields = COALESCE(@CustomFields::jsonb, custom_fields),
                    updated_at = @UpdatedAt
                WHERE id = @DeliveryId AND xmin::text::uint4 = @Version
                RETURNING
                    id AS DeliveryId, contract_id AS ContractId, contract_instance_id AS ContractInstanceId,
                    book_type AS BookType, supply_month AS SupplyMonth,
                    volume_nominated_mwh AS VolumeNominatedMwh, volume_realised_mwh AS VolumeRealisedMwh,
                    volume_mwh AS VolumeMwh, price_mechanism AS PriceMechanism,
                    revenue_eur AS RevenueEur, subtotal_eur AS SubtotalEur, vat_eur AS VatEur,
                    invoice_amount_eur AS InvoiceAmountEur, status AS Status,
                    custom_fields::text AS CustomFieldsJson, xmin::text::uint4 AS Version,
                    created_at AS CreatedAt, updated_at AS UpdatedAt;
                """;

            var updated = await conn.QuerySingleOrDefaultAsync<PhysicalDeliveryDetailsDto>(
                new CommandDefinition(updateSql, new
                {
                    request.VolumeRealisedMwh,
                    request.PriceEurMwh,
                    request.Status,
                    CustomFields = request.CustomFieldsJson,
                    UpdatedAt = now,
                    request.DeliveryId,
                    request.Version
                }, transaction: tx, cancellationToken: ct));

            if (updated is null)
            {
                await tx.RollbackAsync(ct);
                return null;
            }

            await tx.CommitAsync(ct);
            return updated;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> DeleteAtomicAsync(DeletePhysicalDeliveryRequest request, CancellationToken ct)
    {
        await using var conn = await _connectionFactory.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        try
        {
            await conn.ExecuteAsync(
                "SELECT set_config('app.actor_id', @ActorId, true);",
                new { ActorId = request.ActorId.ToString() }, transaction: tx, cancellationToken: ct);

            const string deleteSql = "DELETE FROM physical_deliveries WHERE id = @DeliveryId;";
            int affected = await conn.ExecuteAsync(
                new CommandDefinition(deleteSql, new { request.DeliveryId }, transaction: tx, cancellationToken: ct));

            if (affected == 0)
            {
                await tx.RollbackAsync(ct);
                return false;
            }

            await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
```

---

### 3.4 HybridCache Configuration

#### `src/Backend/Tradebook.Core/Infrastructure/Caching/HybridCacheService.cs`
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Hybrid;

namespace Tradebook.Core.Infrastructure.Caching;

public interface ICacheService
{
    ValueTask<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
}

public sealed class HybridCacheService : ICacheService
{
    private readonly HybridCache _cache;
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

    public HybridCacheService(HybridCache cache)
    {
        _cache = cache;
    }

    public async ValueTask<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var options = new HybridCacheEntryOptions
        {
            Expiration = expiration ?? DefaultExpiration,
            LocalCacheExpiration = expiration ?? DefaultExpiration
        };

        return await _cache.GetOrCreateAsync(
            key,
            async ct => await factory(ct),
            options,
            cancellationToken: cancellationToken
        );
    }

    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(key, cancellationToken);
    }
}
```

---

### 3.5 FastEndpoints REPR Endpoint Implementations

#### `src/Backend/Tradebook.Api/Features/PhysicalDeliveries/CreatePhysicalDelivery/CreatePhysicalDeliveryValidator.cs`
```csharp
using FastEndpoints;
using FluentValidation;
using Tradebook.Core.DTOs;

namespace Tradebook.Api.Features.PhysicalDeliveries.CreatePhysicalDelivery;

public sealed class CreatePhysicalDeliveryValidator : Validator<CreatePhysicalDeliveryRequest>
{
    public CreatePhysicalDeliveryValidator()
    {
        RuleFor(x => x.ContractId)
            .NotEmpty().WithMessage("ContractId is required.");

        RuleFor(x => x.ContractInstanceId)
            .NotEmpty().MaximumLength(120)
            .WithMessage("ContractInstanceId is required and must not exceed 120 characters.");

        RuleFor(x => x.BookType)
            .Must(b => b is "Sourcing" or "Sales" or "Intercompany")
            .WithMessage("BookType must be one of: Sourcing, Sales, Intercompany.");

        RuleFor(x => x.SupplyMonth)
            .NotEmpty().WithMessage("SupplyMonth is required.");

        RuleFor(x => x.VolumeNominatedMwh)
            .GreaterThanOrEqualTo(0).When(x => x.VolumeNominatedMwh.HasValue)
            .WithMessage("VolumeNominatedMwh cannot be negative.");

        RuleFor(x => x.VolumeRealisedMwh)
            .GreaterThanOrEqualTo(0).When(x => x.VolumeRealisedMwh.HasValue)
            .WithMessage("VolumeRealisedMwh cannot be negative.");

        RuleFor(x => x.PriceEurMwh)
            .GreaterThanOrEqualTo(0).When(x => x.PriceEurMwh.HasValue)
            .WithMessage("PriceEurMwh cannot be negative.");
    }
}
```

#### `src/Backend/Tradebook.Api/Features/PhysicalDeliveries/CreatePhysicalDelivery/CreatePhysicalDeliveryEndpoint.cs`
```csharp
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Tradebook.Core.DTOs;
using Tradebook.Core.Infrastructure.Data;

namespace Tradebook.Api.Features.PhysicalDeliveries.CreatePhysicalDelivery;

public sealed class CreatePhysicalDeliveryEndpoint : Endpoint<CreatePhysicalDeliveryRequest, CreatePhysicalDeliveryResponse>
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ICacheService _cacheService;

    public CreatePhysicalDeliveryEndpoint(IDeliveryRepository deliveryRepository, ICacheService cacheService)
    {
        _deliveryRepository = deliveryRepository;
        _cacheService = cacheService;
    }

    public override void Configure()
    {
        Post("/api/v1/deliveries");
        AllowAnonymous(); // Configure authentication policy as needed
        Description(b => b
            .Produces<CreatePhysicalDeliveryResponse>(201)
            .ProducesProblem(400)
            .ProducesProblem(500));
    }

    public override async Task HandleAsync(CreatePhysicalDeliveryRequest req, CancellationToken ct)
    {
        // Get actor ID from claims or default system actor
        var actorIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var actorId = Guid.TryParse(actorIdClaim, out var parsed) ? parsed : Guid.Empty;

        var delivery = await _deliveryRepository.CreateAtomicAsync(req, actorId, ct);

        // Invalidate list caches
        await _cacheService.RemoveAsync($"deliveries:list", ct);

        var response = new CreatePhysicalDeliveryResponse(
            delivery.DeliveryId,
            delivery.ContractInstanceId,
            delivery.InvoiceAmountEur,
            delivery.Status,
            delivery.CreatedAt
        );

        await SendCreatedAtAsync<GetDeliveryById.GetDeliveryByIdEndpoint>(
            new { deliveryId = delivery.DeliveryId },
            response,
            generateAbsoluteUrl: false,
            cancellation: ct
        );
    }
}
```

#### `src/Backend/Tradebook.Api/Features/PhysicalDeliveries/GetDeliveryHistory/GetDeliveryHistoryEndpoint.cs`
```csharp
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Tradebook.Core.DTOs;
using Tradebook.Core.Infrastructure.Data;

namespace Tradebook.Api.Features.PhysicalDeliveries.GetDeliveryHistory;

public sealed class GetDeliveryHistoryEndpoint : Endpoint<GetDeliveryHistoryRequest, GetDeliveryHistoryResponse>
{
    private readonly IDeliveryRepository _deliveryRepository;

    public GetDeliveryHistoryEndpoint(IDeliveryRepository deliveryRepository)
    {
        _deliveryRepository = deliveryRepository;
    }

    public override void Configure()
    {
        Get("/api/v1/deliveries");
        AllowAnonymous();
        Description(b => b
            .Produces<GetDeliveryHistoryResponse>(200)
            .ProducesProblem(400));
    }

    public override async Task HandleAsync(GetDeliveryHistoryRequest req, CancellationToken ct)
    {
        var result = await _deliveryRepository.GetHistoryAsync(req, ct);
        await SendOkAsync(result, ct);
    }
}
```

#### `src/Backend/Tradebook.Api/Features/PhysicalDeliveries/GetDeliveryById/GetDeliveryByIdEndpoint.cs`
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Tradebook.Core.DTOs;
using Tradebook.Core.Infrastructure.Data;
using Tradebook.Core.Infrastructure.Caching;

namespace Tradebook.Api.Features.PhysicalDeliveries.GetDeliveryById;

public record GetDeliveryByIdRouteRequest(Guid DeliveryId);

public sealed class GetDeliveryByIdEndpoint : Endpoint<GetDeliveryByIdRouteRequest, PhysicalDeliveryDetailsDto>
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ICacheService _cacheService;

    public GetDeliveryByIdEndpoint(IDeliveryRepository deliveryRepository, ICacheService cacheService)
    {
        _deliveryRepository = deliveryRepository;
        _cacheService = cacheService;
    }

    public override void Configure()
    {
        Get("/api/v1/deliveries/{deliveryId}");
        AllowAnonymous();
        Description(b => b
            .Produces<PhysicalDeliveryDetailsDto>(200)
            .ProducesProblem(404));
    }

    public override async Task HandleAsync(GetDeliveryByIdRouteRequest req, CancellationToken ct)
    {
        string cacheKey = $"delivery:{req.DeliveryId}";

        var delivery = await _cacheService.GetOrCreateAsync<PhysicalDeliveryDetailsDto?>(
            cacheKey,
            async token => await _deliveryRepository.GetByIdAsync(req.DeliveryId, token),
            TimeSpan.FromMinutes(5),
            ct
        );

        if (delivery is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendOkAsync(delivery, ct);
    }
}
```

#### `src/Backend/Tradebook.Api/Features/PhysicalDeliveries/UpdatePhysicalDelivery/UpdatePhysicalDeliveryEndpoint.cs`
```csharp
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Tradebook.Core.DTOs;
using Tradebook.Core.Infrastructure.Data;
using Tradebook.Core.Infrastructure.Caching;

namespace Tradebook.Api.Features.PhysicalDeliveries.UpdatePhysicalDelivery;

public sealed class UpdatePhysicalDeliveryEndpoint : Endpoint<UpdatePhysicalDeliveryRequest, PhysicalDeliveryDetailsDto>
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ICacheService _cacheService;

    public UpdatePhysicalDeliveryEndpoint(IDeliveryRepository deliveryRepository, ICacheService cacheService)
    {
        _deliveryRepository = deliveryRepository;
        _cacheService = cacheService;
    }

    public override void Configure()
    {
        Put("/api/v1/deliveries/{deliveryId}");
        AllowAnonymous();
        Description(b => b
            .Produces<PhysicalDeliveryDetailsDto>(200)
            .ProducesProblem(404)
            .ProducesProblem(409));
    }

    public override async Task HandleAsync(UpdatePhysicalDeliveryRequest req, CancellationToken ct)
    {
        var actorIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var actorId = Guid.TryParse(actorIdClaim, out var parsed) ? parsed : Guid.Empty;

        var updated = await _deliveryRepository.UpdateAtomicAsync(req, actorId, ct);
        if (updated is null)
        {
            await SendAsync(new ProblemDetailsResponse(
                "optimistic-concurrency",
                "Stale version",
                409,
                "The delivery was modified by another actor.",
                "",
                new Dictionary<string, string[]>()
            ), 409, ct); // Optimistic Concurrency Conflict
            return;
        }

        // Invalidate caches
        await _cacheService.RemoveAsync($"delivery:{req.DeliveryId}", ct);
        await _cacheService.RemoveAsync($"deliveries:list", ct);
        await SendOkAsync(updated, ct);
    }
}
```

#### `src/Backend/Tradebook.Api/Features/PhysicalDeliveries/DeletePhysicalDelivery/DeletePhysicalDeliveryEndpoint.cs`
```csharp
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Tradebook.Core.DTOs;
using Tradebook.Core.Infrastructure.Data;
using Tradebook.Core.Infrastructure.Caching;

namespace Tradebook.Api.Features.PhysicalDeliveries.DeletePhysicalDelivery;

public sealed class DeletePhysicalDeliveryEndpoint : Endpoint<DeletePhysicalDeliveryRequest>
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ICacheService _cacheService;

    public DeletePhysicalDeliveryEndpoint(IDeliveryRepository deliveryRepository, ICacheService cacheService)
    {
        _deliveryRepository = deliveryRepository;
        _cacheService = cacheService;
    }

    public override void Configure()
    {
        Delete("/api/v1/deliveries/{deliveryId}");
        AllowAnonymous();
        Description(b => b
            .Produces(204)
            .ProducesProblem(404));
    }

    public override async Task HandleAsync(DeletePhysicalDeliveryRequest req, CancellationToken ct)
    {
        bool deleted = await _deliveryRepository.DeleteAtomicAsync(req, ct);
        if (!deleted)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        await _cacheService.RemoveAsync($"delivery:{req.DeliveryId}", ct);
        await _cacheService.RemoveAsync($"deliveries:list", ct);
        await SendNoContentAsync(ct);
    }
}
```

---

### 3.6 System.Text.Json Source Generator for Native AOT

#### `src/Backend/Tradebook.Api/AppJsonSerializerContext.cs`
```csharp
using System.Text.Json.Serialization;
using Tradebook.Core.DTOs;

namespace Tradebook.Api;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(CreatePhysicalDeliveryRequest))]
[JsonSerializable(typeof(CreatePhysicalDeliveryResponse))]
[JsonSerializable(typeof(PhysicalDeliveryDetailsDto))]
[JsonSerializable(typeof(GetDeliveryHistoryRequest))]
[JsonSerializable(typeof(GetDeliveryHistoryResponse))]
[JsonSerializable(typeof(UpdatePhysicalDeliveryRequest))]
[JsonSerializable(typeof(DeletePhysicalDeliveryRequest))]
[JsonSerializable(typeof(ProblemDetailsResponse))]
public partial class AppJsonSerializerContext : JsonSerializerContext;
```

---

### 3.7 Program.cs Main Entry Point

#### `src/Backend/Tradebook.Api/Program.cs`
```csharp
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tradebook.Api;
using Tradebook.Core.Infrastructure.Caching;
using Tradebook.Core.Infrastructure.Data;
using Tradebook.Core.Infrastructure.Options;

var builder = WebApplication.CreateSlimBuilder(args);

// Configure System.Text.Json source generation for Native AOT
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

// Configure Options with validation on start
builder.Services
    .AddOptions<DatabaseOptions>()
    .BindConfiguration("Database")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Register Infrastructure Services
builder.Services.AddSingleton<INpgsqlConnectionFactory, NpgsqlConnectionFactory>();
builder.Services.AddScoped<IDeliveryRepository, DeliveryRepository>();

// Register Native AOT HybridCache
#pragma warning disable EXTEXP0018
builder.Services.AddHybridCache();
#pragma warning restore EXTEXP0018
builder.Services.AddSingleton<ICacheService, HybridCacheService>();

// Register FastEndpoints
builder.Services.AddFastEndpoints();

var app = builder.Build();

app.UseFastEndpoints(c =>
{
    c.Serializer.Options.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

app.Run();
```

---

## 4. Step-by-Step Implementation Guide & Subagent Workflow

To execute Task 02 without errors or contract drift, subagents must perform the steps below in order.

### Step 1: Solution & Project Creation
1. Navigate to `src/Backend/`.
2. Run `dotnet new sln -n Tradebook`.
3. Create projects:
   - `dotnet new webapi -n Tradebook.Api --no-openapi`
   - `dotnet new classlib -n Tradebook.Core`
   - `dotnet new xunit -n Tradebook.Tests`
4. Add project references:
   - `dotnet sln Tradebook.sln add Tradebook.Api/Tradebook.Api.csproj Tradebook.Core/Tradebook.Core.csproj Tradebook.Tests/Tradebook.Tests.csproj`
   - `dotnet add Tradebook.Api/Tradebook.Api.csproj reference Tradebook.Core/Tradebook.Core.csproj`
   - `dotnet add Tradebook.Tests/Tradebook.Tests.csproj reference Tradebook.Api/Tradebook.Api.csproj Tradebook.Core/Tradebook.Core.csproj`
5. Edit `.csproj` files to configure Native AOT properties (`<PublishAot>true</PublishAot>`).

### Step 2: DTOs & TypeGen Annotations
1. Create `src/Backend/Tradebook.Core/DTOs/DeliveryDtos.cs`, `ContractDtos.cs`, and `CommonDtos.cs` mirroring `architecture/entity-model.md` column names.
2. Annotate all records with `[ExportTsInterface]`.
3. Verify that `TypeGen` annotations compile properly.

### Step 3: Data Access Layer & Dapper Repositories
1. Implement `NpgsqlConnectionFactory` in `Tradebook.Core/Infrastructure/Data/`.
2. Write `DeliveryRepository` implementing atomic transaction handling for `CreateAtomicAsync`, `UpdateAtomicAsync`, `DeleteAtomicAsync`, `GetByIdAsync`, and `GetHistoryAsync`.
3. Ensure all raw SQL statements target PostgreSQL 17 entity tables created in Task 01 (`physical_deliveries`, `contracts`, `counterparties`, `audit_log`, `outbox_events`).

### Step 4: HybridCache & Options Setup
1. Create `DatabaseOptions.cs` with DataAnnotation validations (`[Required]`).
2. Implement `HybridCacheService.cs` wrapping `.NET 9 HybridCache`.
3. Configure DI in `Program.cs`.

### Step 5: FastEndpoints Vertical Slices
1. Implement `Features/PhysicalDeliveries/CreatePhysicalDelivery/CreatePhysicalDeliveryEndpoint.cs` and `CreatePhysicalDeliveryValidator.cs`.
2. Implement `Features/PhysicalDeliveries/GetDeliveryHistory/GetDeliveryHistoryEndpoint.cs`.
3. Implement `Features/PhysicalDeliveries/GetDeliveryById/GetDeliveryByIdEndpoint.cs`.
4. Implement `Features/PhysicalDeliveries/UpdatePhysicalDelivery/UpdatePhysicalDeliveryEndpoint.cs`.
5. Implement `Features/PhysicalDeliveries/DeletePhysicalDelivery/DeletePhysicalDeliveryEndpoint.cs`.
6. Replicate the identical pattern for the remaining slices: `Contracts`, `CapacityBookings`, `Transfers`, `Biotickets`, `GoOCertificates`, `MarketPrices`, `TaxTariffs`.

### Step 6: Native AOT JsonSerializerContext Wireup
1. Create `AppJsonSerializerContext.cs`.
2. Include all Request/Response DTO types in `[JsonSerializable]` attributes.
3. Hook `AppJsonSerializerContext.Default` into `ConfigureHttpJsonOptions` and `UseFastEndpoints` in `Program.cs`.

### Step 7: Build & Test Execution
1. Run `dotnet build src/Backend/Tradebook.sln -c Release`.
2. Execute unit, integration, and architecture tests.

---

## 5. API Endpoints Specification & Contracts

| HTTP Method | Route Path | Request DTO | Response DTO | Status Codes | Description |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **POST** | `/api/v1/deliveries` | `CreatePhysicalDeliveryRequest` | `CreatePhysicalDeliveryResponse` | 201, 400, 500 | Creates delivery record; `app.actor_id` set so the bi-temporal audit trigger writes `audit_log`; outbox event enqueued atomically. |
| **GET** | `/api/v1/deliveries` | `GetDeliveryHistoryRequest` | `GetDeliveryHistoryResponse` | 200, 400 | Paginated Dapper history query with contract, instance, book type, status, and month filters. |
| **GET** | `/api/v1/deliveries/{deliveryId}` | Route Params | `PhysicalDeliveryDetailsDto` | 200, 404 | Fast `HybridCache` delivery lookup (<0.5ms on hit). |
| **PUT** | `/api/v1/deliveries/{deliveryId}` | `UpdatePhysicalDeliveryRequest` | `PhysicalDeliveryDetailsDto` | 200, 404, 409 | Atomic delivery update (volume/status) with `xmin` optimistic concurrency verification. |
| **DELETE** | `/api/v1/deliveries/{deliveryId}` | `DeletePhysicalDeliveryRequest` | None (204) | 204, 404 | Bi-temporal deletion and audit logging. |

> Additional slices follow the identical contract shapes: `POST/GET /api/v1/contracts`, `POST/GET /api/v1/capacity-bookings`, `POST/GET /api/v1/transfers`, `POST/GET /api/v1/biotickets`, `GET /api/v1/goo-certificates`, `GET/PUT /api/v1/market-prices`, `GET/PUT /api/v1/tax-tariffs`.

---

## 6. Comprehensive Test Plan & Test Suite Architecture (`Tradebook.Tests`)

### 6.1 Unit Tests (`src/Backend/Tradebook.Tests/Unit/CreatePhysicalDeliveryValidatorTests.cs`)
```csharp
using FastEndpoints;
using FluentAssertions;
using Tradebook.Api.Features.PhysicalDeliveries.CreatePhysicalDelivery;
using Tradebook.Core.DTOs;
using Xunit;

namespace Tradebook.Tests.Unit;

public class CreatePhysicalDeliveryValidatorTests
{
    private readonly CreatePhysicalDeliveryValidator _validator = new();

    [Fact]
    public void Validate_WithValidRequest_ShouldNotHaveErrors()
    {
        var req = new CreatePhysicalDeliveryRequest(
            ContractId: Guid.NewGuid(),
            ContractInstanceId: "BFEX45.BT.2301.CO2E-9-2023",
            BookType: "Sales",
            SupplyMonth: new DateTime(2023, 9, 1),
            CapacityMw: null,
            VolumeNominatedMwh: 12000m,
            VolumeRealisedMwh: 11800m,
            PriceEurMwh: 34.50m,
            PriceMechanism: "TTF",
            StartDay: new DateTime(2023, 9, 1),
            EndDay: new DateTime(2023, 9, 30),
            CustomFieldsJson: null
        );

        var result = _validator.Validate(req);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Sales", "2023-09-01")]
    [InlineData("BFEX45.BT.2301.CO2E-9-2023", "OTC", "2023-09-01")]
    [InlineData("BFEX45.BT.2301.CO2E-9-2023", "Sales", "")]
    public void Validate_WithInvalidParameters_ShouldFailValidation(string instanceId, string bookType, string supplyMonth)
    {
        var req = new CreatePhysicalDeliveryRequest(
            ContractId: Guid.NewGuid(),
            ContractInstanceId: instanceId,
            BookType: bookType,
            SupplyMonth: DateTime.TryParse(supplyMonth, out var month) ? month : default,
            CapacityMw: null,
            VolumeNominatedMwh: null,
            VolumeRealisedMwh: null,
            PriceEurMwh: null,
            PriceMechanism: null,
            StartDay: null,
            EndDay: null,
            CustomFieldsJson: null
        );

        var result = _validator.Validate(req);
        result.IsValid.Should().BeFalse();
    }
}
```

---

### 6.2 Architecture Boundary Tests (`src/Backend/Tradebook.Tests/Architecture/SliceBoundaryTests.cs`)
```csharp
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.Fluent;
using Xunit;

namespace Tradebook.Tests.Architecture;

public class SliceBoundaryTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture =
        new ArchLoader().LoadAssemblies(typeof(Api.AppJsonSerializerContext).Assembly).Build();

    [Fact]
    public void CreatePhysicalDeliveryFeature_MustNotDependOn_UpdatePhysicalDeliveryFeature()
    {
        ArchRuleDefinition.Types()
            .That().ResideInNamespace("Tradebook.Api.Features.PhysicalDeliveries.CreatePhysicalDelivery")
            .Should().NotDependOnAny(
                ArchRuleDefinition.Types().That().ResideInNamespace("Tradebook.Api.Features.PhysicalDeliveries.UpdatePhysicalDelivery")
            )
            .Check(Architecture);
    }

    [Fact]
    public void DeliverySlice_MustNotDependOn_MarketPriceSlice()
    {
        ArchRuleDefinition.Types()
            .That().ResideInNamespace("Tradebook.Api.Features.PhysicalDeliveries")
            .Should().NotDependOnAny(
                ArchRuleDefinition.Types().That().ResideInNamespace("Tradebook.Api.Features.MarketPrices")
            )
            .Check(Architecture);
    }
}
```

---

## 7. Independent Verification & Agent Acceptance Steps

### 7.1 Verification Commands
Execute the following verification sequence from the project root:

```bash
# 1. Build backend solution with Native AOT Release configuration
dotnet build src/Backend/Tradebook.sln -c Release

# 2. Run unit and architecture tests
dotnet test tests/Tradebook.Tests/Tradebook.Tests.csproj -c Release

# 3. Publish Native AOT binary and verify publication output
dotnet publish src/Backend/Tradebook.Api/Tradebook.Api.csproj -c Release -r linux-x64 --self-contained

# 4. Perform RAM footprint & startup latency check
# Launch compiled binary in background and test health response latency
./src/Backend/Tradebook.Api/bin/Release/net9.0/linux-x64/publish/Tradebook.Api &
PID=$!
sleep 0.1
curl -s -o /dev/null -w "%{time_total}\n" http://localhost:5000/api/v1/deliveries
kill $PID
```

### 7.2 Acceptance Criteria Checklist
- [ ] `dotnet build src/Backend/Tradebook.sln` completes with zero warnings and zero errors.
- [ ] Native AOT publish (`<PublishAot>true</PublishAot>`) succeeds without reflection warnings or missing metadata errors.
- [ ] Baseline RAM footprint of published binary is **<30MB**.
- [ ] Cold start HTTP response latency is **<5ms**.
- [ ] `CreatePhysicalDeliveryEndpoint` executes an atomic PostgreSQL transaction inserting into `physical_deliveries`, writing `audit_log` via the generic bi-temporal trigger (`app.actor_id` session setting), and inserting into `outbox_events` simultaneously.
- [ ] `GetDeliveryByIdEndpoint` uses `HybridCache` to return cached records in **<0.5ms**.
- [ ] All DTO records feature TypeGen `[ExportTsInterface]` annotations.
- [ ] `ArchUnitNET` slice boundary tests pass green.
- [ ] Contract instance IDs are generated with `fn_generate_contract_instance` (or the equivalent domain service) and satisfy the `{ContractName}-{DeliveryMonthNo}-{Year}` format.

---

## 8. Anti-Cheating & Integrity Guardrails

1. **No Fake / In-Memory Repositories**: All Dapper queries in `DeliveryRepository.cs` must execute actual SQL statements over `NpgsqlDataSource` against PostgreSQL 17. Creating dummy in-memory list repositories for production endpoints is strictly prohibited.
2. **No Hardcoded Test Responses**: Endpoints must not return hardcoded sample JSON payloads. Every response must be generated from real database results or calculated domain values.
3. **No Dynamic Reflection Serialization**: The API must use `AppJsonSerializerContext` source generation. No falling back to reflection-based `JsonSerializer` calls at runtime.
4. **Mandatory Atomic Transactions**: `CreateAtomicAsync`, `UpdateAtomicAsync`, and `DeleteAtomicAsync` must execute within explicit PostgreSQL `NpgsqlTransaction` blocks encapsulating entity mutations, audit logging (via trigger), and outbox event persistence.
5. **Entity-Model Lock**: SQL columns must exactly match `architecture/entity-model.md` v2.0 / blueprint §3 DDL. Inventing `trades`-style tables (`symbol`, `asset_class`, `side`, `portfolio_accounts`, `market_venues`) is a hard failure.

---
*End of Task 02 Specification.*
