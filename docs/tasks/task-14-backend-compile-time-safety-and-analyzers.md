# Task 14: Backend Compile-Time Safety, Analyzers & Formatting

> **GREENFIELD MODERNIZATION TASK (2026-08-07)** — This task adopts static analyzers, deterministic formatting, compile-time options validation, and source-generated object mapping across the entire backend, repo-wide, from the first commit. Every knob ships enabled with no staged rollout: `TreatWarningsAsErrors` is on immediately because there is no legacy warning debt to burn down. Record the accepted analyzer, formatter, and mapper choices and their rationale in `docs/architecture/decision-log.md`.

- **Phase**: 3 — Backend Hardening
- **Lead / Owner**: Backend Platform Guild
- **Complexity**: Medium (broad, largely mechanical surface area; low conceptual risk, high blast radius)
- **Prerequisites**: Task 13 (Central Package Management, `GlobalPackageReference`, .NET 10 SDK pin)
- **Status**: Specified
- **Target Files**:
  - `Directory.Build.props`
  - `.editorconfig` (repo root)
  - `BannedSymbols.txt` (repo root)
  - `Directory.Packages.props`
  - `.git-blame-ignore-revs` (repo root)
  - `src/Backend/src/Tradebook.Infrastructure/Options/DatabaseOptions.cs`
  - `src/Backend/src/Tradebook.Infrastructure/Options/JwtOptions.cs`
  - `src/Backend/src/Tradebook.Api/Program.cs`
  - `src/Backend/src/Tradebook.Api/Features/**/*Mapper.cs`

---

## 1. Detailed Scope & Feature Coverage

### 1.1 Problem Statement

The backend compiles cleanly but leans on runtime behavior for guarantees the compiler could enforce. Options bind through reflection-based `ValidateDataAnnotations()`, so misconfiguration surfaces only when a code path executes. DTO-to-domain translation is hand-written and drifts silently. Nothing stops a slice from reading `DateTime.UtcNow` instead of an injected clock, coercing money through `double`, or parsing a price with the ambient culture — each a latent trading-correctness defect. Formatting is unenforced, so diffs carry noise and review friction. This task moves those guarantees to build time.

### 1.2 Required Outcomes

- Analyzers run on every project and their findings break the build.
- `decimal` money (NUMERIC(18,8) quantity, NUMERIC(18,4) price) can never be silently widened to `double`, and wall-clock reads are forced through `TimeProvider`.
- Every options type is validated at startup by a source-generated `[OptionsValidator]` — zero reflection.
- All DTO↔domain mapping is Mapperly source-generated; no reflection mapper, no commercial mapper/mediator.
- Formatting is deterministic and gated: unformatted code fails the build.

### 1.3 In Scope

- Repo-wide analyzer adoption via `GlobalPackageReference` (`PrivateAssets=all`): Meziantou.Analyzer, SonarAnalyzer.CSharp, Microsoft.CodeAnalysis.BannedApiAnalyzers, Microsoft.VisualStudio.Threading.Analyzers, plus `xunit.analyzers` on test projects.
- `BannedSymbols.txt` covering clock, money, and culture-sensitive APIs.
- Solution-wide MSBuild knobs in `Directory.Build.props` and an authoritative `.editorconfig` severity map.
- CSharpier build-time enforcement and a one-shot whole-solution reformat.
- Converting `DatabaseOptions`, `JwtOptions`, and every other options type to `[OptionsValidator]` + `.ValidateOnStart()`.
- Introducing Riok.Mapperly and migrating all existing hand-written maps.
- Converting all request/response DTOs to `record` types with `required` members.

### 1.4 Out of Scope

- Domain value objects for money/quantity (Task 15).
- Frontend linting and formatting (Task 18).
- Runtime performance tuning of the mappers or validators.

---

## 2. Key Deliverables & File Layout

```text
tradebook/
├─ Directory.Build.props            # analyzer + warnings-as-errors knobs (all projects)
├─ Directory.Packages.props         # GlobalPackageReference analyzers + Mapperly version
├─ .editorconfig                    # authoritative per-diagnostic severity + C# style
├─ BannedSymbols.txt                # clock/money/culture bans (fed via AdditionalFiles)
├─ .git-blame-ignore-revs           # the one-shot CSharpier reformat commit SHA
└─ src/Backend/
   ├─ Tradebook.sln
   └─ src/
      ├─ Tradebook.Api/
      │  ├─ Program.cs              # .ValidateOnStart() wiring for every options type
      │  └─ Features/
      │     └─ Trades/
      │        ├─ TradeMapper.cs    # [Mapper] partial — DTO↔domain
      │        └─ Trades.cs         # record + required request/response DTOs
      ├─ Tradebook.Core/
      └─ Tradebook.Infrastructure/
         └─ Options/
            ├─ DatabaseOptions.cs   # [OptionsValidator] partial validator
            └─ JwtOptions.cs        # [OptionsValidator] partial validator
```

