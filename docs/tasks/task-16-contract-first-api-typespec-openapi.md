# Task 16: Contract-First API — TypeSpec, OpenAPI & Generated TS Client with Runtime Validation

> **GREENFIELD MODERNIZATION TASK (2026-08-07)** — This task supersedes the TypeGen approach from Task 08. TypeSpec becomes the single contract source of truth for every endpoint and DTO; from it the frontend receives generated TypeScript types, generated runtime validators, and generated TanStack Query hooks in one pipeline. The types-only TypeGen path is retired outright — no dual pipeline survives. Record the direction and its rationale in `docs/architecture/decision-log.md`.

**Phase**: 3 — API Contract & Frontend Type Safety
**Lead / Owner**: Platform Engineering (API contract) with the Frontend Guild (generated data layer)
**Complexity**: Very High
**Prerequisites**: Task 13 (complete). Coordinates with Task 15 (value-object → primitive mapping) and Task 17 (event DTOs); supersedes Task 08 (TypeGen); relates to Task 11 (TanStack Query v5 + Router).
**Status**: Specified
**Target Files**:
- `docs/api/typespec/main.tsp`, `docs/api/typespec/models/*.tsp` — authored contract (new)
- `docs/api/typespec/tspconfig.yaml` — OpenAPI 3.1 emitter config (new)
- `scripts/check-contract-drift.sh` — CI zero-drift gate (new)
- `src/Frontend/src/api/generated/` — generated types + Zod v4 + hooks (regenerated, never hand-edited)
- `src/Frontend/openapi-ts.config.ts` — Hey API codegen config (new)
- `src/Frontend/package.json` — codegen scripts + dependencies
- `src/Frontend/src/hooks/useRealtimeQuerySync.ts` — Zod-validated SignalR payload parsing
- `src/Backend/Tradebook.Api/Serialization/MoneyJsonConverter.cs` — money-as-string STJ converter (new)
- `tgconfig.json` — **deleted**
- `Directory.Build.targets` — remove the `GenerateTypeScriptContracts` MSBuild target
- `TypeGen` NuGet package — **removed** from the solution

---

## 1. Scope

Establish one contract that produces strong types, runtime validation, and a typed data layer across the entire application, and delete the legacy code generator so a single pipeline remains.

### 1.1 TypeSpec is the single source of truth

Author every endpoint and DTO in `docs/api/typespec/`. Define a `Money` decimal scalar (a string carrying `format: decimal`), model all enums as string-valued enums, model RFC 9457 Problem Details, and carry the integer `version` optimistic-concurrency field on every mutable aggregate. Emit **OpenAPI 3.1** through `@typespec/openapi3` (GA). The TypeSpec C# server emitter is alpha and is **not** used: TypeSpec **governs** the contract, FastEndpoints **implements** it, and a CI gate proves the two agree. This split is committed for the whole API surface, not a subset.

### 1.2 Remove TypeGen entirely

Delete `tgconfig.json`, remove the `GenerateTypeScriptContracts` target from `Directory.Build.targets`, and drop the `TypeGen` NuGet package. TypeGen emitted TypeScript types only — no validators, no client, no hooks — off `Tradebook.Core.dll` at build time. After this task no MSBuild target generates frontend code; codegen is contract-driven and lives on the frontend side.

### 1.3 Generated type-safe data layer

Generate the client with **Hey API (`@hey-api/openapi-ts`)** into `src/Frontend/src/api/generated` (Kubb is the accepted equivalent). One run produces TypeScript types, **Zod v4** schemas (`validator: true`), and **TanStack Query v5** `queryOptions` / `mutationOptions`. Migrate all data fetching in the React 19 app onto the generated hooks — no hand-written fetch wrappers around API endpoints remain.

### 1.4 Runtime validation, money-as-string, and zero drift

Validate every API response with its generated Zod schema inside the generated hooks, and reuse those same named schemas to `.parse()` every SignalR payload in `useRealtimeQuerySync.ts`, replacing the unchecked casts that Task 11 guardrail #8 forbids. Parse Problem Details bodies and map field errors by property path. Transport money as a string end-to-end. A CI job regenerates the client and fails on any diff, and separately fails if the TypeSpec spec and the FastEndpoints-emitted OpenAPI diverge.

