# Task 08: Autonomous AI Agent-Readiness Tooling, Contract Generation & Testing Infrastructure

> **DESCOPE NOTICE (2026-08-06 — applied to this spec)** — per [`architecture/decision-log.md`](../architecture/decision-log.md): all Native AOT references were removed (**D7**). This task formally **owns ArchUnitNET** boundary tests (**D13**) — they live only under `tests/Tradebook.ArchitectureTests` (§5.7); Tasks 02/10 reference them, never duplicate them. TypeGen is pinned to one version (5.0.0) across all tasks. Stryker: one threshold (break = 80), one root config, repo-internal paths only.

- **Phase**: Phase 3 (Messaging, Data Pipelines & Governance)
- **Lead / Owner**: Specialist AI Agent / DevEx Lead
- **Complexity**: Medium
- **Prerequisites**: Task 02 (.NET 9 Modular Monolith Backend Core)
- **Target Files**:
  - `.commitlintrc.json`
  - `.releaserc.json`
  - `bin/agent-commit.sh`
  - `tgconfig.json`
  - `.github/workflows/verify-contracts.yml`
  - `stryker-config.json`
  - `.eslintrc.cjs`
  - `AGENTS.md`
  - `GEMINI.md`
  - `backend/AGENTS.md`
  - `frontend/AGENTS.md`
  - `infra/AGENTS.md`
  - `tests/Tradebook.IntegrationTests/Fixtures/CustomWebApplicationFactory.cs`
  - `tests/Tradebook.IntegrationTests/Fixtures/DatabaseTestBase.cs`
  - `tests/Tradebook.ArchitectureTests/Tradebook.ArchitectureTests.csproj`
  - `tests/Tradebook.ArchitectureTests/BoundaryTests.cs`
  - `src/Frontend/src/mocks/handlers.ts`
  - `src/Frontend/src/mocks/server.ts`

---

## 1. Executive Overview & Objectives

### 1.1 Executive Summary
Task 08 establishes the comprehensive **Agent-Readiness & Governance Framework** for the Tradebook monorepo (.NET 9 Web API + PostgreSQL 17 + React 19 SPA). Autonomous AI coding agents require deterministic feedback loops, strict type boundaries, hermetic test execution, structured context maps, and automated lint/release guardrails to operate safely without human supervision.

This task delivers 5 fundamental pillars of agent governance:
1. **Conventional Commits 1.0.0 & Automated Release Management**: Machine-parseable commit message structures enforced by `.commitlintrc.json`, automated semantic versioning via `.releaserc.json`, and a validated commit helper wrapper script `bin/agent-commit.sh`.
2. **Automated C# to TypeScript TypeGen Pipeline**: Zero-drift contract generation from backend C# DTO records to frontend TypeScript interfaces with MSBuild post-build compilation and GitHub Actions zero-drift CI enforcement.
3. **Hermetic Test Fixtures & Stryker.NET Mutation Testing**: Total test isolation using `Testcontainers` PostgreSQL 17, sub-10ms table resets via `Respawn`, MSW 2.0 network mockers for React 19, and Stryker.NET mutation score break thresholds (>=80%).
4. **Master Context Map System & ESLint Boundary Enforcement**: Comprehensive AI navigation guides (`AGENTS.md`, `GEMINI.md`, sub-directory context maps) and ESLint `eslint-plugin-boundaries` rule sets to prevent architecture corruption.
5. **Step-by-step Subagent Execution & Independent Verification**: Precise workflows, test plans, and quantitative SLA verification commands to prevent shortcuts or facade code.

### 1.2 Core System Objectives
1. **Machine-Readable Git History**: Enforce strict Conventional Commit formatting to automate CHANGELOG generation and semantic release versioning.
2. **Zero Type Drift**: Eliminate manual interface maintenance between .NET 9 FastEndpoints DTOs and React 19 TypeScript interfaces through automated code generation.
3. **Hermetic Test Reliability**: Eliminate flaky tests caused by dirty database state or missing external dependencies using containerized ephemerality and MSW mocking.
4. **Mutated Logic Assertions**: Enforce a mandatory Stryker.NET mutation score threshold of >=80% on backend slices to prevent fake/tautological unit tests (`Assert.True(true)`).
5. **AI Navigation Ergonomics**: Maintain multi-level `AGENTS.md` context maps providing direct commands, directory rules, and architecture blueprints for autonomous agents.

---

## 2. Prerequisites, Scope & Dependencies

### 2.1 Prerequisites
- **Runtime & Tools**:
  - .NET 9.0 SDK (`dotnet`)
  - Node.js 22.x LTS & npm 10.x
  - Docker Desktop 24.0+ / Docker Engine (for Testcontainers)
  - `git` 2.40+ & `bash` shell environment
  - `npx` (for Commitlint, ESLint, Stryker)
- **NuGet Packages**:
  - `TypeGen` (**pinned 5.0.0** — the single version used in every csproj and CI step)
  - `Testcontainers.PostgreSql` (v3.10+)
  - `Respawn` (v6.2+)
  - `Microsoft.AspNetCore.Mvc.Testing` (v9.0+)
  - `Stryker.CLI` (v4.0+)
  - `ArchUnitNET.xUnit` (v0.11+ — architecture boundary tests, owned here per D13)
- **npm Packages**:
  - `@commitlint/cli`, `@commitlint/config-conventional`
  - `@semantic-release/changelog`, `@semantic-release/exec`, `@semantic-release/git`
  - `msw` (v2.0+)
  - `eslint-plugin-boundaries`

