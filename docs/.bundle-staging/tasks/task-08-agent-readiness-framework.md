# Task 08: Autonomous AI Agent-Readiness Tooling, Contract Generation & Testing Infrastructure

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
  - `tests/Tradebook.Tests/Fixtures/CustomWebApplicationFactory.cs`
  - `tests/Tradebook.Tests/Fixtures/DatabaseTestBase.cs`
  - `src/frontend/src/mocks/handlers.ts`
  - `src/frontend/src/mocks/server.ts`

---

## 1. Executive Overview & Objectives

### 1.1 Executive Summary
Task 08 establishes comprehensive **Agent-Readiness & Governance Framework** for Tradebook monorepo (.NET 9 Web API + PostgreSQL 17 + React 19 SPA). Autonomous AI coding agents need deterministic feedback loops, strict type boundaries, hermetic test execution, structured context maps, automated lint/release guardrails to operate safely without human supervision.

Task delivers 5 fundamental pillars of agent governance:
1. **Conventional Commits 1.0.0 & Automated Release Management**: machine-parseable commit message structures enforced by `.commitlintrc.json`, automated semantic versioning via `.releaserc.json`, validated commit helper wrapper script `bin/agent-commit.sh`.
2. **Automated C# to TypeScript TypeGen Pipeline**: zero-drift contract generation from backend C# DTO records to frontend TypeScript interfaces, MSBuild post-build compilation, GitHub Actions zero-drift CI enforcement.
3. **Hermetic Test Fixtures & Stryker.NET Mutation Testing**: total test isolation using `Testcontainers` PostgreSQL 17, sub-10ms table resets via `Respawn`, MSW 2.0 network mockers for React 19, Stryker.NET mutation score break thresholds (>=80%).
4. **Master Context Map System & ESLint Boundary Enforcement**: AI navigation guides (`AGENTS.md`, `GEMINI.md`, sub-directory context maps), ESLint `eslint-plugin-boundaries` rule sets to prevent architecture corruption.
5. **Step-by-step Subagent Execution & Independent Verification**: precise workflows, test plans, quantitative SLA verification commands to prevent shortcuts/facade code.

### 1.2 Core System Objectives
1. **Machine-Readable Git History**: enforce strict Conventional Commit formatting to automate CHANGELOG generation and semantic release versioning.
2. **Zero Type Drift**: eliminate manual interface maintenance between .NET 9 FastEndpoints DTOs and React 19 TypeScript interfaces via automated code generation.
3. **Hermetic Test Reliability**: eliminate flaky tests from dirty database state or missing external dependencies via containerized ephemerality and MSW mocking.
4. **Mutated Logic Assertions**: enforce mandatory Stryker.NET mutation score threshold >=80% on backend slices to prevent fake/tautological unit tests (`Assert.True(true)`).
5. **AI Navigation Ergonomics**: maintain multi-level `AGENTS.md` context maps providing direct commands, directory rules, architecture blueprints for autonomous agents.

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
  - `TypeGen` (v5.0+)
  - `Testcontainers.PostgreSql` (v3.10+)
  - `Respawn` (v6.2+)
  - `Microsoft.AspNetCore.Mvc.Testing` (v9.0+)
  - `Stryker.CLI` (v4.0+)
- **npm Packages**:
  - `@commitlint/cli`, `@commitlint/config-conventional`
  - `@semantic-release/changelog`, `@semantic-release/exec`, `@semantic-release/git`
  - `msw` (v2.0+)
  - `eslint-plugin-boundaries`

### 2.2 Scope Boundaries
- **In-Scope**:
  - Provision `.commitlintrc.json`, `.releaserc.json`, `bin/agent-commit.sh`.
  - Set up `TypeGen` C#-to-TypeScript generation pipeline (`tgconfig.json`, MSBuild post-build target, GitHub Actions `.github/workflows/verify-contracts.yml`).
  - Build `CustomWebApplicationFactory` (Testcontainers PostgreSQL 17) and `DatabaseTestBase` (Respawn resets).
  - Set up MSW 2.0 network handlers and mock server for frontend unit/integration testing.
  - Configure Stryker.NET (`stryker-config.json`) with enforced mutation score thresholds.
  - Create master context maps (`AGENTS.md`, `GEMINI.md`, `backend/AGENTS.md`, `frontend/AGENTS.md`, `infra/AGENTS.md`).
  - Configure ESLint module boundaries (`.eslintrc.cjs`).
  - Full test plan and automated verification steps.
