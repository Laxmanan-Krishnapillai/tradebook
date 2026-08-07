# Task 13: Platform Currency & Central Package Management

> **GREENFIELD MODERNIZATION TASK (2026-08-07)** — Move the entire `Tradebook.sln` onto **.NET 10 LTS** and adopt repo-wide NuGet **Central Package Management (CPM)** as the single source of version truth. This task is foundational and gates Tasks 14–24 (analyzer/formatter rollout, contract pipeline, migration safety, test-framework migration), each of which assumes a `net10.0` target and a populated root `Directory.Packages.props`. Record the adoption in `docs/architecture/decision-log.md`.

- **Phase:** Foundation / Platform Currency (unblocks Tasks 14–24)
- **Lead / Owner:** Backend Platform
- **Complexity:** Medium — mechanical bulk edit across every project plus one multi-major reconciliation (FastEndpoints 5.30 → 8.x)
- **Prerequisites:** None (foundational; all downstream modernization tasks depend on this one)
- **Status:** Specified
- **Target Files:**
  - `global.json`
  - `Directory.Packages.props` (new)
  - `Directory.Build.props`
  - `Directory.Build.targets`
  - `stryker-config.json`
  - `.github/workflows/ci.yml`
  - `docs/architecture/decision-log.md`
  - `src/Backend/Tradebook.sln`
  - `src/Backend/src/Tradebook.Api/Tradebook.Api.csproj`
  - `src/Backend/src/Tradebook.Core/Tradebook.Core.csproj`
  - `src/Backend/src/Tradebook.Infrastructure/Tradebook.Infrastructure.csproj`
  - `tests/Tradebook.UnitTests/Tradebook.UnitTests.csproj`
  - `tests/Tradebook.IntegrationTests/Tradebook.IntegrationTests.csproj`
  - `tests/Tradebook.ArchitectureTests/Tradebook.ArchitectureTests.csproj`

---

## 1. Detailed Scope & Feature Coverage

### 1.1 Problem Statement

The solution targets `net9.0` on SDK `9.0.316`. .NET 9 reaches **end-of-support on 2026-11-10** (.NET 8 the same day); after that date there are no security patches, which is untenable for a trading platform. .NET 10 is **LTS** (released 2025-11-11, supported through Nov 2028), and the full backend stack — FastEndpoints, Dapper/Npgsql, SignalR + MessagePack, HybridCache, System.Text.Json source generation — already supports `net10.0`. Separately, every `.csproj` declares its own `<PackageReference … Version="…">`, so versions duplicate and drift: `Microsoft.Extensions.Caching.Hybrid 9.3.0`, for example, is pinned independently in both `Tradebook.Api.csproj` and `Tradebook.Infrastructure.csproj`. There is no authoritative version list, `Directory.Build.props` is nearly empty (only a Respawn reference for integration tests), and `<Nullable>`/`<ImplicitUsings>` repeat per project — making the runtime bump an N-project edit instead of a one-file change.

### 1.2 Required Outcomes

- Every project targets `net10.0`; **no `net9.0` remains** anywhere in the repo.
- `global.json` pins a `10.0.x` SDK with `rollForward: latestFeature`.
- A root `Directory.Packages.props` is the single version source, with `ManagePackageVersionsCentrally` and `CentralPackageTransitivePinningEnabled` both `true`.
- No `.csproj` carries a `Version=` attribute on any `<PackageReference>`; all target packages are bumped to their current (Aug 2026) versions per §3.
- `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` are centralized in `Directory.Build.props`.
- The FastEndpoints 5.30 → 8.x API is reconciled and the full suite passes on SDK 10; `GlobalPackageReference` is reserved (documented, unused) for Task 14's repo-wide analyzer rollout.

### 1.3 In Scope

- All backend projects (`Tradebook.Api`, `Tradebook.Core`, `Tradebook.Infrastructure`) and all test projects (`Tradebook.UnitTests`, `Tradebook.IntegrationTests`, `Tradebook.ArchitectureTests`).
- Root MSBuild config (new `Directory.Packages.props`, edited `Directory.Build.props`, verified `Directory.Build.targets` TypeGen 5.0.0 + `stryker-config.json`), the `global.json` SDK pin, and the `.github/workflows/ci.yml` SDK-install step.
- The breaking-change reconciliation (FastEndpoints, FluentValidation, Npgsql) required to compile and pass tests on the bumped versions.

### 1.4 Out of Scope

