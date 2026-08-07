# Task 22: Test Platform Modernization — xUnit v3 / Microsoft.Testing.Platform & Property-Based Testing

> **GREENFIELD MODERNIZATION TASK (2026-08-07)** — Migrate every .NET test project in `tradebook` from xUnit v2 to xUnit v3 (3.2.x) running on Microsoft.Testing.Platform (MTP), and adopt CsCheck (4.x) property-based testing across the suite — most notably for the Task 04 `SemanticQueryCompiler` (JSON AST → parameterized SQL) and the Task 15 Vogen validators/value objects. This is a committed, repo-wide adoption, not a provisional change; record the framework decision in `docs/architecture/decision-log.md`.

- **Phase**: 4 — Testing & Quality Modernization
- **Type**: Cross-cutting / test infrastructure
- **Complexity**: Medium
- **Prerequisites**: Task 13 (coordinates Task 04 semantic compiler, Task 15 value objects, Task 21 Aspire testing)
- **Status**: Specified
- **Target Files**:
  - `tests/Tradebook.UnitTests/Tradebook.UnitTests.csproj` and all unit test files (incl. `SemanticQueryCompilerTests.cs`, validator and value-validation tests)
  - `tests/Tradebook.UnitTests/Properties/SemanticQueryCompilerPropertyTests.cs` (new — CsCheck oracle / metamorphic / injection-whitelist properties)
  - `tests/Tradebook.UnitTests/Properties/ValueObjectPropertyTests.cs` (new — CsCheck round-trip / validation properties)
  - `tests/Tradebook.IntegrationTests/*` (csproj + fixtures; keep Testcontainers PG17 + Respawn + `Microsoft.AspNetCore.Mvc.Testing`)
  - `tests/Tradebook.ArchitectureTests/*` (csproj + ArchUnitNET rules)
  - `stryker-config.json` (MTP runner; keep thresholds high 85 / low 80 / break 80)
  - `.config/dotnet-tools.json` (pin `dotnet-stryker` ≥ 4.13 — current 4.16 — and coverage tooling)

---

## 1. Context

### 1.1 Problem Statement

`tradebook`'s tests run on xUnit v2 hosted by VSTest. That host blocks the platform features this codebase now depends on: native Microsoft.Testing.Platform execution, first-class `Aspire.Hosting.Testing` integration (Task 21), and Stryker's faster MTP mutation runner. The suite is also almost entirely example-based. The Task 04 `SemanticQueryCompiler` (JSON AST → parameterized SQL with an identifier whitelist) and the Task 15 Vogen value objects encode invariants — parameterization, whitelist enforcement, round-trip and validation totality — that hand-written examples cannot cover exhaustively. The untested edges here are security- and correctness-critical: the SQL-injection surface of the compiler and silent acceptance of malformed value objects.

### 1.2 Required Outcomes

- Migrate all three test projects (`Tradebook.UnitTests`, `Tradebook.IntegrationTests`, `Tradebook.ArchitectureTests`) to xUnit v3 (3.2.x) on MTP and remove every xUnit v2 package.
- Run the full suite through `dotnet test` on MTP, green, with no VSTest bridge remaining.
- Drive Stryker.NET (4.16) through its MTP runner and keep the thresholds unchanged: high 85 / low 80 / break 80.
- Add CsCheck (4.x) property tests for the `SemanticQueryCompiler` (oracle/model-based, metamorphic equivalence, injection-whitelist invariants) and for the Vogen value objects and validators (round-trip and validation invariants).
- Preserve the hermetic Testcontainers PG17 + Respawn integration setup, the ArchUnitNET rules, and coverage reporting — all executing under MTP.
- Keep the Task 21 `Aspire.Hosting.Testing` harness running under MTP.

### 1.3 In Scope

- Project-file and package changes across the three test projects, including the coverlet → `Microsoft.Testing.Extensions.CodeCoverage` move for MTP coverage.
- New CsCheck property-test files for the semantic compiler and value objects.
- `stryker-config.json` MTP-runner configuration and `.config/dotnet-tools.json` tool pins.
- Updating any shared `Directory.Build.props` / `Directory.Packages.props` entries the test projects consume.

### 1.4 Out of Scope

