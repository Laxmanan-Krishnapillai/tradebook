# Task 15: Strongly-Typed Domain Primitives (Vogen)

> **GREENFIELD MODERNIZATION TASK (2026-08-07)** — Adopt [Vogen](https://github.com/SteveDunn/Vogen) value objects for **every** entity identifier and **every** money/quantity field across the entire Tradebook domain, replacing raw `Guid`/`string` IDs and bare `decimal` amounts with compiler-enforced primitives. This is a committed, repo-wide adoption applied to every aggregate — not a subset. See `docs/architecture/entity-model.md` for the authoritative entity inventory and `docs/architecture/decision-log.md` (ADR: "Domain primitives via Vogen") for the recorded decision and its consequences.

- **Phase**: 3 — Domain Hardening
- **Lead / Owner**: Backend Platform Guild
- **Complexity**: High
- **Prerequisites**: Task 13 (domain model consolidation) — coordinates with Task 14 (analyzers) and Task 16 (contract mapping)
- **Status**: Specified
- **Target Files**:
  - Value-object definitions under `src/Backend/src/Tradebook.Core/Domain/ValueObjects/`
  - Dapper type-handler registration in `src/Backend/src/Tradebook.Infrastructure/Data/VogenTypeHandlers.cs`
  - `src/Backend/src/Tradebook.Api/Serialization/AppJsonSerializerContext.cs`
  - MessagePack formatters in `src/Backend/src/Tradebook.Api/Realtime/VogenMessagePackResolver.cs`
  - Unit + integration tests under `src/Backend/tests/`

---

## 1. Detailed Scope & Feature Coverage

### 1.1 Problem Statement

The domain currently models identity as raw `Guid`/`string` and money as bare `decimal`. Nothing stops a caller passing a `CounterpartyId` where a `TradingPointId` is expected, adding a `Price` to a `Quantity`, or persisting a price at 8-decimal scale when the column is `NUMERIC(18,4)`. These are silent, high-cost trading defects: mis-booked deliveries, mis-priced hedges, and rounding drift that only surfaces at settlement. The type system is not carrying any of the invariants the business depends on, so the compiler cannot reject the mistakes.

### 1.2 Required Outcomes

- Every entity identifier is a distinct Vogen value object wrapping `Guid`; `ContractId` and `CounterpartyId` are not assignment-compatible.
- Every money and quantity field is a validated value object: `Price` (scale 4, non-negative), `Quantity` (scale 8), and `Amount` for all other monetary fields (scale 4).
- No raw `Guid`, `string`, or bare `decimal` identifier/money member remains public on any Domain type or DTO.
- Each value object crosses every boundary (SQL, JSON, SignalR) losslessly through registered interop, and surfaces to contracts (Task 16) as its underlying primitive.

### 1.3 In Scope

- Defining and validating value objects for all fourteen aggregates listed in `docs/architecture/entity-model.md`.
- Rewriting Domain entity and DTO members to use the value objects.
- Wiring Dapper, System.Text.Json source-gen, and MessagePack interop for every value object.
- Unit, JSON round-trip, and Testcontainers integration tests, plus an ArchUnitNET conformance rule.

### 1.4 Out of Scope

- Changing SQL column types, indexes, or the entity model (identifiers stay `uuid`; money stays `NUMERIC(18,4)` / quantity `NUMERIC(18,8)`).
- Analyzer packaging and enforcement rules (Task 14).
- OpenAPI/TypeSpec contract generation and the generated TS client (Task 16).
- Value objects embedded inside integration/queue messages (Task 17).

## 2. Key Deliverables & File Layout

```text
src/Backend/src/
├── Tradebook.Core/
│   └── Domain/
│       └── ValueObjects/
│           ├── Ids/
│           │   ├── ContractId.cs
│           │   ├── DeliveryId.cs
│           │   ├── CapacityBookingId.cs
│           │   ├── TransferId.cs
│           │   ├── BioticketDeliveryId.cs
│           │   ├── CounterpartyId.cs
│           │   ├── CompanyId.cs
│           │   ├── TradingPointId.cs
│           │   ├── TaxTariffId.cs
│           │   ├── HedgeId.cs
│           │   ├── MarketPriceId.cs
│           │   ├── CapacityPriceIndexId.cs
│           │   ├── GooCertificateTransactionId.cs
│           │   └── InvoiceLineItemId.cs
│           ├── Money/
│           │   ├── Price.cs
│           │   ├── Quantity.cs
│           │   └── Amount.cs
│           └── VogenDefaults.cs          # [assembly: VogenDefaults(...)]
├── Tradebook.Infrastructure/
│   └── Data/
│       └── VogenTypeHandlers.cs          # SqlMapper.AddTypeHandler registration
└── Tradebook.Api/
    ├── Serialization/
    │   └── AppJsonSerializerContext.cs   # + new VogenTypesFactory()
    ├── Realtime/
    │   └── VogenMessagePackResolver.cs   # hub-payload formatters
    └── Program.cs                        # RegisterAll() + STJ + MessagePack wiring
src/Backend/tests/
├── Tradebook.Core.UnitTests/ValueObjects/
│   ├── PriceTests.cs
│   ├── QuantityTests.cs
│   └── ValueObjectJsonRoundTripTests.cs
└── Tradebook.Infrastructure.IntegrationTests/
    ├── VogenDapperRoundTripTests.cs      # Testcontainers PG17
    └── DomainSurfaceArchRuleTests.cs     # ArchUnitNET
```

**Value-object inventory** — every aggregate is covered:

| Value Object | Underlying | Applies To (entity → field) | Validation |
|---|---|---|---|
| `ContractId` | `Guid` | `contracts.id` and all FK references | non-empty Guid |
| `DeliveryId` | `Guid` | `physical_deliveries.id` | non-empty Guid |
| `CapacityBookingId` | `Guid` | `capacity_bookings.id` | non-empty Guid |
| `TransferId` | `Guid` | `transfers.id` | non-empty Guid |
| `BioticketDeliveryId` | `Guid` | `bioticket_deliveries.id` | non-empty Guid |
| `CounterpartyId` | `Guid` | `counterparties.id` | non-empty Guid |
| `CompanyId` | `Guid` | `companies.id` | non-empty Guid |
| `TradingPointId` | `Guid` | `trading_points.id` | non-empty Guid |
| `TaxTariffId` | `Guid` | `tax_tariffs.id` | non-empty Guid |
| `HedgeId` | `Guid` | `hedges.id` | non-empty Guid |
| `MarketPriceId` | `Guid` | `market_prices.id` | non-empty Guid |
| `CapacityPriceIndexId` | `Guid` | `capacity_price_indexes.id` | non-empty Guid |
| `GooCertificateTransactionId` | `Guid` | `goo_certificate_transactions.id` | non-empty Guid |
| `InvoiceLineItemId` | `Guid` | `invoice_line_items.id` | non-empty Guid |
| `Price` | `decimal` | `market_prices.price`, `contracts.unit_price`, `capacity_price_indexes.value` | `>= 0`, scale ≤ 4 |
| `Quantity` | `decimal` | delivery/booking/transfer volumes, `goo_certificate_transactions.volume` | scale ≤ 8 |
| `Amount` | `decimal` | `invoice_line_items.*`, `tax_tariffs.rate`, `hedges.notional` | scale ≤ 4 |

## 3. Architecture & Code Contract Blueprints

Set repo-wide Vogen defaults once so every value object inherits identical interop; no type hand-rolls its converters:

```csharp
// Domain/ValueObjects/VogenDefaults.cs
[assembly: VogenDefaults(
    conversions: Conversions.SystemTextJson | Conversions.DapperTypeHandler,
    throws: typeof(Tradebook.Core.Domain.TradebookDomainException))]
```

**Identifier value object** — every ID follows this shape:

```csharp
using Vogen;

namespace Tradebook.Core.Domain.ValueObjects.Ids;

[ValueObject<Guid>]
public readonly partial struct ContractId
{
    private static Validation Validate(Guid input) =>
        input == Guid.Empty ? Validation.Invalid("ContractId must not be empty.") : Validation.Ok;

    public static ContractId New() => From(Guid.CreateVersion7());
}
```

**Money & quantity** — `Validate()` enforces sign and scale; there is no `NormalizeInput` that would silently round away a scale violation:

```csharp
[ValueObject<decimal>]
public readonly partial struct Price
{
    private static Validation Validate(decimal input)
    {
        if (input < 0m) return Validation.Invalid("Price must be non-negative.");
        if (decimal.Round(input, 4, MidpointRounding.ToEven) != input)
            return Validation.Invalid("Price scale must not exceed 4 decimal places.");
        return Validation.Ok;
    }
}

[ValueObject<decimal>]
public readonly partial struct Quantity
{
    private static Validation Validate(decimal input) =>
        decimal.Round(input, 8, MidpointRounding.ToEven) != input
            ? Validation.Invalid("Quantity scale must not exceed 8 decimal places.")
            : Validation.Ok;
}
```

**Dapper interop** — Vogen emits a nested `DapperTypeHandler` per type; register every one at startup, before the first query executes:

```csharp
// Tradebook.Infrastructure/Data/VogenTypeHandlers.cs
public static class VogenTypeHandlers
{
    private static bool _registered;
    public static void RegisterAll()
    {
        if (_registered) return;
        SqlMapper.AddTypeHandler(new ContractId.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new CounterpartyId.DapperTypeHandler());
        // … one line per identifier value object …
        SqlMapper.AddTypeHandler(new Price.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new Quantity.DapperTypeHandler());
        SqlMapper.AddTypeHandler(new Amount.DapperTypeHandler());
        _registered = true;
    }
}
```

**System.Text.Json source-gen** — register Vogen's factory in the source-gen `JsonSerializerOptions` used by `AppJsonSerializerContext`:

```csharp
// Program.cs
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new VogenTypesFactory());
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});
VogenTypeHandlers.RegisterAll();
```

**SignalR + MessagePack** — provide a resolver that serializes the underlying primitive for every hub-payload value object:

```csharp
public sealed class PriceFormatter : IMessagePackFormatter<Price>
{
    public void Serialize(ref MessagePackWriter w, Price v, MessagePackSerializerOptions o) => w.Write(v.Value);
    public Price Deserialize(ref MessagePackReader r, MessagePackSerializerOptions o) => Price.From(r.ReadDecimal());
}

builder.Services.AddSignalR().AddMessagePackProtocol(o =>
    o.SerializerOptions = MessagePackSerializerOptions.Standard
        .WithResolver(CompositeResolver.Create(VogenMessagePackResolver.Instance, StandardResolver.Instance)));
```

**Task 16 contract mapping (coordination note):** each value object surfaces to OpenAPI/TS as its underlying primitive — `Guid → string`, `Price → decimal-as-string`, `Quantity → decimal-as-string`, `Amount → decimal-as-string`. The mapping is authored in Task 16's TypeSpec layer; this task guarantees the JSON wire shape is exactly the primitive so no client change is needed.

## 4. Subagent Implementation Step-by-Step Workflow

1. Add the `Vogen` 8.x package reference to `Tradebook.Core` and author `VogenDefaults.cs`.
2. Define all fourteen identifier value objects under `Domain/ValueObjects/Ids/`.
3. Define `Price`, `Quantity`, and `Amount` under `Domain/ValueObjects/Money/` with `Validate()`.
4. Replace every raw `Guid`/`string` ID and bare `decimal` money member on Domain entities and DTOs with the corresponding value object.
5. Add `VogenTypeHandlers.RegisterAll()` and call it from `Program.cs` before any Dapper query runs; audit `Tradebook.Infrastructure/Data/` SQL so parameters bind the value object directly.
6. Add `new VogenTypesFactory()` to the STJ options and confirm `AppJsonSerializerContext` still compiles under source generation.
7. Implement `VogenMessagePackResolver` formatters for every value object appearing on a SignalR hub payload and register it.
8. Coordinate the primitive mapping with Task 16; record the wire contract in the ADR.
9. Write unit tests (validation + JSON round-trip), the Testcontainers PG17 Dapper round-trip test, and the ArchUnitNET Domain-surface rule.
10. Run the verification suite in section 5.1 and close out the acceptance IDs in 5.2.

## 5. Independent Verification & Acceptance Workflow

### 5.1 Commands

```bash
dotnet restore src/Backend/Tradebook.sln
dotnet build   src/Backend/Tradebook.sln -warnaserror
dotnet test    src/Backend/tests/Tradebook.Core.UnitTests
dotnet test    src/Backend/tests/Tradebook.Infrastructure.IntegrationTests   # Testcontainers PG17
# Guardrail: no raw Guid/decimal identifier or money member escapes the Domain public surface
rg -n "public\s+(Guid|decimal)\s+\w*(Id|Price|Quantity|Amount|Total|Notional|Rate)\b" \
   src/Backend/src/Tradebook.Core/Domain && exit 1 || true
```

### 5.2 Acceptance Criteria

| ID | Criterion | Verification |
|---|---|---|
| TYPE-01 | Solution builds clean with `-warnaserror` | `dotnet build` |
| TYPE-02 | Every entity ID is a distinct Vogen VO wrapping `Guid`; no raw `Guid`/`string` IDs in Domain/DTOs | build + `rg` guardrail |
| TYPE-03 | `Price`/`Quantity`/`Amount` reject bad sign and out-of-range scale | `PriceTests`, `QuantityTests` |
| TYPE-04 | Every VO has a JSON round-trip test under the source-gen context | `ValueObjectJsonRoundTripTests` |
| TYPE-05 | Every VO has a registered Dapper handler in `VogenTypeHandlers.RegisterAll()` | code review + build |
| TYPE-06 | Integration test reads/writes VOs through Dapper against Testcontainers PG17 | `VogenDapperRoundTripTests` |
| TYPE-07 | ArchUnitNET rule asserts Domain exposes no public raw `Guid`/`decimal` identifier/money members | `DomainSurfaceArchRuleTests` |
| TYPE-08 | `VogenTypesFactory` is registered in the STJ options | code review + round-trip test |
| TYPE-09 | MessagePack formatters cover every hub-payload VO | resolver review + hub test |
| TYPE-10 | Each VO surfaces to contracts as its underlying primitive | Task 16 mapping check |

## 6. Anti-Cheating & Integrity Guardrails

1. No raw `Guid`/`string` IDs or bare `decimal` money members exist anywhere in Domain or DTOs — enforced by the ArchUnitNET rule and the `rg` guardrail.
2. Every value object MUST have validation, a registered Dapper handler, an STJ converter, and a MessagePack formatter before it is used across any boundary.
3. Never bypass a value object in SQL parameter binding — bind the value object itself and let its Dapper handler unwrap the primitive; do not hand-unwrap to `.Value` at the call site.
4. Do not weaken `Price` or `Quantity` validation (sign or scale) to make a test pass; fix the caller or the test data instead.
5. Do not change SQL column types or the entity model; the underlying storage is unchanged and out of scope.
6. All value objects inherit interop from `[assembly: VogenDefaults]`; do not hand-roll per-type converters or diverge from the shared configuration.