### 2.2 Scope Boundaries
- **In-Scope**:
  - Provisioning `.commitlintrc.json`, `.releaserc.json`, and `bin/agent-commit.sh`.
  - Setting up `TypeGen` C#-to-TypeScript generation pipeline (`tgconfig.json`, MSBuild post-build target, GitHub Actions `.github/workflows/verify-contracts.yml`).
  - Building `CustomWebApplicationFactory` (Testcontainers PostgreSQL 17) and `DatabaseTestBase` (Respawn resets).
  - Setting up MSW 2.0 network handlers and mock server for frontend unit/integration testing.
  - Configuring Stryker.NET (`stryker-config.json`) with the single enforced break threshold (80).
  - Owning `tests/Tradebook.ArchitectureTests` — ArchUnitNET boundary rules (D13).
  - Creating master context maps (`AGENTS.md`, `GEMINI.md`, `backend/AGENTS.md`, `frontend/AGENTS.md`, `infra/AGENTS.md`).
  - Configuring ESLint module boundaries (`.eslintrc.cjs`).
  - Full test plan and automated verification steps.
- **Out-of-Scope**:
  - Writing domain feature logic or CRUD endpoints (covered in Task 02 and Task 05).
  - Terraform cloud infrastructure setup (covered in Task 07).
  - E2E Playwright test scenarios (covered in Task 09).

### 2.3 Dependency Graph
```
┌─────────────────────────────────────────────────────────────┐
│                       Task 02 Backend                       │
│ .NET 9 FastEndpoints Monolith Slices & Record DTOs          │
└──────────────────────────────┬──────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                    Task 08 Agent-Readiness                  │
│ Conventional Commits, TypeGen, Hermetic Tests, Context Maps │
└──────────────────────────────┬──────────────────────────────┘
                               │
            ┌──────────────────┴──────────────────┐
            ▼                                     ▼
┌───────────────────────┐             ┌───────────────────────┐
│     Task 05 Frontend  │             │ Task 09 E2E Testing   │
│ React 19 UI & Stores  │             │ Playwright & k6       │
└───────────────────────┘             └───────────────────────┘
```

---

## 3. Conventional Commits 1.0.0 & Semantic Release Infrastructure

### 3.1 Conventional Commits 1.0.0 Specification & Monorepo Scope Registry
AI agents commit automatically during multi-step tasks. To maintain release hygiene, all commits must adhere to Conventional Commits 1.0.0:
```text
<type>(<scope>): <short summary in lower-case imperative mood>

[optional body describing technical rationale and logic chain]

[optional footer(s), e.g. BREAKING CHANGE: <description>]
```

#### Monorepo Allowed Scope Registry
To prevent scope fragmentation, scopes are strictly constrained to physical monorepo boundaries:

| Monorepo Section | Allowed Scopes | Description |
| :--- | :--- | :--- |
| **Backend** | `backend/core`, `backend/auth`, `backend/ledger`, `backend/cqrs`, `backend/jobs` | .NET 9 Web API slices and core domain engines |
| **Frontend** | `frontend/ui`, `frontend/kanban`, `frontend/canvas`, `frontend/store`, `frontend/routes` | React 19 components, state stores, and routing |
| **Contracts** | `contracts` | Auto-generated TypeScript DTOs & API schemas |
| **Infrastructure** | `infra/docker`, `infra/tf`, `infra/db` | Docker Compose, PostgreSQL DDL, Terraform modules |
| **DevEx & CI** | `ci`, `deps`, `docs`, `repo` | GitHub Actions, dependencies, documentation, tooling |

### 3.2 Commitlint Configuration (`.commitlintrc.json`)

```json
{
  "$schema": "https://json.schemastore.org/commitlintrc.json",
  "extends": ["@commitlint/config-conventional"],
  "rules": {
    "type-enum": [
      2,
      "always",
      [
        "build",
        "chore",
        "ci",
        "docs",
        "feat",
        "fix",
        "perf",
        "refactor",
        "revert",
        "style",
        "test"
      ]
    ],
    "scope-enum": [
      2,
      "always",
      [
        "backend/core",
        "backend/auth",
        "backend/ledger",
        "backend/cqrs",
        "backend/jobs",
        "frontend/ui",
        "frontend/kanban",
        "frontend/canvas",
        "frontend/store",
        "frontend/routes",
        "contracts",
        "infra/docker",
        "infra/tf",
        "infra/db",
        "ci",
        "deps",
        "docs",
        "repo"
      ]
    ],
    "scope-empty": [2, "never"],
    "subject-case": [2, "always", "lower-case"],
    "header-max-length": [2, "always", 100]
  }
}
```

### 3.3 Semantic Release Configuration (`.releaserc.json`)

```json
{
  "branches": ["main"],
  "plugins": [
    [
      "@semantic-release/commit-analyzer",
      {
        "preset": "conventionalcommits",
        "releaseRules": [
          { "type": "docs", "scope": "README", "release": "patch" },
          { "type": "refactor", "release": "patch" },
          { "type": "style", "release": false },
          { "type": "perf", "release": "minor" },
          { "type": "feat", "release": "minor" },
          { "type": "fix", "release": "patch" }
        ]
      }
    ],
    [
      "@semantic-release/release-notes-generator",
      {
        "preset": "conventionalcommits"
      }
    ],
    [
      "@semantic-release/changelog",
      {
        "changelogFile": "CHANGELOG.md"
      }
    ],
    [
      "@semantic-release/exec",
      {
        "prepareCmd": "dotnet build src/Backend/Tradebook.sln -c Release /p:Version=${nextRelease.version}"
      }
    ],
    [
      "@semantic-release/git",
      {
        "assets": ["CHANGELOG.md", "Directory.Build.props"],
        "message": "chore(release): ${nextRelease.version} [skip ci]\n\n${nextRelease.notes}"
      }
    ],
    "@semantic-release/github"
  ]
}
```