- Playwright E2E and k6 load suites (Task 09) — leave entirely unchanged.
- Production/application code changes beyond test-visibility shims needed to import the compiled model and value-object types.
- Lowering mutation thresholds or weakening ArchUnitNET rules to pass.
- Re-opening the test-framework selection.

## 2. Decision & Rationale

### 2.1 Framework Decision (Committed)

xUnit v3 (3.2.x) on Microsoft.Testing.Platform is the committed target for every .NET test project. TUnit was assessed and rejected: although it is natively MTP-based, it carries higher integration risk against Stryker's MTP mutation runner, ArchUnitNET, and `Aspire.Hosting.Testing` at the versions this repo pins. xUnit v3 delivers native MTP execution, a Stryker MTP runner supported since 4.13, and drop-in continuity for the existing `[Fact]`/`[Theory]` tests, Testcontainers fixtures, and ArchUnitNET rules. This decision is final and recorded in `docs/architecture/decision-log.md`; do not re-open it.

### 2.2 Property-Based Testing with CsCheck

Adopt CsCheck 4.x wherever properties add signal beyond example tests. CsCheck's deterministic shrinking and its model-based/operations support fit the compiler (random ASTs against a reference oracle, metamorphic rewrites) and the value objects (round-trip and total-validation laws). Every property must encode a real invariant and must be able to fail on a plausible fault — no tautologies.

## 3. Technical Design

### 3.1 xUnit v3 Project on Microsoft.Testing.Platform + Stryker MTP Runner

```xml
<!-- tests/Tradebook.UnitTests/Tradebook.UnitTests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <!-- Run under Microsoft.Testing.Platform, not the VSTest bridge. -->
    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="3.2.0" />
    <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" Version="17.14.4" />
    <PackageReference Include="CsCheck" Version="4.4.0" />
  </ItemGroup>
</Project>
```

Stryker resolves the MTP runner automatically for MTP test projects; keep the thresholds fixed:

```json
{
  "stryker-config": {
    "project": "src/Tradebook.Domain/Tradebook.Domain.csproj",
    "test-projects": ["tests/Tradebook.UnitTests/Tradebook.UnitTests.csproj"],
    "thresholds": { "high": 85, "low": 80, "break": 80 },
    "reporters": ["html", "progress", "cleartext"]
  }
}
```

### 3.2 CsCheck Properties — SemanticQueryCompiler

```csharp
using CsCheck;
using Tradebook.Domain.Semantics; // SemanticQueryCompiler, QueryAst, CompiledModel, Predicate, Op
using Xunit;

namespace Tradebook.UnitTests.Properties;

public class SemanticQueryCompilerPropertyTests
{
    static readonly CompiledModel Model = CompiledModel.ForEntity<Trade>();
    static readonly SemanticQueryCompiler Compiler = new(Model);

    // Field names originate from the compiled model's whitelist — never arbitrary strings.
    static Gen<string> GenKnownField => Gen.OneOfConst(Model.Fields.ToArray());
    static Gen<object> GenValue => Gen.OneOf(
        Gen.Int.Select(i => (object)i), Gen.String.Select(s => (object)s));
    static Gen<Predicate> GenPredicate =>
        Gen.Select(GenKnownField, GenValue, (f, v) => new Predicate(f, Op.Eq, v));
    static Gen<QueryAst> GenConjunction => GenPredicate.List[1, 6].Select(QueryAst.And);

    // Oracle / model-based: a slow, obviously-correct reference must agree with the
    // production compiler on normalized SQL and on the parameter set.
    [Fact]
    public void Compile_Agrees_With_Reference_Oracle() =>
        GenConjunction.Sample(ast =>
        {
            var actual = Compiler.Compile(ast);
            var oracle = ReferenceCompiler.Compile(ast);
            Assert.Equal(Sql.Normalize(oracle.Sql), Sql.Normalize(actual.Sql));
            Assert.Equal(oracle.Parameters.OrderBy(p => p.Name),
                         actual.Parameters.OrderBy(p => p.Name));
        });

    // Metamorphic: reordering a logically-equal conjunction compiles equivalently.
    [Fact]
    public void Reordered_Conjunction_Is_Equivalent() =>
        GenConjunction
            .SelectMany(ast => Gen.Shuffle(ast.Predicates).Select(sh => (ast, sh)))
            .Sample(t =>
            {
                var a = Compiler.Compile(t.ast);
                var b = Compiler.Compile(QueryAst.And(t.sh));
                Assert.Equal(Sql.Normalize(a.Sql), Sql.Normalize(b.Sql));
                Assert.Equal(a.Parameters.ToHashSet(), b.Parameters.ToHashSet());
            });

    // Injection-whitelist invariant: adversarial identifiers are rejected against the
    // model; adversarial values are always parameterized, never string-interpolated.
    [Fact]
    public void Adversarial_Input_Never_Reaches_Raw_Sql() =>
        Gen.Select(GenAdversarialIdentifier, GenAdversarialValue).Sample((ident, value) =>
        {
            if (!Model.Fields.Contains(ident))
            {
                Assert.Throws<UnknownIdentifierException>(
                    () => Compiler.Compile(QueryAst.Where(ident, Op.Eq, value)));
                return;
            }
            var compiled = Compiler.Compile(QueryAst.Where(ident, Op.Eq, value));
            Assert.DoesNotContain(value, compiled.Sql);                        // not interpolated
            Assert.Contains(compiled.Parameters, p => Equals(p.Value, value)); // parameterized
        });

    static Gen<string> GenAdversarialIdentifier => Gen.OneOf(
        Gen.Const("id; DROP TABLE trades;--"),
        Gen.Const("1=1 OR ticker='x'"),
        Gen.Const("\"; DELETE FROM trades --"),
        GenKnownField);

    static Gen<string> GenAdversarialValue => Gen.OneOf(
        Gen.Const("' OR '1'='1"), Gen.Const("');DROP TABLE trades;--"), Gen.String);
}
```