- **Out-of-Scope**:
  - Domain feature logic or CRUD endpoints (covered Task 02, Task 05).
  - Terraform cloud infrastructure setup (covered Task 07).
  - E2E Playwright test scenarios (covered Task 09).

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
│ React 19 UI & Stores  │             │ Playwright & NBomber  │
└───────────────────────┘             └───────────────────────┘
```

---

## 3. Conventional Commits 1.0.0 & Semantic Release Infrastructure

### 3.1 Conventional Commits 1.0.0 Specification & Monorepo Scope Registry
AI agents commit automatically during multi-step tasks. To maintain release hygiene, all commits must adhere Conventional Commits 1.0.0:
```text
<type>(<scope>): <short summary in lower-case imperative mood>

[optional body describing technical rationale and logic chain]

[optional footer(s), e.g. BREAKING CHANGE: <description>]
```

#### Monorepo Allowed Scope Registry
To prevent scope fragmentation, scopes strictly constrained to physical monorepo boundaries:

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

Autonomous agents execute commits via wrapper script, validates message format using `commitlint` before invoking `git commit`.

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
To prevent contract drift between .NET 9 Web API backend and React 19 frontend, `TypeGen` reflects directly on compiled C# DTO record assemblies, exports strictly typed TypeScript interface files.

```
   +-------------------------------------------------------+
   | C# DTO Records (.NET 9 FastEndpoints)                 |
   | src/Backend/Features/Trades/CreateTradeEndpoint.cs    |
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
   | src/Frontend/src/types/generated/create-trade.ts     |
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
  "outputPath": "./src/Frontend/src/types/generated",
  "clearOutputDirectory": false,
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
    "./src/Backend/Tradebook.Api/bin/Debug/net9.0/Tradebook.Api.dll"
  ]
}
```

### 4.3 Backend FastEndpoints REPR C# DTO Example

```csharp
// File: src/Backend/Features/PhysicalDeliveries/CreatePhysicalDeliveryModels.cs
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

Add following post-build target block to `src/Backend/Tradebook.Api/Tradebook.Api.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="FastEndpoints" Version="5.30.0" />
    <PackageReference Include="TypeGen" Version="5.3.0" />
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
// File: src/Frontend/src/types/generated/create-physical-delivery-request.ts
// Auto-generated by TypeGen - DO NOT EDIT DIRECTLY

export enum BookType {
  Sourcing = "Sourcing",
  Sales = "Sales",
  Intercompany = "Intercompany",
}

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
          dotnet tool install -g TypeGen
          dotnet build src/Backend/Tradebook.sln -c Debug

      - name: Generate TypeScript Contracts
        run: |
          dotnet typegen generate --project-folder .

      - name: Assert Zero Git Drift
        run: |
          git status --porcelain src/Frontend/src/types/generated
          if [ -n "$(git status --porcelain src/Frontend/src/types/generated)" ]; then
            echo "::error::Uncommitted TypeScript contract changes detected!"
            echo "Run 'dotnet typegen generate' locally and commit the generated types in src/Frontend/src/types/generated."
            git diff src/Frontend/src/types/generated
            exit 1
          fi
          echo "SUCCESS: Zero contract drift verified."
```

---

## 5. Hermetic Test Fixtures & Stryker.NET Mutation Testing