### 3.4 Agent Git Wrapper Script (`bin/agent-commit.sh`)

Autonomous agents execute commits via this wrapper script, which validates the message format using `commitlint` before invoking `git commit`.

```bash
#!/usr/bin/env bash
# bin/agent-commit.sh - Validated Git Commit Wrapper for Autonomous AI Agents
set -euo pipefail

if [ "$#" -lt 3 ]; then
    echo "Usage: bin/agent-commit.sh <type> <scope> <summary> [body]"
    echo "Example: bin/agent-commit.sh feat backend/ledger 'add bi-temporal audit trigger execution'"
    exit 1
fi

TYPE="$1"
SCOPE="$2"
SUMMARY="$3"
BODY="${4:-}"

# Construct commit header
HEADER="${TYPE}(${SCOPE}): ${SUMMARY}"

# Format full commit message
if [ -n "$BODY" ]; then
    COMMIT_MSG="${HEADER}"$'\n\n'"${BODY}"
else
    COMMIT_MSG="${HEADER}"
fi

echo "==> Validating commit message via Commitlint..."
echo "$COMMIT_MSG" | npx commitlint

echo "==> Commitlint check passed. Executing git commit..."
git commit -m "$COMMIT_MSG"
```

---

## 4. Automated Type-Safety Contract Generation Pipeline

### 4.1 TypeGen Tooling Strategy & C# to TypeScript Mapping Blueprint
To prevent contract drift between the .NET 9 Web API backend and the React 19 frontend, `TypeGen` reflects directly on compiled C# DTO record assemblies and exports strictly typed TypeScript interface files. **C# is the single source of truth for all API contracts; TypeScript is generated output, never authored.**

```
   +-------------------------------------------------------+
   | C# DTO Records (.NET 9 FastEndpoints)                 |
   | src/Backend/src/Tradebook.Api/Features/PhysicalDeliveries/CreatePhysicalDeliveryEndpoint.cs    |
   +-------------------------------------------------------+
                              |
                     (dotnet build trigger)
                              v
   +-------------------------------------------------------+
   | MSBuild Post-Build TypeGen Generator Exec Target      |
   | tgconfig.json Assembly Reflection                     |
   +-------------------------------------------------------+
                              |
                              v
   +-------------------------------------------------------+
   | Generated TypeScript Interfaces & Enums               |
   | src/Frontend/src/api/generated/create-physical-delivery-request.ts     |
   +-------------------------------------------------------+
                              |
                              v
   +-------------------------------------------------------+
   | Zero-Drift CI Verification Workflow                   |
   | .github/workflows/verify-contracts.yml               |
   +-------------------------------------------------------+
```

### 4.2 TypeGen Configuration (`tgconfig.json`)

```json
{
  "$schema": "https://raw.githubusercontent.com/jburzynski/TypeGen/master/schema/tgconfig.json",
  "outputPath": "./src/Frontend/src/api/generated",
  "clearOutputDirectory": true,
  "generateObsoleteAttribute": true,
  "typeUnionsForEnums": true,
  "enumStringInitializers": true,
  "propertyNameConverters": ["CamelCase"],
  "customTypeMappings": {
    "System.Guid": "string",
    "System.DateTime": "string",
    "System.DateTimeOffset": "string",
    "NodaTime.Instant": "string",
    "decimal": "number"
  },
  "csNullableTranslation": "Null",
  "assemblies": [
    "./src/Backend/src/Tradebook.Api/bin/Debug/net9.0/Tradebook.Api.dll"
  ]
}
```

### 4.3 Backend FastEndpoints REPR C# DTO Example

```csharp
// File: src/Backend/src/Tradebook.Api/Features/PhysicalDeliveries/CreatePhysicalDeliveryModels.cs
namespace Tradebook.Api.Features.PhysicalDeliveries;

using TypeGen.Core.TypeAnnotations;

[ExportTsInterface(ReturnValueAttributes = true)]
public record CreatePhysicalDeliveryRequest(
    Guid ContractId,
    string ContractInstanceId,
    BookType BookType,
    DateTime SupplyMonth,
    decimal? CapacityMw,
    decimal? VolumeNominatedMwh,
    decimal? VolumeRealisedMwh,
    decimal? PriceEurMwh,
    string? PriceMechanism,
    string? CustomFieldsJson
);

[ExportTsEnum]
public enum BookType
{
    Sourcing,
    Sales,
    Intercompany
}

[ExportTsInterface]
public record CreatePhysicalDeliveryResponse(
    Guid DeliveryId,
    string ContractInstanceId,
    decimal? InvoiceAmountEur,
    string Status,
    DateTimeOffset CreatedAt
);
```

### 4.4 MSBuild Post-Build Contract Generator Target (`Tradebook.Api.csproj`)

Add the following post-build target block to `src/Backend/src/Tradebook.Api/Tradebook.Api.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="FastEndpoints" Version="5.30.0" />
    <PackageReference Include="TypeGen" Version="5.0.0" /> <!-- pinned: same version everywhere -->

  </ItemGroup>

  <!-- Post-Build Automated TypeScript Generation -->
  <Target Name="GenerateTypeScriptContracts" AfterTargets="Build" Condition="'$(Configuration)' == 'Debug'">
    <Message Text="[TypeGen] Generating TypeScript DTO contracts for React frontend..." Importance="high" />
    <Exec Command="dotnet typegen generate --project-folder $(MSBuildProjectDirectory)/../../.." ContinueOnError="false" />
  </Target>
</Project>
```