---

## 3. Architecture & Code Contract Blueprints

Set the solution-wide knobs in `Directory.Build.props` and feed the ban list to the analyzers there:

```xml
<!-- Directory.Build.props — applies to every project in Tradebook.sln -->
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>

    <!-- Compiler + analyzers catch bugs; warnings are build breaks from day one -->
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <CodeAnalysisTreatWarningsAsErrors>true</CodeAnalysisTreatWarningsAsErrors>
    <AnalysisLevel>latest</AnalysisLevel>
    <AnalysisMode>Recommended</AnalysisMode>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>

    <!-- CSharpier.MsBuild fails the build on unformatted code -->
    <CSharpier_Check>true</CSharpier_Check>
  </PropertyGroup>

  <ItemGroup>
    <AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.txt" />
  </ItemGroup>
</Project>
```

Declare analyzers once as `GlobalPackageReference` so they flow to all projects under CPM:

```xml
<!-- Directory.Packages.props — GlobalPackageReference flows to ALL projects -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <GlobalPackageReference Include="Meziantou.Analyzer" Version="3.0.16" PrivateAssets="all" />
    <GlobalPackageReference Include="SonarAnalyzer.CSharp" Version="10.4.0.108396" PrivateAssets="all" />
    <GlobalPackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="5.0.0" PrivateAssets="all" />
    <GlobalPackageReference Include="Microsoft.VisualStudio.Threading.Analyzers" Version="18.0.11" PrivateAssets="all" />
    <GlobalPackageReference Include="CSharpier.MsBuild" Version="1.2.0" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <!-- Source-generated mapping — a real runtime reference, not an analyzer -->
    <PackageVersion Include="Riok.Mapperly" Version="4.2.1" />
  </ItemGroup>
</Project>
```

Every options type becomes a `record`/class validated by a source-generated partial:

```csharp
// src/Backend/src/Tradebook.Infrastructure/Options/DatabaseOptions.cs
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required, MinLength(1)]
    public required string ConnectionString { get; init; }

    [Range(1, 500)]
    public int MaxPoolSize { get; init; } = 50;

    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

// Source-generated validator — zero reflection, enforced at ValidateOnStart()
[OptionsValidator]
public sealed partial class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>;
```

Wire it in `Program.cs`, replacing `ValidateDataAnnotations()`:

```csharp
builder.Services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();
builder.Services
    .AddOptions<DatabaseOptions>()
    .BindConfiguration(DatabaseOptions.SectionName)
    .ValidateOnStart();
```

All DTO↔domain translation goes through a Mapperly `[Mapper]` partial; DTOs are records with `required` members:

```csharp
// src/Backend/src/Tradebook.Api/Features/Trades/TradeMapper.cs
[Mapper]
public sealed partial class TradeMapper
{
    public partial TradeResponse ToResponse(Trade trade);
    public partial Trade ToDomain(CreateTradeRequest request);
}

public sealed record CreateTradeRequest
{
    public required string Symbol { get; init; }
    public required decimal Quantity { get; init; } // NUMERIC(18,8)
    public required decimal Price { get; init; }    // NUMERIC(18,4)
}
```

Ban clock, money, and culture pitfalls in `BannedSymbols.txt` (declaration-ID `;message` format):

```text
# Clocks — force injected TimeProvider (deterministic, testable)
P:System.DateTime.Now;Use TimeProvider.GetLocalNow(); wall-clock reads must be injected
P:System.DateTime.UtcNow;Use TimeProvider.GetUtcNow(); wall-clock reads must be injected
P:System.DateTimeOffset.UtcNow;Use TimeProvider.GetUtcNow()

# Money — no lossy binary floating point on price/quantity paths
M:System.Decimal.op_Explicit(System.Double)~System.Decimal;Never build money from double
M:System.Decimal.op_Explicit(System.Decimal)~System.Double;Never widen money to double

# Culture — money parse/format must be invariant
M:System.Decimal.Parse(System.String);Pass CultureInfo.InvariantCulture explicitly
M:System.Double.Parse(System.String);Pass CultureInfo.InvariantCulture explicitly
```

**Version matrix**

| Package | Version | Scope | Purpose |
|---|---|---|---|
| Meziantou.Analyzer | 3.0.x | All projects | Correctness, async, culture rules |
| SonarAnalyzer.CSharp | 10.x | All projects | Bug/code-smell detection |
| Microsoft.CodeAnalysis.BannedApiAnalyzers | 5.x | All projects | Enforce `BannedSymbols.txt` (RS0030) |
| Microsoft.VisualStudio.Threading.Analyzers | 18.x | All projects | Async/threading correctness |
| CSharpier.MsBuild | 1.x | All projects | Deterministic formatting gate |
| Riok.Mapperly | 4.x | Api / Core | Compile-time DTO↔domain mapping |
| xunit.analyzers | latest | Test projects | Test authoring correctness |