### 5.1 Hermetic Testing Architecture
Autonomous agents frequently corrupt shared test databases or rely on external third-party services, causing false failures. Hermetic testing guarantees:
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
// File: tests/Tradebook.Tests/Fixtures/CustomWebApplicationFactory.cs
namespace Tradebook.Tests.Fixtures;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Xunit;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
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
            SchemasToInclude = new[] { "public" },
            TablesToIgnore = new[] { new Respawn.Graph.Table("__EFMigrationsHistory") }
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
        var migrationSql = @"
            CREATE EXTENSION IF NOT EXISTS ""uuid-ossp"";
            CREATE TABLE IF NOT EXISTS physical_deliveries (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                contract_id UUID NOT NULL,
                contract_instance_id VARCHAR(128) NOT NULL,
                book_type VARCHAR(32) NOT NULL,
                supply_month DATE NOT NULL,
                volume_nominated_mwh NUMERIC(18, 4),
                volume_realised_mwh NUMERIC(18, 4),
                price_eur_mwh NUMERIC(18, 4)
            );";

        await using var cmd = new NpgsqlCommand(migrationSql, conn);
        await cmd.ExecuteNonQueryAsync();
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
// File: tests/Tradebook.Tests/Fixtures/DatabaseTestBase.cs
namespace Tradebook.Tests.Fixtures;

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
import { CreatePhysicalDeliveryResponse, BookType } from '../types/generated/create-physical-delivery-request';

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
        bookType: BookType.Sourcing,
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

Unit test line coverage (e.g. 90%) can be spoofed by AI agents writing tests with zero assertions. Stryker.NET injects artificial bugs (mutants) into compiled binaries to confirm test suites fail when logic altered.

#### Stryker Configuration (`stryker-config.json`)

```json
{
  "$schema": "https://raw.githubusercontent.com/stryker-mutator/stryker-net/master/src/Stryker.Core/Stryker.Core/ConfigFile/stryker-config.schema.json",
  "stryker-config": {
    "solution": "src/Backend/Tradebook.sln",
    "project": "Tradebook.Api.csproj",
    "test-projects": ["../../../tests/Tradebook.Tests/Tradebook.Tests.csproj"],
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

### 5.6 Module Mutation Score Threshold Matrix

| Monorepo Module | Minimum Line Coverage | Stryker Break Threshold | Rationale |
| :--- | :--- | :--- | :--- |
| **Audit Ledger & Accounting** | >= 95% | **85%** | Immutable bi-temporal data loss or sign error is catastrophic |
| **Auth & Security** | >= 90% | **80%** | JWT verification, RLS policies, token validation logic |
| **CRUD Slices & REST API** | >= 80% | **75%** | Standard API handling and validation pipelines |
| **Frontend UI Components** | >= 75% | **65%** | UI rendering micro-interactions and layout state |

---

## 6. Master Context Map System & Static Boundary Enforcement

### 6.1 Context Map Hierarchy & Architecture
To guide autonomous agents quickly to relevant rules/code paths, hierarchical context map system deployed:
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
1. **Never Edit Generated Contracts Manually**: DTO files in `src/Frontend/src/types/generated/` are auto-compiled from C# records. Modify the C# DTO first, then run `dotnet typegen generate`.
2. **Enforce Vertical Slices**: Backend features live in `src/Backend/Features/<FeatureName>/`. Do NOT introduce shared repository patterns across feature slices unless explicitly instructed.
3. **Hermetic Test Isolation**: All database tests must inherit from `DatabaseTestBase` utilizing `CustomWebApplicationFactory` (Testcontainers PostgreSQL 17). Never assume a running host database.
4. **Commits via Wrapper**: Execute git commits strictly using `bin/agent-commit.sh <type> <scope> <summary>`.
5. **Mutation Score Enforcement**: Any backend change must maintain a Stryker mutation score >= 80%.

## Fast Command Reference
- **Build Backend**: `dotnet build src/Backend/Tradebook.sln`
- **Run Backend Tests**: `dotnet test tests/Tradebook.Tests/Tradebook.Tests.csproj`
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
- Feature Slices: `src/Backend/Features/<FeatureName>/` (e.g. `Trades/`, `Ledger/`, `Auth/`).
- Data Access: Dapper for high-speed queries, EF Core 9 for relational migrations.
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

- Developer Stack: `docker-compose.yml` boots PostgreSQL 17, Redis 7, NATS JetStream, LocalStack.
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
      { type: 'types', pattern: 'src/types/*' },
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
3. Create `bin/agent-commit.sh`, execute `chmod +x bin/agent-commit.sh`, test commit validation.

### Phase 2: TypeGen Contract Pipeline Integration
1. Create `tgconfig.json` in monorepo root pointing output to `./src/Frontend/src/types/generated`.
2. Update `src/Backend/Tradebook.Api/Tradebook.Api.csproj` with `GenerateTypeScriptContracts` post-build target.
3. Execute `dotnet build` and confirm TypeScript interface generation.
4. Add GitHub Actions workflow `.github/workflows/verify-contracts.yml`.

### Phase 3: Hermetic Test Harness & Stryker Configuration
1. Add `Testcontainers.PostgreSql`, `Respawn`, `Microsoft.AspNetCore.Mvc.Testing` NuGet references.
2. Create `tests/Tradebook.Tests/Fixtures/CustomWebApplicationFactory.cs` and `DatabaseTestBase.cs`.
3. Create MSW 2.0 handlers `src/Frontend/src/mocks/handlers.ts` and `server.ts`.
4. Create `stryker-config.json` enforcing break threshold >= 80%.

### Phase 4: Context Map & ESLint Boundary Enforcement
1. Create root `AGENTS.md` and `GEMINI.md`.
2. Create sub-directory guides `src/Backend/AGENTS.md`, `src/Frontend/AGENTS.md`, `infra/AGENTS.md`.
3. Create `src/Frontend/.eslintrc.cjs` with `eslint-plugin-boundaries` configuration.

### Phase 5: CI/CD Pipeline Synthesis & End-to-End Verification
1. Run local verification suite: build, unit tests, Stryker mutation test, typegen contract check, ESLint check.
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

### 8.2 Unit & Integration Test Implementation Examples

```csharp
// File: tests/Tradebook.Tests/TypeGenContractTests.cs
namespace Tradebook.Tests;