### 4.5 Generated TypeScript Interface Contract Example

```typescript
// File: src/Frontend/src/api/generated/create-physical-delivery-request.ts
// Auto-generated by TypeGen - DO NOT EDIT DIRECTLY

// typeUnionsForEnums: enums become literal unions of the verbatim C# member names
export type BookType = "Sourcing" | "Sales" | "Intercompany";

export interface CreatePhysicalDeliveryRequest {
  contractId: string;
  contractInstanceId: string;
  bookType: BookType;
  supplyMonth: string;
  capacityMw?: number | null;
  volumeNominatedMwh?: number | null;
  volumeRealisedMwh?: number | null;
  priceEurMwh?: number | null;
  priceMechanism?: string | null;
  customFieldsJson?: string | null;
}

export interface CreatePhysicalDeliveryResponse {
  deliveryId: string;
  contractInstanceId: string;
  invoiceAmountEur?: number | null;
  status: string;
  createdAt: string;
}
```

### 4.6 GitHub Actions Zero-Drift Verification Workflow (`.github/workflows/verify-contracts.yml`)

```yaml
name: Verify TypeScript Contract Drift

on:
  push:
    branches: [ main, dev ]
  pull_request:
    branches: [ main, dev ]

jobs:
  check-contract-drift:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout Source Code
        uses: actions/checkout@v4

      - name: Setup .NET 9 SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Setup Node.js 22
        uses: actions/setup-node@v4
        with:
          node-version: '22.x'

      - name: Install Tooling & Build Backend
        run: |
          dotnet tool install -g TypeGen --version 5.0.0
          dotnet build src/Backend/Tradebook.sln -c Debug

      - name: Generate TypeScript Contracts
        run: |
          dotnet typegen generate --project-folder .

      - name: Assert Zero Git Drift
        run: |
          git status --porcelain src/Frontend/src/api/generated
          if [ -n "$(git status --porcelain src/Frontend/src/api/generated)" ]; then
            echo "::error::Uncommitted TypeScript contract changes detected!"
            echo "Run 'dotnet typegen generate' locally and commit the generated types in src/Frontend/src/api/generated."
            git diff src/Frontend/src/api/generated
            exit 1
          fi
          echo "SUCCESS: Zero contract drift verified."
```

### 4.7 Enum Serialization & Casing Contract (single convention)