- Analyzer/formatter policy (Meziantou/Sonar/BannedApi/Threading, CSharpier, `.editorconfig`, `TreatWarningsAsErrors`) — **Task 14**. This task only *reserves* `GlobalPackageReference`; it adds no analyzer packages.
- Test-framework migration to xUnit v3 — **Task 22**. xUnit stays on its current v2 line here and is only centralized.
- Frontend dependency management (`src/Frontend` — npm, Vite, ESLint); CPM governs the .NET graph only.
- Feature work, schema/migration changes, and the OpenAPI/Hey API contract pipeline.

---

## 2. Key Deliverables & File Layout

```text
<repo root>
├── global.json                          EDIT   9.0.316 → 10.0.x SDK pin (rollForward: latestFeature)
├── Directory.Packages.props             NEW    CPM manifest: every <PackageVersion> + CPM switches
├── Directory.Build.props                EDIT   centralize <Nullable> + <ImplicitUsings>
├── Directory.Build.targets / stryker-config.json  VERIFY TypeGen + Stryker still run on SDK 10 / net10.0
├── .github/workflows/ci.yml             EDIT   install/use .NET 10 SDK (actions/setup-dotnet)
├── docs/architecture/decision-log.md    EDIT   append the .NET 10 + CPM ADR entry
├── src/Backend/Tradebook.sln
├── src/Backend/src/Tradebook.Api/Tradebook.Api.csproj                       EDIT net10.0; strip Version=; FastEndpoints 8.x
├── src/Backend/src/Tradebook.Core/Tradebook.Core.csproj                     EDIT net10.0; strip Version=
├── src/Backend/src/Tradebook.Infrastructure/Tradebook.Infrastructure.csproj EDIT net10.0; strip Version=; Dapper/Npgsql
├── src/Frontend/                         OUT OF SCOPE (no .csproj; JS/TS deps unaffected)
├── tests/Tradebook.UnitTests/Tradebook.UnitTests.csproj                     EDIT net10.0; strip Version=
├── tests/Tradebook.IntegrationTests/Tradebook.IntegrationTests.csproj       EDIT net10.0; strip Version= (Testcontainers PG17 + Respawn)
└── tests/Tradebook.ArchitectureTests/Tradebook.ArchitectureTests.csproj     EDIT net10.0; strip Version= (ArchUnitNET)
```

---

## 3. Architecture & Code Contract Blueprints

**`global.json`** — pin the runtime line, let feature/patch bands roll forward:

```json
{
  "sdk": {
    "version": "10.0.103",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

**`Directory.Packages.props`** (new, repo root) — the single source of version truth:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Dapper" Version="2.1.79" />
    <PackageVersion Include="Npgsql" Version="10.0.3" />
    <PackageVersion Include="FastEndpoints" Version="8.2.0" />
    <PackageVersion Include="FluentValidation" Version="12.1.0" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.3" />
    <!-- dedupes Caching.Hybrid 9.3.0 previously pinned in both Api and Infrastructure -->
    <PackageVersion Include="Microsoft.Extensions.Caching.Hybrid" Version="10.0.3" />
    <PackageVersion Include="Microsoft.AspNetCore.SignalR.Protocols.MessagePack" Version="10.0.3" />
    <!-- pin every other Microsoft.Extensions.* found by the step-1 inventory at 10.0.3 -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.0" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.6.0" />
    <PackageVersion Include="Respawn" Version="6.2.1" />
    <PackageVersion Include="TngTech.ArchUnitNET.xUnit" Version="0.11.0" />
  </ItemGroup>
  <!-- RESERVED FOR TASK 13: GlobalPackageReference applies a package to EVERY project from one line
       and carries its Version inline (not split into a PackageVersion). Task 14 owns the entries:
       <ItemGroup><GlobalPackageReference Include="Meziantou.Analyzer" Version="3.0.139" PrivateAssets="All" /></ItemGroup> -->
</Project>
```

**Per-project contract** — flip `TargetFramework`, strip every `Version=`, and drop the now-centralized `<Nullable>`/`<ImplicitUsings>`. Example `Tradebook.Infrastructure.csproj` after:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Dapper" />
    <PackageReference Include="Npgsql" />
    <PackageReference Include="Microsoft.Extensions.Caching.Hybrid" />
  </ItemGroup>