## 2. Deliverables & File Layout

```text
tradebook/
├── docs/
│   ├── api/typespec/
│   │   ├── main.tsp                  # service, routes, Money scalar, Problem Details
│   │   ├── tspconfig.yaml            # @typespec/openapi3 -> OpenAPI 3.1
│   │   ├── models/
│   │   │   ├── orders.tsp            # Order + enums + version field
│   │   │   └── common.tsp            # Money, ProblemDetails, shared types
│   │   └── tsp-output/@typespec/openapi3/openapi.yaml   # emitted (git-ignored)
│   └── architecture/decision-log.md  # ADR: TypeSpec supersedes TypeGen
├── scripts/
│   └── check-contract-drift.sh       # regen + git diff + spec-vs-runtime diff
├── src/
│   ├── Backend/Tradebook.Api/
│   │   └── Serialization/MoneyJsonConverter.cs
│   └── Frontend/
│       ├── openapi-ts.config.ts
│       ├── package.json              # api:generate script + deps
│       └── src/
│           ├── api/generated/        # types.gen.ts, zod.gen.ts, sdk.gen.ts,
│           │                         #   @tanstack/react-query.gen.ts  (NEVER edit)
│           └── hooks/useRealtimeQuerySync.ts
├── tgconfig.json                     # DELETED
└── Directory.Build.targets           # GenerateTypeScriptContracts target REMOVED
```

Pipeline (contract → generated artifacts, with the CI gate closing the loop):

```text
  docs/api/typespec/*.tsp
        │  tsp compile  (@typespec/openapi3, GA)
        ▼
  tsp-output/@typespec/openapi3/openapi.yaml   (OpenAPI 3.1) ─────────┐
        │  @hey-api/openapi-ts                                        │
        ▼                                                             │  drift gate
  src/Frontend/src/api/generated/                                     │  (diff spec vs
    ├── types.gen.ts                 TypeScript types                 │   FastEndpoints
    ├── zod.gen.ts                   Zod v4 validators                │   OpenAPI 3.1 from
    ├── sdk.gen.ts                   fetch client (validator: true)   │   Microsoft.
    └── @tanstack/react-query.gen.ts queryOptions / mutationOptions   ▼   AspNetCore.OpenApi)
        │  reused by hooks + useRealtimeQuerySync.ts        FastEndpoints implementation
        ▼                                                   (governed, not generated)
  React 19 components — typed, runtime-validated data layer
```

## 3. Architecture & Code Contract Blueprints

### 3.1 TypeSpec contract

```tsp
// docs/api/typespec/main.tsp
import "@typespec/http";
import "@typespec/openapi3";

using Http;

@service(#{ title: "Tradebook API" })
@info(#{ version: "1.0.0" })
namespace Tradebook;

/** Decimal money transported as a base-10 string to preserve NUMERIC(18,8)/(18,4).
    Never a JSON number — float64 is lossy at 18 significant digits. */
@format("decimal")
scalar Money extends string;

/** Enums always serialize as strings (Program.cs registers JsonStringEnumConverter). */
enum OrderSide { Buy: "Buy", Sell: "Sell" }
enum OrderStatus { New: "New", PartiallyFilled: "PartiallyFilled", Filled: "Filled", Cancelled: "Cancelled" }

model Order {
  @key @format("uuid") id: string;
  symbol: string;
  side: OrderSide;
  status: OrderStatus;
  quantity: Money;
  price: Money;
  /** Optimistic-concurrency token; a stale write is rejected with 409. */
  version: int32;
}

/** RFC 9457 Problem Details. `errors` maps a property path to its messages. */
@error
model ProblemDetails {
  @header contentType: "application/problem+json";
  type?: url;
  title?: string;
  status?: int32;
  detail?: string;
  instance?: url;
  errors?: Record<string[]>;
}

@route("/orders") @tag("Orders")
interface Orders {
  @get list(): Order[] | ProblemDetails;
  @get read(@path @format("uuid") id: string): Order | ProblemDetails;
  @post create(@body order: Order): { @statusCode _: 201; @body created: Order } | ProblemDetails;
}
```