`JsonStringEnumConverter` (registered in Task 02's `Program.cs`) serializes every enum as its **C# member name, verbatim PascalCase** (e.g. `Sourcing`, `Intercompany`). The same strings appear in three places, and all three must match exactly:
1. C# enum member names — the source of truth;
2. generated TS literal unions — `typeUnionsForEnums` + `enumStringInitializers` emit the member names verbatim;
3. FluentValidation accepted-value lists in Task 02 validators.

No kebab-case, camelCase, or snake_case variants anywhere on the wire. This is the single casing convention for enum values across the platform.

---

## 5. Hermetic Test Fixtures & Stryker.NET Mutation Testing

### 5.1 Hermetic Testing Architecture
Autonomous agents frequently corrupt shared test databases or rely on external third-party services, causing false failures.
Hermetic testing guarantees:
- **Ephemeral PostgreSQL 17**: Testcontainers boots disposable PostgreSQL 17 Docker instances per test assembly.
- **Sub-10ms Resets**: `Respawn` truncates tables between test methods without restarting containers.
- **MSW 2.0 Mocking**: Mock Service Worker intercepts all frontend HTTP requests in Vitest.

```
   +-------------------------------------------------------+
   | Ephemeral PostgreSQL 17 Docker Container              |
   | Testcontainers.PostgreSql (Port Allocated Dynamically)|
   +-------------------------------------------------------+
                              ^
                              | (DB Connection String)
   +-------------------------------------------------------+
   | CustomWebApplicationFactory<Program>                  |
   | Replaces Production DB Connection in ASP.NET Core     |
   +-------------------------------------------------------+
                              |
                     (Sub-10ms Reset)
                              v
   +-------------------------------------------------------+
   | Respawn Fast Truncation Harness                       |
   | DatabaseTestBase.ResetStateAsync() per Test Method    |
   +-------------------------------------------------------+
```

### 5.2 Backend WebApplicationFactory & Ephemeral Testcontainer Fixture

```csharp
// File: tests/Tradebook.IntegrationTests/Fixtures/CustomWebApplicationFactory.cs
namespace Tradebook.IntegrationTests.Fixtures;

using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Xunit;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .WithDatabase("tradebook_hermetic_test")
        .WithUsername("test_user")
        .WithPassword("test_password_123")
        .Build();

    private NpgsqlConnection _dbConnection = default!;
    private Respawner _respawner = default!;

    public string ConnectionString => _dbContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        // 1. Boot PostgreSQL 17 container
        await _dbContainer.StartAsync();

        _dbConnection = new NpgsqlConnection(ConnectionString);
        await _dbConnection.OpenAsync();

        // 2. Execute SQL DDL Migrations
        await ApplyDatabaseMigrationsAsync(_dbConnection);

        // 3. Initialize Respawn Truncator
        _respawner = await Respawner.CreateAsync(_dbConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = new[] { "public" }
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await _respawner.ResetAsync(_dbConnection);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseEnvironment("Testing");
    }

    private static async Task ApplyDatabaseMigrationsAsync(NpgsqlConnection conn)
    {
        // Real Task 01 schema — apply every migration in src/Database/Migrations, in order.
        // No synthetic fixture DDL: tests run against the exact schema production runs.
        var migrationsDir = Path.Combine(FindRepoRoot(), "src", "Database", "Migrations");
        foreach (var file in Directory.GetFiles(migrationsDir, "*.sql").OrderBy(f => f))
        {
            var sql = await File.ReadAllTextAsync(file);
            await using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 300 };
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Database", "Migrations")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate repo root (src/Database/Migrations).");
    }

    public new async Task DisposeAsync()
    {
        await _dbConnection.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }
}
```

### 5.3 Respawn Sub-10ms Database Truncation Harness

```csharp
// File: tests/Tradebook.IntegrationTests/Fixtures/DatabaseTestBase.cs
namespace Tradebook.IntegrationTests.Fixtures;

using System.Threading.Tasks;
using Xunit;

public abstract class DatabaseTestBase : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected DatabaseTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    public virtual Task InitializeAsync()
    {
        // Executes sub-10ms table truncation before each test method runs
        return Factory.ResetDatabaseAsync();
    }

    public virtual Task DisposeAsync() => Task.CompletedTask;
}
```

### 5.4 Frontend MSW 2.0 Network Mocking Setup

```typescript
// File: src/Frontend/src/mocks/handlers.ts
import { http, HttpResponse } from 'msw';
import { CreatePhysicalDeliveryResponse, BookType } from '../api/generated/create-physical-delivery-request';

export const handlers = [
  // Intercept Physical Delivery Creation Endpoint
  http.post('/api/v1/deliveries', async ({ request }) => {
    const body = await request.json() as any;

    const mockResponse: CreatePhysicalDeliveryResponse = {
      deliveryId: '11111111-2222-3333-4444-555555555555',
      contractInstanceId: body.contractInstanceId || 'BFEX45.BT.2301.CO2E-9-2023',
      status: 'Pending - No Invoice',
      createdAt: new Date().toISOString()
    };

    return HttpResponse.json(mockResponse, { status: 201 });
  }),

  // Intercept Physical Delivery Listing Endpoint
  http.get('/api/v1/deliveries', () => {
    return HttpResponse.json([
      {
        deliveryId: '11111111-2222-3333-4444-555555555555',
        contractInstanceId: 'BFEX45.BT.2301.CO2E-9-2023',
        bookType: 'Sourcing' satisfies BookType,
        supplyMonth: '2023-09-01',
        volumeNominatedMwh: 12000,
        priceEurMwh: 34.50
      }
    ], { status: 200 });
  })
];
```

```typescript
// File: src/Frontend/src/mocks/server.ts
import { setupServer } from 'msw/node';
import { handlers } from './handlers';

export const server = setupServer(...handlers);
```

### 5.5 Stryker.NET Mutation Testing Architecture & Threshold Enforcements

Unit test line coverage (e.g. 90%) can be spoofed by AI agents writing tests with zero assertions. Stryker.NET injects artificial bugs (mutants) into compiled binaries to confirm that test suites fail when logic is altered.

#### Stryker Configuration (`stryker-config.json`)

```json
{
  "$schema": "https://raw.githubusercontent.com/stryker-mutator/stryker-net/master/src/Stryker.Core/Stryker.Core/ConfigFile/stryker-config.schema.json",
  "stryker-config": {
    "solution": "src/Backend/Tradebook.sln",
    "project": "Tradebook.Api.csproj",
    "test-projects": ["tests/Tradebook.UnitTests/Tradebook.UnitTests.csproj"],
    "mutation-level": "Standard",
    "concurrency": 4,
    "reporters": ["html", "progress", "cleartext", "json"],
    "thresholds": {
      "high": 85,
      "low": 75,
      "break": 80
    },
    "mutate": [
      "Features/**/*.cs",
      "!Features/**/Models/*.cs",
      "!Features/**/Dto/*.cs"
    ]
  }
}
```

### 5.6 Single Mutation Threshold

**One number, one config**: the Stryker break threshold is **80**, defined once in the root `stryker-config.json` and applied to the whole backend. There are no per-module threshold overrides — a single value keeps CI deterministic and leaves no knob for threshold gaming (§10).

### 5.7 Architecture Boundary Tests (ArchUnitNET — owned here, D13)

Task 08 is the **single owner** of architecture boundary tests. They live in `tests/Tradebook.ArchitectureTests`; no other task ships ArchUnitNET code or package references.

```xml
<!-- File: tests/Tradebook.ArchitectureTests/Tradebook.ArchitectureTests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="ArchUnitNET.xUnit" Version="0.11.1" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Backend\src\Tradebook.Api\Tradebook.Api.csproj" />
    <ProjectReference Include="..\..\src\Backend\src\Tradebook.Core\Tradebook.Core.csproj" />
    <ProjectReference Include="..\..\src\Backend\src\Tradebook.Infrastructure\Tradebook.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

Three concrete rules (the initial, mandatory set):

```csharp
// File: tests/Tradebook.ArchitectureTests/BoundaryTests.cs
namespace Tradebook.ArchitectureTests;

using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

public class BoundaryTests
{
    private static readonly Architecture Arch = new ArchLoader()
        .LoadAssemblies(
            System.Reflection.Assembly.Load("Tradebook.Api"),
            System.Reflection.Assembly.Load("Tradebook.Core"),
            System.Reflection.Assembly.Load("Tradebook.Infrastructure"))
        .Build();

    // 1. Core references neither Api nor Infrastructure (dependency rule, Task 02 §2)
    [Fact]
    public void Core_DependsOn_Neither_Api_Nor_Infrastructure() =>
        Types().That().ResideInNamespace("Tradebook.Core", true)
            .Should().NotDependOnAny(Types().That().ResideInNamespace("Tradebook.Api", true))
            .AndShould().NotDependOnAny(Types().That().ResideInNamespace("Tradebook.Infrastructure", true))
            .Check(Arch);

    // 2. Api endpoint classes never touch Npgsql directly — data access goes through Infrastructure
    [Fact]
    public void ApiEndpoints_DoNot_Reference_Npgsql() =>
        Classes().That().ResideInNamespace("Tradebook.Api.Features", true)
            .Should().NotDependOnAny(Types().That().ResideInNamespace("Npgsql", true))
            .Check(Arch);

    // 3. Feature folders do not reference sibling feature folders (vertical-slice isolation)
    [Fact]
    public void FeatureSlices_DoNot_Reference_Siblings() =>
        Types().That().ResideInNamespace("Tradebook.Api.Features.PhysicalDeliveries", true)
            .Should().NotDependOnAny(Types().That().ResideInNamespace("Tradebook.Api.Features.MarketPrices", true))
            .Check(Arch);
}
```

---

## 6. Master Context Map System & Static Boundary Enforcement

### 6.1 Context Map Hierarchy & Architecture
To guide autonomous agents quickly to relevant rules and code paths, a hierarchical context map system is deployed:
- `AGENTS.md` (Root): Monorepo architecture rules, build/test commands, master directory layout.
- `GEMINI.md` (Root): Gemini-specific system directives, formatting mandates, tool invocation rules.
- `backend/AGENTS.md`: Vertical slice design rules, FastEndpoints pattern, REPR conventions.
- `frontend/AGENTS.md`: React 19 Feature-Sliced Design rules, state machine conventions.
- `infra/AGENTS.md`: Terraform HCL module conventions, Docker Compose networking rules.

```
c:\Users\LaxmananKrishnapilla\tradebook\
├── AGENTS.md                          # Master AI Context Map
├── GEMINI.md                          # Gemini Subagent Directives
├── .commitlintrc.json                 # Commit Lint Rules
├── .releaserc.json                    # Release Automation Setup
├── bin/
│   └── agent-commit.sh                # Validated Commit Wrapper
├── src/
│   ├── Backend/
│   │   └── AGENTS.md                  # Backend Vertical Slice Guide
│   └── Frontend/
│       ├── AGENTS.md                  # Frontend Component & Store Guide
│       └── .eslintrc.cjs              # Feature-Sliced Boundary Rules
└── infra/
    └── AGENTS.md                      # IaC & Docker Environment Guide
```

### 6.2 Root Master Context Map (`AGENTS.md`)

```markdown
# Tradebook Master Agent Context Guide (`AGENTS.md`)

Welcome, Autonomous AI Agent. This codebase is configured for deterministic, agent-first execution.

## Core Rules of Engagement
1. **Never Edit Generated Contracts Manually**: DTO files in `src/Frontend/src/api/generated/` are auto-compiled from C# records. Modify the C# DTO first, then run `dotnet typegen generate`.
2. **Enforce Vertical Slices**: Backend features live in `src/Backend/src/Tradebook.Api/Features/<FeatureName>/`. Do NOT introduce shared repository patterns across feature slices unless explicitly instructed.
3. **Hermetic Test Isolation**: All database tests must inherit from `DatabaseTestBase` utilizing `CustomWebApplicationFactory` (Testcontainers PostgreSQL 17). Never assume a running host database.
4. **Commits via Wrapper**: Execute git commits strictly using `bin/agent-commit.sh <type> <scope> <summary>`.
5. **Mutation Score Enforcement**: Any backend change must maintain a Stryker mutation score >= 80%.

## Fast Command Reference
- **Build Backend**: `dotnet build src/Backend/Tradebook.sln`
- **Run Backend Tests**: `dotnet test tests/Tradebook.UnitTests/Tradebook.UnitTests.csproj && dotnet test tests/Tradebook.IntegrationTests/Tradebook.IntegrationTests.csproj`
- **Run Architecture Tests**: `dotnet test tests/Tradebook.ArchitectureTests/Tradebook.ArchitectureTests.csproj`
- **Run Stryker Mutation Tests**: `dotnet stryker --config-file stryker-config.json`
- **Generate TS Contracts**: `dotnet typegen generate --project-folder .`
- **Build Frontend**: `npm --prefix src/Frontend run build`
- **Run Frontend Tests**: `npm --prefix src/Frontend test`
- **Check ESLint Boundaries**: `npm --prefix src/Frontend run lint`

## Navigation Map
- Master Architecture: `architecture/master-architecture-blueprint.md`
- Backend Guidelines: `src/Backend/AGENTS.md`
- Frontend Guidelines: `src/Frontend/AGENTS.md`
- Infrastructure Guidelines: `infra/AGENTS.md`
```

### 6.3 Gemini-Specific System Directives Map (`GEMINI.md`)

```markdown
# Gemini System Directives (`GEMINI.md`)

This file specifies operational directives for Gemini AI subagents executing tasks within Tradebook.

## Directives
1. **Integrity First**: Do NOT generate mock test passes (`Assert.True(true)`), hardcode test outputs, or create dummy facade implementations.
2. **Context Awareness**: Always re-read `BRIEFING.md` and `progress.md` before initiating tool actions.
3. **Execution Verification**: Immediately run the relevant build, test, and lint commands after editing any source file.
4. **Handoff Quality**: Handoff reports (`handoff.md`) must contain verbatim command outputs and a 5-component structure (Observation, Logic Chain, Caveats, Conclusion, Verification Method).
```

### 6.4 Module Context Maps

#### `src/Backend/AGENTS.md`
```markdown
# Backend Architectural Guide (`src/Backend/AGENTS.md`)

- Framework: .NET 9 Web API using FastEndpoints (REPR pattern: Request, Endpoint, Response).
- Feature Slices: `src/Backend/src/Tradebook.Api/Features/<FeatureName>/` (e.g. `PhysicalDeliveries/`, `MarketPrices/`, `Auth/`).
- Data Access: Dapper over Npgsql in `Tradebook.Infrastructure`; schema migrations are plain SQL owned by Task 01 (`src/Database/Migrations`).
- Bi-Temporal Triggers: Database triggers maintain `audit_log` records automatically.
```

#### `src/Frontend/AGENTS.md`
```markdown
# Frontend Architectural Guide (`src/Frontend/AGENTS.md`)

- Framework: React 19 SPA + Vite + TanStack Query v5 + Zustand.
- Types: All API DTO interfaces imported exclusively from `src/types/generated/`.
- Network Mocking: Vitest tests use MSW 2.0 handlers from `src/mocks/handlers.ts`.
- Architecture: Feature-Sliced Design. Cross-feature imports are blocked by ESLint boundaries.
```

#### `infra/AGENTS.md`
```markdown
# Infrastructure Architectural Guide (`infra/AGENTS.md`)

- Developer Stack: `docker-compose.yml` boots plain PostgreSQL 17 only (D9).
- IaC Engine: Terraform 1.9+ HCL modules under `infra/terraform/`.
- Verification: `terraform fmt -check`, `terraform validate`, `tflint`.
```

### 6.5 ESLint Feature-Sliced Architectural Boundary Linting (`.eslintrc.cjs`)

```javascript
// File: src/Frontend/.eslintrc.cjs
module.exports = {
  root: true,
  env: { browser: true, es2022: true },
  parser: '@typescript-eslint/parser',
  plugins: ['@typescript-eslint', 'boundaries'],
  settings: {
    'boundaries/elements': [
      { type: 'ui', pattern: 'src/components/ui/*' },
      { type: 'feature', pattern: 'src/features/*' },
      { type: 'store', pattern: 'src/store/*' },
      { type: 'types', pattern: ['src/types/*', 'src/api/*'] },
      { type: 'lib', pattern: 'src/lib/*' }
    ]
  },
  rules: {
    'boundaries/entry-point': [
      2,
      { default: 'disallow', rules: [{ target: 'ui', allow: 'index.ts' }] }
    ],
    'boundaries/element-types': [
      2,
      {
        default: 'disallow',
        rules: [
          { from: 'feature', allow: ['ui', 'store', 'types', 'lib'] },
          { from: 'ui', allow: ['types', 'lib'] },
          { from: 'store', allow: ['types', 'lib'] },
          { from: 'lib', allow: ['types'] }
        ]
      }
    ]
  }
};
```

---

## 7. Step-by-Step Subagent Implementation Workflow

### Phase 1: Conventional Commits & Release Automation Setup
1. Create `.commitlintrc.json` in monorepo root with exact scope rules.
2. Create `.releaserc.json` in monorepo root for automated semantic releasing.
3. Create `bin/agent-commit.sh`, execute `chmod +x bin/agent-commit.sh`, and test commit validation.

### Phase 2: TypeGen Contract Pipeline Integration
1. Create `tgconfig.json` in monorepo root pointing output to `./src/Frontend/src/api/generated`.
2. Update `src/Backend/src/Tradebook.Api/Tradebook.Api.csproj` with the `GenerateTypeScriptContracts` post-build target.
3. Execute `dotnet build` and confirm TypeScript interface generation.
4. Add GitHub Actions workflow `.github/workflows/verify-contracts.yml`.

### Phase 3: Hermetic Test Harness & Stryker Configuration
1. Add `Testcontainers.PostgreSql`, `Respawn`, and `Microsoft.AspNetCore.Mvc.Testing` NuGet references.
2. Create `tests/Tradebook.IntegrationTests/Fixtures/CustomWebApplicationFactory.cs` and `DatabaseTestBase.cs` (boot `postgres:17`, apply the real Task 01 migrations from `src/Database/Migrations`).
3. Create MSW 2.0 handlers `src/Frontend/src/mocks/handlers.ts` and `server.ts`.
4. Create `stryker-config.json` enforcing the single break threshold of 80.
5. Create `tests/Tradebook.ArchitectureTests` with the three ArchUnitNET boundary rules (§5.7).

### Phase 4: Context Map & ESLint Boundary Enforcement
1. Create root `AGENTS.md` and `GEMINI.md`.
2. Create sub-directory guides `src/Backend/AGENTS.md`, `src/Frontend/AGENTS.md`, and `infra/AGENTS.md`.
3. Create `src/Frontend/.eslintrc.cjs` with `eslint-plugin-boundaries` configuration.

### Phase 5: CI/CD Pipeline Synthesis & End-to-End Verification
1. Run local verification suite: build, unit tests, Stryker mutation test, typegen contract check, and ESLint check.
2. Verify zero git drift and zero linter errors.

---

## 8. Comprehensive Test Plan & SLA Matrix

### 8.1 Testing Strategy & Matrix

| Test Suite | Purpose | Target Framework | SLA / Success Criteria |
| :--- | :--- | :--- | :--- |
| **TS-01: Commit Linting** | Assert commit message formatting | Commitlint CLI | Fails invalid scopes; passes valid conventional messages |
| **TS-02: Contract Generation** | Verify C# DTOs convert to valid TS interfaces | TypeGen CLI | 100% type match; zero drift in CI |
| **TS-03: Hermetic DB Reset** | Verify Testcontainers + Respawn performance | xUnit / Testcontainers | Database table truncation executes in < 10ms |
| **TS-04: Stryker Mutation** | Validate unit test quality against mutated binary logic | Stryker.NET CLI | Mutation Score >= 80% (Build fails if < 80%) |
| **TS-05: ESLint Boundaries** | Prevent invalid cross-feature component imports | ESLint / Boundaries | Zero architectural boundary violations |
| **TS-06: Architecture Boundaries** | Enforce layer & slice dependency rules | ArchUnitNET / xUnit | All 3 boundary rules green; violations fail the build |

### 8.2 Unit & Integration Test Implementation Examples

```csharp
// File: tests/Tradebook.UnitTests/TypeGenContractTests.cs
namespace Tradebook.UnitTests;

using System.IO;
using Xunit;

public class TypeGenContractTests
{
    [Fact]
    public void TypeGen_GeneratedFiles_ShouldExist_AndContain_ExpectedTypes()
    {
        var targetFile = Path.Combine(Directory.GetCurrentDirectory(), "../../../../../src/Frontend/src/api/generated/create-physical-delivery-request.ts");
        
        // Assert file generation
        Assert.True(File.Exists(targetFile), $"Expected generated file at {targetFile}");

        var content = File.ReadAllText(targetFile);
        Assert.Contains("export interface CreatePhysicalDeliveryRequest", content);
        Assert.Contains("export type BookType", content);
        Assert.Contains("supplyMonth: string;", content);
    }
}
```

```csharp
// File: tests/Tradebook.UnitTests/StrykerScoreVerificationTests.cs
namespace Tradebook.UnitTests;

using System.IO;
using System.Text.Json;
using Xunit;

public class StrykerScoreVerificationTests
{
    [Fact]
    public void StrykerConfigFile_ShouldEnforce_80PercentBreakThreshold()
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "../../../../../stryker-config.json");
        Assert.True(File.Exists(configPath), "stryker-config.json must exist in root");

        var json = File.ReadAllText(configPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.GetProperty("stryker-config");
        var thresholds = root.GetProperty("thresholds");
        
        int breakThreshold = thresholds.GetProperty("break").GetInt32();
        Assert.True(breakThreshold >= 80, $"Stryker break threshold must be >= 80%, found {breakThreshold}%");
    }
}
```

---

## 9. Independent Verification & Acceptance Workflow

To guarantee system integrity, an independent auditor will execute the following verification steps:

### Step 1: Commitlint Verification
```bash
echo "feat(backend/ledger): add bi-temporal audit entry point" | npx commitlint
# Result: PASS (exit code 0)