</Project>
```

`Directory.Build.props` gains `<Nullable>enable</Nullable>` + `<ImplicitUsings>enable</ImplicitUsings>` in a shared `<PropertyGroup>`; its existing integration-test Respawn `<PackageReference>` is preserved but loses its `Version=` (now pinned centrally).

**Target versions (Aug 2026) and migration notes:**

| Package | Pin | Consumers | Migration note |
|---|---|---|---|
| Dapper | `2.1.79` | Infrastructure | Low-risk patch bump |
| Npgsql | `10.0.3` | Infrastructure | 10.0 line ships with .NET 10; reconcile any 9.x API |
| FastEndpoints | `8.2.0` | Api | **5.30 → 8.x multi-major** — handler/config + OpenAPI engine changed (step 8) |
| FluentValidation | `12.1.0` | Api / Core | Apache-2.0, free; reconcile v12 validator API |
| Microsoft.AspNetCore.OpenApi | `10.0.3` | Api | .NET 10 emits OpenAPI 3.1; FastEndpoints 8.2 routes through it |
| Microsoft.Extensions.Caching.Hybrid | `10.0.3` | Api **+** Infrastructure | Was duplicated at 9.3.0 — one `PackageVersion` now |
| Microsoft.AspNetCore.SignalR.Protocols.MessagePack | `10.0.3` | Api | Runtime-aligned |
| Test stack — `Microsoft.NET.Test.Sdk` 17.14.1, `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.0, `Testcontainers.PostgreSql` 4.6.0, `Respawn` 6.2.1, `TngTech.ArchUnitNET.xUnit` 0.11.0 | see manifest | test projects | Pinned in `Directory.Packages.props`; xunit stays v2 (v3 = Task 22) |

Docs: CPM — https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management · Support policy — https://learn.microsoft.com/en-us/dotnet/core/releases-and-support · `global.json` — https://learn.microsoft.com/en-us/dotnet/core/tools/global-json

---

## 4. Subagent Implementation Step-by-Step Workflow

1. **Inventory every reference.** Enumerate all `.csproj` under `src/Backend/src/` and `tests/`, the Respawn reference in `Directory.Build.props`, and any package driven from `Directory.Build.targets` (TypeGen 5.0.0). Produce one deduplicated `Include → Version` list; where duplicates disagree, resolve upward to the §3 target.
2. **Create `Directory.Packages.props`** at the repo root with both CPM switches `true`, one `<PackageVersion>` per package at §3 versions, and the commented `GlobalPackageReference` reservation for Task 14.
3. **Flip the runtime.** In every `.csproj`, change `<TargetFramework>net9.0</TargetFramework>` to `net10.0`. Leave no project on net9.0 and do not multi-target to keep it alive.
4. **Strip versions.** Remove the `Version=` attribute from every `<PackageReference>` in every `.csproj` and from the Respawn reference in `Directory.Build.props`; each reference keeps only `Include=` (and existing `PrivateAssets`/`IncludeAssets`).
5. **Centralize compilation defaults.** Move `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` into `Directory.Build.props`; delete the per-project copies; preserve the existing Respawn `ItemGroup`.
6. **Pin the SDK.** Update `global.json` to `"version": "10.0.103"`, `"rollForward": "latestFeature"`, `"allowPrerelease": false`.
7. **Update CI and validate build hooks.** Point the SDK-install step in `.github/workflows/ci.yml` at .NET 10 (`actions/setup-dotnet` with `global-json-file: global.json`). Confirm `stryker-config.json` mutation runs on SDK 10 and the `Directory.Build.targets` TypeGen step runs under net10.0.
8. **Reconcile the multi-major bumps.** FastEndpoints 5.30 → 8.2.0 changes endpoint/handler registration, `app.UseFastEndpoints(...)` configuration, validation wiring, and the OpenAPI engine (v8.2 routes through `Microsoft.AspNetCore.OpenApi`; .NET 10 defaults to OpenAPI 3.1). Reconcile the FluentValidation 12 and Npgsql 10 breaking changes in the endpoints and data layer. Write real code — no stubs, no `#if`-disabled blocks.
9. **Restore, build, test.** Run the §5.1 commands; the solution must build `-c Release` and the full suite (Testcontainers PG17 + Respawn + ArchUnitNET) must pass on SDK 10.
10. **Record the decision.** Append an ADR entry to `docs/architecture/decision-log.md` covering the .NET 10 LTS adoption, CPM as the single version source, transitive pinning for CVE control, and the reserved `GlobalPackageReference` hand-off to Task 14.

---

## 5. Independent Verification & Acceptance Workflow

### 5.1 Verification Commands