### 3.3 CsCheck Properties — Vogen Value Objects

```csharp
using CsCheck;
using Tradebook.Domain.ValueObjects; // Ticker (Vogen), TickerGrammar
using Vogen;                          // ValueObjectValidationException
using Xunit;

namespace Tradebook.UnitTests.Properties;

public class ValueObjectPropertyTests
{
    // Any string in the Ticker grammar constructs and round-trips: parse ∘ render = identity.
    static Gen<string> GenValidTicker =>
        Gen.Char['A', 'Z'].Array[1, 5].Select(cs => new string(cs));

    [Fact]
    public void Ticker_Parse_Render_Is_Identity() =>
        GenValidTicker.Sample(raw =>
        {
            var vo = Ticker.From(raw);
            Assert.Equal(vo, Ticker.From(vo.Value)); // round-trip through the rendered form
            Assert.Equal(raw, vo.Value);             // render is faithful
        });

    // Validation is total: valid inputs never throw; invalid inputs are always rejected.
    [Fact]
    public void Ticker_Validation_Is_Total() =>
        Gen.String.Sample(raw =>
        {
            var expectedValid = TickerGrammar.IsValid(raw);
            var thrown = Record.Exception(() => Ticker.From(raw));
            if (expectedValid) Assert.Null(thrown);
            else Assert.IsType<ValueObjectValidationException>(thrown);
        });
}
```

### 3.4 Test Type → Framework Map

| Test type | Project | Framework / tooling |
|---|---|---|
| Unit | `tests/Tradebook.UnitTests` | xUnit v3 3.2.x on MTP |
| Property-based | `tests/Tradebook.UnitTests` | CsCheck 4.x on xUnit v3 / MTP |
| Integration | `tests/Tradebook.IntegrationTests` | xUnit v3 / MTP + Testcontainers PG17 + Respawn + `Microsoft.AspNetCore.Mvc.Testing` |
| Architecture | `tests/Tradebook.ArchitectureTests` | xUnit v3 / MTP + ArchUnitNET |
| Distributed (Aspire) | integration harness | xUnit v3 / MTP + `Aspire.Hosting.Testing` (Task 21) |
| Mutation | all above | Stryker.NET 4.16 via MTP runner |
| Coverage | all above | `Microsoft.Testing.Extensions.CodeCoverage` |
| E2E / load | — | Playwright / k6 (Task 09 — out of scope) |

### 3.5 Reference Documentation