echo "invalid_commit: missing scope" | npx commitlint
# Result: FAIL (exit code 1)
```

### Step 2: TypeGen Contract & Zero-Drift Verification
```bash
dotnet build src/Backend/Tradebook.sln -c Debug
dotnet typegen generate --project-folder .
git status --porcelain src/Frontend/src/api/generated
# Result: Empty output (Zero contract drift)
```

### Step 3: Hermetic Test Execution & Sub-10ms Reset Check
```bash
dotnet test tests/Tradebook.IntegrationTests/Tradebook.IntegrationTests.csproj --filter "FullyQualifiedName~DatabaseTestBase"
# Result: 100% Passed (Database table truncation < 10ms)
```

### Step 4: Stryker.NET Mutation Score Enforcer
```bash
dotnet stryker --config-file stryker-config.json
# Result: Mutation score >= 80% (Exit code 0)
```

### Step 5: Frontend ESLint Boundary Check
```bash
npm --prefix src/Frontend run lint
# Result: 0 errors, 0 warnings
```

### Step 6: Architecture Boundary Tests
```bash
dotnet test tests/Tradebook.ArchitectureTests/Tradebook.ArchitectureTests.csproj
# Result: 3/3 ArchUnitNET boundary rules green
```

---

## 10. Anti-Cheating & Integrity Guardrails

### 10.1 Prohibited Shortcuts & Facade Implementations
Subagents and implementers are strictly forbidden from committing any of the following anti-patterns:
1. ❌ **Mock Assertions**: Writing unit tests with `Assert.True(true)` or `Assert.NotNull(new object())` to pad test count.
2. ❌ **Lowering Stryker Thresholds**: Modifying `stryker-config.json` break score below 80% to force CI to pass.
3. ❌ **Manual TS Editing**: Hand-editing TypeScript files in `src/Frontend/src/api/generated/` instead of deriving them from C# models.
4. ❌ **Ignored Boundary Warnings**: Disabling `@typescript-eslint` or `boundaries/element-types` rules with `// eslint-disable`.
5. ❌ **Host DB Dependencies**: Writing backend integration tests that require an already-running PostgreSQL database on `localhost:5432` rather than `Testcontainers`.

### 10.2 Forensic Auditor Verification Rules
The independent auditor agent will verify:
- Actual execution of `dotnet stryker` with real binary mutation log output.
- Automatic deletion of generated TS contracts followed by `dotnet build` to confirm regeneration.
- Inspection of `CustomWebApplicationFactory.cs` to ensure real Docker container lifecycle methods are present.

---
*End of Task 08 Specification.*