```bash
dotnet --version                                                    # → 10.0.x (resolved from global.json)
dotnet restore src/Backend/Tradebook.sln
dotnet build   src/Backend/Tradebook.sln -c Release --no-restore    # PLAT-01
dotnet test    src/Backend/Tradebook.sln -c Release --no-build      # PLAT-02 (PG17 container starts)
grep -rEn '<PackageReference[^>]*\bVersion=' --include='*.csproj' . # PLAT-03: expect NO matches
grep -rn 'net9\.0' --include='*.csproj' . ; grep -n '9\.0\.' global.json  # PLAT-06: expect NO matches
grep -q 'ManagePackageVersionsCentrally>true'         Directory.Packages.props  # PLAT-04
grep -q 'CentralPackageTransitivePinningEnabled>true' Directory.Packages.props  # PLAT-04
grep -q '"version": *"10\.0\.' global.json ; grep -q '"rollForward": *"latestFeature"' global.json  # PLAT-07
grep -rn 'VersionOverride' --include='*.csproj' .                   # each hit must carry an inline <!-- reason -->
```

### 5.2 Acceptance Criteria

| ID | Acceptance criterion | Evidence |
|---|---|---|
| PLAT-01 | `dotnet build Tradebook.sln -c Release` succeeds on SDK 10 | §5.1 build exits 0 |
| PLAT-02 | Full suite builds and passes, integration tests included | §5.1 test green; PG17 container starts |
| PLAT-03 | No `<PackageReference>` carries a `Version=` attribute | §5.1 grep returns no matches |
| PLAT-04 | Root `Directory.Packages.props` has CPM + transitive pinning enabled | §5.1 both greps hit |
| PLAT-05 | Every package pinned once at its §3 target version | manifest review; no duplicate `PackageVersion` |
| PLAT-06 | No `net9.0` remains in any project or config | §5.1 greps return no matches |
| PLAT-07 | `global.json` pins `10.0.x` with `rollForward: latestFeature` | §5.1 both greps hit |
| PLAT-08 | `Nullable`/`ImplicitUsings` centralized in `Directory.Build.props`, removed from `.csproj` | file diff |
| PLAT-09 | `.github/workflows/ci.yml` installs and uses the .NET 10 SDK | workflow diff; CI run on SDK 10 |
| PLAT-10 | FastEndpoints 8.x compiles and endpoints resolve; FluentValidation 12 + Npgsql 10 reconciled | build clean; endpoint tests pass |
| PLAT-11 | `GlobalPackageReference` reserved and documented for Task 14; zero analyzer packages added | manifest review |
| PLAT-12 | ADR entry appended to `docs/architecture/decision-log.md` | file diff |

---

## 6. Anti-Cheating & Integrity Guardrails

1. **Versions live in exactly one place.** The only version declarations are `<PackageVersion>` / `GlobalPackageReference` in `Directory.Packages.props`. Re-adding a per-project `Version=` to sidestep CPM is a failure.
2. **Overrides require justification.** A project may pin differently only via `<PackageReference Include="…" VersionOverride="…" />` with an inline `<!-- reason -->` on the same line. Any unjustified `VersionOverride` is rejected.
3. **No project left on net9.0.** Do not skip a `.csproj`, do not multi-target `net9.0;net10.0`, and do not keep net9.0 alive behind a condition.
4. **Do not float the SDK off 10.0.x.** `rollForward` stays `latestFeature` — never `latestMinor`/`latestMajor`, which could resolve a 9.x or 11.x SDK — and `allowPrerelease` stays `false`.
5. **CPM stays on everywhere.** Do not set `ManagePackageVersionsCentrally=false` or `CentralPackageTransitivePinningEnabled=false` to dodge a pinning conflict; resolve it by raising the pin or adding package source mapping.
6. **No fake-green upgrades.** Do not mask the FastEndpoints / FluentValidation / Npgsql migration with `<NoWarn>`, `#pragma warning disable`, `<TreatWarningsAsErrors>false</…>`, or `[Obsolete]` shims; reconcile the API for real.
7. **Do not weaken the suite.** Do not delete, `[Fact(Skip=…)]`, or comment out tests to reach green; the Testcontainers PG17 integration tests and ArchUnitNET boundary tests must actually execute.
8. **No analyzer scope-creep.** This task reserves `GlobalPackageReference` but adds no analyzer/formatter packages, `.editorconfig` severities, or `TreatWarningsAsErrors` — that is Task 14.
9. **Do not migrate the test framework.** xUnit stays on its current v2 line, centralized only; the v3 move is Task 22.
10. **Keep `Nullable`/`ImplicitUsings` enabled solution-wide.** Do not disable either per project to force a file to compile; fix the nullability instead.