- `docs/architecture/decision-log.md` — the recorded framework-selection ADR.
- xUnit v3 on Microsoft.Testing.Platform — https://xunit.net/docs/getting-started/v3/microsoft-testing-platform
- Microsoft.Testing.Platform overview — https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro
- CsCheck — https://github.com/AnthonyLloyd/CsCheck
- Stryker.NET (MTP runner) — https://stryker-mutator.io/docs/stryker-net/
- Vogen — https://github.com/SteveDunn/Vogen

## 4. Dependencies & Coordination

- **Task 13 (prerequisite)**: its build/test scaffolding is the baseline this migration modifies; land it first.
- **Task 04 (`SemanticQueryCompiler`)**: the compiled model is the single source of truth for the property generators. Import it; do not fork a copy. The generator field-set must equal the compiler's identifier whitelist so the whitelist invariant test stays honest.
- **Task 15 (Vogen value objects)**: property tests import the real `From`/validation types. Coordinate any renamed types or validation messages.
- **Task 21 (`Aspire.Hosting.Testing`)**: align the distributed-app test harness's target framework and package versions so it runs under MTP alongside the migrated projects.

## 5. Verification & Acceptance

### 5.1 Commands

```bash
# 1. Restore pinned local tools (dotnet-stryker, coverage/report tooling).
dotnet tool restore

# 2. Build and run every test project under Microsoft.Testing.Platform.
dotnet test

# 3. Run one MTP test executable directly with native MTP arguments.
dotnet run --project tests/Tradebook.UnitTests \
  -- --filter-namespace Tradebook.UnitTests.Properties --report-trx

# 4. Emit coverage via the MTP code-coverage extension.
dotnet test -- --coverage --coverage-output-format cobertura

# 5. Run mutation testing through Stryker's MTP runner (thresholds enforced).
dotnet stryker --config-file stryker-config.json

# 6. Fail the build if any xUnit v2 package remains.
dotnet list tests package | grep -Ei 'xunit(\.core|\.abstractions)?\s+2\.' && exit 1 || true
```

### 5.2 Acceptance Criteria

| ID | Criterion | Evidence |
|---|---|---|
| TEST-01 | All three test projects build and run under MTP via `dotnet test`, green | CI test summary |
| TEST-02 | No xUnit v2 packages remain in any project | `dotnet list package` audit |
| TEST-03 | Stryker runs via the MTP runner and meets break (80); high 85 / low 80 unchanged | `dotnet stryker` report |
| TEST-04 | CsCheck compiler oracle/metamorphic properties execute; a seeded fault shrinks to a minimal counterexample | test run + seed log |
| TEST-05 | Injection-whitelist property exercises adversarial identifiers/values; unknown identifiers rejected, values parameterized | test review + run |
| TEST-06 | Vogen round-trip and total-validation properties pass | test run |
| TEST-07 | Coverage report (cobertura) generates from the MTP coverage extension | CI artifact |
| TEST-08 | Integration tests stay hermetic on Testcontainers PG17 + Respawn (no shared/host DB) | test run |
| TEST-09 | ArchUnitNET architecture rules run under MTP, green | test run |
| TEST-10 | Task 21 `Aspire.Hosting.Testing` harness runs under MTP, green | test run |

## 6. Guardrails

1. Remove every xUnit v2 package (`xunit`, `xunit.core`, `xunit.abstractions`, v2 `xunit.assert`, v2 `xunit.runner.visualstudio`); only `xunit.v3` (3.2.x) references may remain.
2. Property tests must assert real invariants — no always-true or tautological properties; each must be able to fail on a plausible fault.
3. The `SemanticQueryCompiler` injection-whitelist test must exercise adversarial identifiers and values (e.g. `id; DROP TABLE trades;--`, quote/comment payloads) and assert that unknown identifiers are rejected against the compiled model and that user values are always parameterized, never string-interpolated.
4. Keep Testcontainers PG17 hermetic — every integration run provisions its own container and resets with Respawn; never point at a shared or host database.
5. Do not lower the Stryker thresholds (high 85 / low 80 / break 80) to pass; kill or justify surviving mutants instead.
6. Pin the CsCheck seed in CI and keep shrinking enabled so failures reproduce and report a minimal counterexample.
7. Do not touch the Playwright E2E or k6 load suites (Task 09) — they remain out of scope.
8. Consume the compiled model (Task 04) and value-object types (Task 15) from their owning projects; do not duplicate their definitions inside the test projects.