**Doc links**: [`docs/architecture/decision-log.md`](docs/architecture/decision-log.md) · [Options validation source generator](https://learn.microsoft.com/dotnet/core/extensions/options-validation-generator) · [BannedApiAnalyzers](https://github.com/dotnet/roslyn-analyzers) · [Mapperly](https://mapperly.riok.app) · [CSharpier](https://csharpier.com).

---

## 4. Subagent Implementation Step-by-Step Workflow

1. Add the five analyzer/formatter `GlobalPackageReference` entries (`PrivateAssets=all`) and the `Riok.Mapperly` `PackageVersion` to `Directory.Packages.props`; add `xunit.analyzers` to test projects.
2. Add the MSBuild knobs and the `AdditionalFiles` ban-list entry to `Directory.Build.props`.
3. Author `BannedSymbols.txt` at the repo root with the clock, money, and culture entries.
4. Write the authoritative `.editorconfig`: map each diagnostic ID to its severity (`error`/`warning`/`suggestion`/`none`) and set the C# code-style rules CSharpier does not own.
5. Convert `DatabaseOptions`, `JwtOptions`, and every other options type to a validated shape, add a `[OptionsValidator]` partial per type, and swap `Program.cs` from `ValidateDataAnnotations()` to `.ValidateOnStart()` with the validator registered.
6. Add a `[Mapper]` partial per feature slice under `Features/`, migrate every hand-written map, and delete the manual mapping code.
7. Convert all request/response DTOs to `sealed record` with `required` members.
8. Run CSharpier over the whole solution once; commit that reformat alone and append its SHA to `.git-blame-ignore-revs`.
9. Build the solution, drive every analyzer finding to zero by fixing code (not by suppressing), and update `docs/architecture/decision-log.md`.

---

## 5. Independent Verification & Acceptance Workflow

### 5.1 Commands

```bash
# Restore against the new CPM + analyzer graph
dotnet restore src/Backend/Tradebook.sln

# Whole-solution build; TreatWarningsAsErrors is enforced via Directory.Build.props
dotnet build src/Backend/Tradebook.sln -c Release

# Formatting gate — must report zero files needing changes
dotnet csharpier --check src/Backend

# No reflection-based options validation may remain
grep -rn "ValidateDataAnnotations(" src/Backend/src && exit 1 || true

# No commercial mapper/mediator anywhere in the package graph
grep -rniE "AutoMapper|MediatR|MassTransit" Directory.Packages.props && exit 1 || true

# Prove the ban fires: drop DateTime.Now into a scratch file, build, expect RS0030
```

### 5.2 Acceptance Criteria

| ID | Criterion | Verification |
|---|---|---|
| SAFE-01 | `dotnet build` on `Tradebook.sln` completes with 0 warnings and 0 errors under `TreatWarningsAsErrors` | build command |
| SAFE-02 | `dotnet csharpier --check` reports 0 unformatted files | csharpier gate |
| SAFE-03 | No `ValidateDataAnnotations(` remains in `src/Backend/src` | grep is empty |
| SAFE-04 | Every options type has an `[OptionsValidator]` partial and is wired with `.ValidateOnStart()` | inspection |
| SAFE-05 | All DTO↔domain mapping is via `[Mapper]` partials; no reflection mapper exists | inspection |
| SAFE-06 | No `AutoMapper`, `MediatR`, or `MassTransit` anywhere in the graph | grep is empty |
| SAFE-07 | A `DateTime.Now` usage produces a build error (RS0030) | scratch build |
| SAFE-08 | All request/response DTOs are `record` with `required` members | inspection |
| SAFE-09 | Analyzers apply repo-wide via `GlobalPackageReference` with `PrivateAssets=all` | `Directory.Packages.props` |
| SAFE-10 | The one-shot CSharpier reformat commit SHA is listed in `.git-blame-ignore-revs` | file check |

---

## 6. Anti-Cheating & Integrity Guardrails

1. Never blanket-suppress an analyzer. Any `#pragma warning disable` or `.editorconfig` `severity = none` must carry an inline justification naming the rule ID and the reason; unexplained suppressions fail review.
2. Never add AutoMapper, MediatR, or MassTransit — all three carry commercial licensing (RPL-1.5 or paid) as of 2025. CI greps the package graph and fails on a match.
3. No reflection-based mapping. Every map is a Mapperly source-generated `[Mapper]` partial; hand-rolled reflection or `dynamic` mapping is rejected.
4. Do not weaken `TreatWarningsAsErrors`, lower `AnalysisMode`, or downgrade per-rule severities to make a build pass — fix the offending code instead.
5. Do not exclude files or globs from CSharpier or the analyzers to dodge findings; the gates apply to the whole solution.
6. `.editorconfig` is the single source of truth for severities — no per-project `<NoWarn>` dumping grounds.
7. Keep banned symbols banned: no adding `DateTime.Now`/`DateTime.UtcNow` to an allowed list; read the clock through an injected `TimeProvider`.