### 3.2 Frontend codegen config, generated hook usage, and SignalR validation

```ts
// src/Frontend/openapi-ts.config.ts
import { defineConfig } from '@hey-api/openapi-ts';

export default defineConfig({
  input: '../../docs/api/typespec/tsp-output/@typespec/openapi3/openapi.yaml',
  output: { path: './src/api/generated', format: 'prettier', lint: 'eslint' },
  plugins: [
    '@hey-api/typescript',
    '@hey-api/client-fetch',
    { name: '@hey-api/zod' },                       // Zod v4 schema per model + response
    { name: '@hey-api/sdk', validator: true },      // parse every response with its schema
    { name: '@tanstack/react-query' },              // queryOptions + mutationOptions
  ],
});
```

```ts
// Consuming a generated queryOptions — the response is Zod-validated inside the queryFn.
import { useQuery } from '@tanstack/react-query';
import { listOrdersOptions } from '@/api/generated/@tanstack/react-query.gen';

export const useOrders = () => useQuery(listOrdersOptions());
```

```ts
// src/Frontend/src/hooks/useRealtimeQuerySync.ts — reuse the SAME generated schema.
import { zOrder } from '@/api/generated/zod.gen';

connection.on('OrderUpdated', (raw: unknown) => {
  const order = zOrder.parse(raw); // throws on a malformed payload; replaces the unchecked cast
  queryClient.setQueryData(['orders', order.id], order);
});
```

### 3.3 Money-as-string System.Text.Json converter

```csharp
// src/Backend/Tradebook.Api/Serialization/MoneyJsonConverter.cs
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tradebook.Api.Serialization;

/// <summary>Reads/writes decimal money as a JSON string to preserve NUMERIC(18,8)/(18,4) precision.</summary>
public sealed class MoneyJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Money must be a decimal string, not a JSON number.");
        return decimal.Parse(reader.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}

// Program.cs — register alongside the existing JsonStringEnumConverter:
//   builder.Services.ConfigureHttpJsonOptions(o =>
//       o.SerializerOptions.Converters.Add(new MoneyJsonConverter()));
```

The OpenAPI schema renders `Money` as `type: string, format: decimal`; the frontend parses it with `decimal.js`, and the generated Zod schema is `z.string()` refined to a valid decimal. Money never crosses the wire as a number.

### 3.4 Codegen commands

```bash
# 1. Compile the contract to OpenAPI 3.1
npx tsp compile docs/api/typespec

# 2. Generate TS types + Zod v4 + TanStack Query hooks
cd src/Frontend && npm run api:generate   # wraps @hey-api/openapi-ts against openapi-ts.config.ts
```

### 3.5 Decision note

TypeGen emitted TypeScript **types only**, off a compiled assembly, with no runtime validators, no HTTP client, and no query hooks — every boundary still needed a hand-written, unchecked cast. TypeSpec plus Hey API is adopted repo-wide because one contract yields types, Zod v4 validators, a fetch client, and TanStack Query hooks together, closing the runtime-validation gap the codebase carried. Kubb is the accepted equivalent generator. The TypeSpec C# server emitter is alpha and is excluded: FastEndpoints remains the implementation, and the drift gate — not code generation — keeps the .NET side honest against the contract.

### 3.6 Reference docs

- `docs/architecture/decision-log.md` — ADR superseding Task 08 TypeGen
- TypeSpec + `@typespec/openapi3` (GA) — https://typespec.io
- Hey API `@hey-api/openapi-ts` — https://heyapi.dev
- RFC 9457 Problem Details — https://www.rfc-editor.org/rfc/rfc9457

## 4. Step-by-Step