using System.IO;
using Xunit;

public class TypeGenContractTests
{
    [Fact]
    public void TypeGen_GeneratedFiles_ShouldExist_AndContain_ExpectedTypes()
    {
        var targetFile = Path.Combine(Directory.GetCurrentDirectory(), "../../../../../src/Frontend/src/types/generated/create-physical-delivery-request.ts");
        
        // Assert file generation
        Assert.True(File.Exists(targetFile), $"Expected generated file at {targetFile}");

        var content = File.ReadAllText(targetFile);
        Assert.Contains("export interface CreatePhysicalDeliveryRequest", content);
        Assert.Contains("export enum BookType", content);
        Assert.Contains("supplyMonth: string;", content);
    }
}
```

```csharp
// File: tests/Tradebook.Tests/StrykerScoreVerificationTests.cs
namespace Tradebook.Tests;

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

To guarantee system integrity, independent auditor executes following verification steps:

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
git status --porcelain src/Frontend/src/types/generated
# Result: Empty output (Zero contract drift)
```

### Step 3: Hermetic Test Execution & Sub-10ms Reset Check
```bash
dotnet test tests/Tradebook.Tests/Tradebook.Tests.csproj --filter "FullyQualifiedName~DatabaseTestBase"
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

---

## 10. Anti-Cheating & Integrity Guardrails

### 10.1 Prohibited Shortcuts & Facade Implementations
Subagents/implementers strictly forbidden from committing following anti-patterns:
1. ❌ **Mock Assertions**: writing unit tests with `Assert.True(true)` or `Assert.NotNull(new object())` to pad test count.
2. ❌ **Lowering Stryker Thresholds**: modifying `stryker-config.json` break score below 80% to force CI pass.
3. ❌ **Manual TS Editing**: hand-editing TypeScript files in `src/Frontend/src/types/generated/` instead of deriving from C# models.
4. ❌ **Ignored Boundary Warnings**: disabling `@typescript-eslint` or `boundaries/element-types` rules with `// eslint-disable`.
5. ❌ **Host DB Dependencies**: writing backend integration tests requiring already-running PostgreSQL database on `localhost:5432` rather than `Testcontainers`.

### 10.2 Forensic Auditor Verification Rules
Independent auditor agent verifies:
- Actual execution of `dotnet stryker` with real binary mutation log output.
- Automatic deletion of generated TS contracts followed by `dotnet build` to confirm regeneration.
- Inspection of `CustomWebApplicationFactory.cs` to ensure real Docker container lifecycle methods present.

---
*End of Task 08 Specification.*