1. Add the ADR to `docs/architecture/decision-log.md` recording that TypeSpec supersedes TypeGen as the single source of truth.
2. Scaffold `docs/api/typespec/` with `main.tsp`, `models/`, and `tspconfig.yaml`; author the `Money` scalar, string enums, `ProblemDetails`, and the `version` field.
3. Model every existing FastEndpoints endpoint and DTO in TypeSpec, coordinating value-object → primitive mappings with Task 15 and event DTOs with Task 17.
4. Run `npx tsp compile docs/api/typespec` and confirm the emitter produces OpenAPI 3.1.
5. Add `MoneyJsonConverter.cs` and register it in `Program.cs` beside the existing `JsonStringEnumConverter`.
6. Add `src/Frontend/openapi-ts.config.ts`, the `api:generate` script, and dependencies (`@hey-api/openapi-ts`, `zod`, `decimal.js`) to `src/Frontend/package.json`.
7. Run `npm run api:generate`; commit the contents of `src/Frontend/src/api/generated/`.
8. Migrate all data fetching onto the generated `queryOptions` / `mutationOptions` hooks; delete hand-written fetch wrappers.
9. Rewrite `useRealtimeQuerySync.ts` to `.parse()` every SignalR payload with the generated Zod schemas; remove the unchecked casts.
10. Parse Problem Details responses and map `errors` by property path into form-field errors.
11. Delete `tgconfig.json`, remove the `GenerateTypeScriptContracts` target from `Directory.Build.targets`, and drop the `TypeGen` package.
12. Add `scripts/check-contract-drift.sh` and wire it into CI (regen + `git diff --exit-code`, plus spec-vs-runtime OpenAPI diff).

## 5. Verification

### 5.1 Commands

```bash
# Contract compiles to OpenAPI 3.1
npx tsp compile docs/api/typespec
grep -q '3\.1\.' docs/api/typespec/tsp-output/@typespec/openapi3/openapi.yaml

# Generated client is up to date (fails on any drift)
cd src/Frontend && npm run api:generate && git diff --exit-code src/api/generated

# Frontend type-checks and builds clean
npm run build            # 0 TypeScript errors

# TypeGen is fully gone
test ! -f ../../tgconfig.json
! grep -rq "GenerateTypeScriptContracts" ../../Directory.Build.targets

# Backend + runtime-validation and money round-trip tests
dotnet test src/Backend
npm run test             # malformed API + SignalR payloads rejected by Zod; money precision holds
```

### 5.2 Acceptance criteria

| ID | Criterion | Evidence |
|----|-----------|----------|
| `CONTRACT-01` | TypeSpec compiles and emits OpenAPI 3.1 | `openapi.yaml` reports `openapi: 3.1.x` |
| `CONTRACT-02` | Generated client is regenerated cleanly | `npm run api:generate` then `git diff --exit-code` passes |
| `CONTRACT-03` | Drift gate catches an untracked C# DTO change | A deliberate DTO edit without a spec update fails CI |
| `CONTRACT-04` | Frontend builds with zero TypeScript errors | `npm run build` exits 0 |
| `CONTRACT-05` | Malformed API response is rejected | Test asserts the generated Zod schema throws |
| `CONTRACT-06` | Malformed SignalR payload is rejected | `useRealtimeQuerySync.ts` `.parse()` throws in test |
| `CONTRACT-07` | Money round-trips without precision loss | `18.12345678` survives request → response as a string |
| `CONTRACT-08` | Enums serialize as strings | OpenAPI + wire payloads show string enum values |
| `CONTRACT-09` | TypeGen is fully removed | No `tgconfig.json`, target, or package remains |

## 6. Guardrails

1. Never hand-edit anything under `src/Frontend/src/api/generated/`; change the TypeSpec contract and regenerate.
2. Never cast a SignalR payload to a DTO without a Zod `.parse()` first (Task 11 guardrail #8).
3. Never serialize or transport money as a JSON number; always a decimal string, `decimal.js` on the client.
4. Keep exactly one pipeline — TypeGen must be fully removed, with no dual generation path.
5. Enums always serialize as strings; never emit or accept integer enum values.
6. Do not depend on the alpha TypeSpec C# server emitter; FastEndpoints implements, TypeSpec governs, the drift gate enforces.
7. Treat TypeSpec as the single source of truth; a contract change lands in `docs/api/typespec/` before any implementation change.
8. The CI drift gate is blocking; a non-empty `git diff` or a spec-vs-runtime OpenAPI divergence fails the build.
