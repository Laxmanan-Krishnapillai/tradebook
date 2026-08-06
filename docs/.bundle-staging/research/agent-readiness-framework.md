# Agent-Readiness Research & Engineering Framework

**Document Version**: 1.0.0
**Target Architecture**: .NET 9 Web API (FastEndpoints REPR) + PostgreSQL 17 + React 19 CSR SPA + Terraform IaC
**Author**: Agent-Readiness Framework Author
**Date**: August 2026
**Status**: Authoritative Open-Source Framework Specification

---

## 1. Executive Summary & 5 Pillars of Agent Readiness

### 1.1 Shift from Human Ergonomics to Agent-First Ergonomics
Traditional dev tools/workflows optimize for human cognition — visual feedback, permissive compiler flags, interactive debuggers, informal commit messages. When development driven by **autonomous AI coding agents** (Gemini, Claude, dedicated subagent pipelines), human ergonomics fail to give deterministic feedback needed for error-free generation.

AI agents excel under **Agent-First Ergonomics**:
- **Deterministic Feedback Loops**: fast, machine-parseable static analysis/type checking/test output notifying agents of regressions immediately.
- **Contract Enforcement**: strict automated schema sync between backend/frontend, eliminating ambient runtime type errors.
- **Hermetic Isolation**: isolated, side-effect-free execution environments — tests pass/fail solely from code changes, not external state.
- **Context Clarity**: explicit context maps (`AGENTS.md`, `GEMINI.md`) bounding agent reasoning, preventing hallucinated architectural imports.
- **Reproducible Infrastructure**: IaC with compile-time validation rules and local containerized runtime defs.

### 1.2 5 Pillars of Agent Readiness
1. **Conventional Commits 1.0.0 & Monorepo Semantic Release Pipeline**: machine-parsable git histories, strict scope enforcement, automated semver.
2. **Automated Type-Safety Contract Generation**: zero-drift C# REPR DTO → TypeScript interface compilation via TypeGen + FastEndpoints OpenAPI.
3. **Hermetic Test Fixtures & Stryker Mutation Testing Guardrails**: disposable Testcontainers PostgreSQL 17, sub-10ms Respawn resets, MSW 2.0 frontend network mocks, Stryker mutation score gates.
4. **Modular Component Boundaries & AI Context Map System**: ESLint boundary enforcement, root `AGENTS.md` context maps, `GEMINI.md` system directives.
5. **Deterministic Infrastructure as Code & Local Docker Compose Environment**: self-contained docker-compose stack with PostgreSQL 17 extensions, fail-fast Terraform HCL validation.

---

## 2. Conventional Commits 1.0.0 & Monorepo Semantic Release Pipeline

### 2.1 Commit Standard & Monorepo Scope Registry
Autonomous agents need structured git commit conventions for automated changelog generation and semantic release triggers. All commits follow **Conventional Commits 1.0.0**:

```text
<type>(<scope>): <short summary in imperative mood>

[optional body providing rationale and evidence chain]

[optional footer(s), e.g., BREAKING CHANGE: <description> or Refs: #123]
```

#### Allowed Commit Types
- `feat`: new end-user or API feature.
- `fix`: bug fix.
- `docs`: documentation/context map updates.
- `style`: formatting, missing semi-colons, whitespace (no logic change).
- `refactor`: restructuring without bug fixes/new features.
- `perf`: performance improvements.
- `test`: adding/correcting tests.
- `build`: build system or external dependency changes.
- `ci`: CI config files/scripts.
- `chore`: other changes not touching src/test files.

#### Monorepo Scope Registry Matrix
Allowed scopes mapped directly to physical monorepo boundaries:

| Category | Scope | Description |
| :--- | :--- | :--- |
| **Backend** | `backend/core` | Core API setup, middleware, dependency injection |
| | `backend/auth` | Authentication, authorization, JWT, session handling |
| | `backend/ledger` | Double-entry ledger, temporal audit tables, accounting engine |
| | `backend/cqrs` | FastEndpoints command/query handlers and mediator patterns |
| | `backend/jobs` | Background task worker, Quartz/Hangfire jobs |
| **Frontend** | `frontend/ui` | Shared React 19 UI component library (`components/ui`) |
| | `frontend/kanban` | Snappy CRUD Kanban board & drag-and-drop state |
| | `frontend/canvas` | Custom visualization canvas & ECharts modules |
| | `frontend/store` | Zustand / TanStack Query client state management |
| | `frontend/routes` | React Router / TanStack Router page slices |
| **Contracts** | `contracts` | Generated TypeScript DTOs and API interfaces |
| **Infra** | `infra/docker` | Local Docker Compose & container configurations |
| | `infra/tf` | Terraform cloud infrastructure modules |
| | `infra/db` | PostgreSQL 17 migrations, DDL scripts, extensions |
| **DevEx & CI** | `ci` | GitHub Actions workflows and CI automation |
| | `deps` | Dependency version bumps |
| | `docs` | Project documentation, `AGENTS.md`, `GEMINI.md` |
| | `repo` | Root workspace config, editor settings, scripts |

### 2.2 Commitlint Configuration (`.commitlintrc.json`)
```json
{
  "extends": ["@commitlint/config-conventional"],
  "rules": {
    "type-enum": [
      2,
      "always",
      [
        "build",
        "ci",
        "chore",
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
    "subject-full-stop": [2, "never", "."],
    "header-max-length": [2, "always", 100]
  }
}
```

### 2.3 Semantic Release Pipeline (`.releaserc.json`)
Automated semantic versioning analyzes commits on `main`, bumps version tags, builds release notes, updates `CHANGELOG.md`.

```json
{
  "branches": [
    "main",
    {
      "name": "beta",
      "prerelease": true
    }
  ],
  "plugins": [
    [
      "@semantic-release/commit-analyzer",
      {
        "preset": "conventionalcommits",
        "releaseRules": [
          { "type": "docs", "scope": "docs", "release": "patch" },
          { "type": "refactor", "release": "patch" },
          { "type": "perf", "release": "minor" },
          { "type": "feat", "release": "minor" },
          { "type": "fix", "release": "patch" }
        ]
      }
    ],
    [
      "@semantic-release/release-notes-generator",
      {
        "preset": "conventionalcommits",
        "presetConfig": {
          "types": [
            { "type": "feat", "section": "Features" },
            { "type": "fix", "section": "Bug Fixes" },
            { "type": "perf", "section": "Performance Improvements" },
            { "type": "refactor", "section": "Code Refactoring" },
            { "type": "docs", "section": "Documentation" }
          ]
        }
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
        "prepareCmd": "dotnet build backend/Tradebook.sln -c Release /p:Version=${nextRelease.version}"
      }
    ],
    [
      "@semantic-release/git",
      {
        "assets": [
          "CHANGELOG.md",
          "Directory.Build.props"
        ],
        "message": "chore(release): ${nextRelease.version} [skip ci]\n\n${nextRelease.notes}"
      }
    ],
    "@semantic-release/github"
  ]
}
```

### 2.4 Agent Commit Helper Script (`bin/agent-commit.sh`)
Ensures autonomous AI agents never generate invalid commit strings that fail CI git hooks:

```bash
#!/usr/bin/env bash
# bin/agent-commit.sh - Validated Git Commit Wrapper for AI Coding Agents
set -euo pipefail

if [ "$#" -lt 3 ]; then
    echo "Error: Insufficient parameters."
    echo "Usage: ./bin/agent-commit.sh <type> <scope> <summary> [body]"
    echo "Example: ./bin/agent-commit.sh feat backend/ledger 'add double entry validation' 'Implements strict debit-credit balancing logic.'"
    exit 1
fi

TYPE="$1"
SCOPE="$2"
SUMMARY="$3"
BODY="${4:-}"

# Construct standard Conventional Commit message
COMMIT_HEADER="${TYPE}(${SCOPE}): ${SUMMARY}"

if [ -n "$BODY" ]; then
    FULL_COMMIT_MSG="${COMMIT_HEADER}"$'\n\n'"${BODY}"
else
    FULL_COMMIT_MSG="${COMMIT_HEADER}"
fi

# Pre-validate message using commitlint CLI
echo "Validating commit message against .commitlintrc.json..."
if ! echo "$FULL_COMMIT_MSG" | npx commitlint; then
    echo "ERROR: Commit message failed linting rules!"
    exit 1
fi

# Execute git commit
git commit -m "$FULL_COMMIT_MSG"
echo "SUCCESS: Commit created successfully."
```

---

## 3. Automated Type-Safety Contract Generation

### 3.1 C# to TypeScript Tooling Evaluation Matrix
Maintaining type alignment between C# backend (.NET 9 FastEndpoints) and React 19 TypeScript frontend is critical for agent productivity — if an agent modifies a C# DTO without updating frontend types, runtime bugs emerge.

| Strategy / Tool | Generation Mechanism | FastEndpoints Compatibility | Developer / Agent DX | Zero-Drift CI Enforceability |
| :--- | :--- | :--- | :--- | :--- |
| **TypeGen** | CLI / MSBuild Reflection over C# assemblies & DTO records | **High**: Reflects record types directly to TypeScript interfaces | **High**: Instant post-build generation without running API server | **High**: Git porcelain diff check in CI workflow |
| **FastEndpoints OpenAPI + Orval** | Export OpenAPI 3.1 JSON -> Generate TypeScript & React Query hooks | **Native**: Native FastEndpoints Swagger generator | **Very High**: Generates types + client fetchers + React Query hooks | **High**: Requires OpenAPI export step before code generation |
| **TypeSpec (Schema-First)** | Custom `.tsp` specifications emit C# DTOs and TypeScript models | **Medium**: Requires schema definitions outside of C# source code | **Medium**: Extra domain DSL to learn; duplication of logic | **Highest**: Upstream source of truth enforced at compile time |
| **Roslyn AST Source Generators** | Custom Roslyn analyzer reading C# record files during compile | **High**: Tailored directly to repository structure | **Low**: High maintenance overhead for internal compiler tools | **Medium**: Susceptible to edge-case parsing bugs |

### 3.2 Dual-Layer Contract Generation Architecture
1. **TypeGen Layer**: compiles pure C# DTO models/Enums directly to `src/frontend/src/types/generated`.
2. **FastEndpoints OpenAPI + Orval Layer**: emits OpenAPI 3.1 JSON, compiles strongly-typed React Query hooks (`useCreateTrade`, `useGetLedger`).

```text
 +-------------------------------------------------------------------------+
 | .NET 9 Backend Feature Slice DTO (C# Record)                           |
 | src/backend/Features/Trades/CreateTradeEndpoint.cs                      |
 +-------------------------------------------------------------------------+
                                      |
         +----------------------------+----------------------------+
         | (MSBuild Post-Build Target)                             | (Swagger JSON Export)
         v                                                         v
 +---------------------------------------+       +---------------------------------------+
 | TypeGen Assembly Generator            |       | FastEndpoints OpenAPI 3.1 Exporter    |
 | (tgconfig.json)                       |       | (swagger.json)                        |
 +---------------------------------------+       +---------------------------------------+
         |                                                         |
         v                                                         v
 +---------------------------------------+       +---------------------------------------+
 | TypeScript Interface Files            |       | Orval / openapi-typescript            |
 | frontend/src/types/generated/*.ts     |       | frontend/src/api/generated/client.ts  |
 +---------------------------------------+       +---------------------------------------+
         |                                                         |
         +----------------------------+----------------------------+
                                      v
 +-------------------------------------------------------------------------+
 | React 19 Frontend Components & Zustand / TanStack Query Store          |
 | frontend/src/features/trades/CreateTradeForm.tsx                       |
 +-------------------------------------------------------------------------+
```

### 3.3 TypeGen Configuration (`tgconfig.json`)
```json
{
  "outputPath": "../src/frontend/src/types/generated",
  "clearOutputDirectory": false,
  "generateObsoleteAttribute": true,
  "typeUnionsForEnums": true,
  "enumStringInitializers": true,
  "customTypeMappings": {
    "System.Guid": "string",
    "System.DateTime": "string",
    "System.DateTimeOffset": "string",
    "NodaTime.Instant": "string",
    "decimal": "number"
  },
  "csNullableTranslation": "Null",
  "assemblies": [
    "../src/backend/Tradebook.Api/bin/Debug/net9.0/Tradebook.Api.dll"
  ]
}
```

### 3.4 Code Examples: C# REPR Endpoint DTO & Generated TypeScript

#### C# FastEndpoints REPR Endpoint (`CreateTradeEndpoint.cs`)
```csharp
namespace Tradebook.Api.Features.Trades;

using FastEndpoints;

public enum TradeSide
{
    Buy,
    Sell
}

public record CreateTradeRequest(
    Guid AccountId,
    string Symbol,
    decimal Quantity,
    decimal Price,
    TradeSide Side,
    DateTimeOffset ExecutedAt
);

public record CreateTradeResponse(
    Guid TradeId,
    string Status,
    DateTimeOffset CreatedAt
);

public class CreateTradeEndpoint : Endpoint<CreateTradeRequest, CreateTradeResponse>
{
    public override void Configure()
    {
        Post("/api/v1/trades");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Creates a new execution record";
            s.Description = "Ingests trade payloads and appends to temporal audit ledger.";
        });
    }

    public override async Task HandleAsync(CreateTradeRequest req, CancellationToken ct)
    {
        var response = new CreateTradeResponse(
            Guid.NewGuid(),
            "COMPLETED",
            DateTimeOffset.UtcNow
        );
        await SendAsync(response, 200, ct);
    }
}
```

#### Generated TypeScript Interface (`frontend/src/types/generated/create-trade-request.ts`)
```typescript
// Auto-generated by TypeGen - DO NOT EDIT DIRECTLY

export enum TradeSide {
  Buy = "Buy",
  Sell = "Sell",
}

export interface CreateTradeRequest {
  accountId: string;
  symbol: string;
  quantity: number;
  price: number;
  side: TradeSide;
  executedAt: string;
}

export interface CreateTradeResponse {
  tradeId: string;
  status: string;
  createdAt: string;
}
```

### 3.5 Zero-Drift CI Pipeline (`.github/workflows/verify-contracts.yml`)
Ensures any PR modifying backend DTOs without committing updated TypeScript types fails CI immediately:

```yaml
name: Zero-Drift Contract Synchronization

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  verify-contracts:
    name: Assert Backend-Frontend Type Synchronization
    runs-on: ubuntu-latest

    steps:
      - name: Checkout Code repository
        uses: actions/checkout@v4

      - name: Setup .NET 9 SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Setup Node.js 22 Environment
        uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: 'npm'
          cache-dependency-path: 'frontend/package-lock.json'

      - name: Build C# Backend Solution
        run: dotnet build backend/Tradebook.Api/Tradebook.Api.csproj -c Release

      - name: Run TypeGen Contract Generation
        run: |
          cd frontend
          npm ci
          npm run generate-contracts

      - name: Assert Zero Git Drift
        run: |
          IF_DRIFT=$(git status --porcelain frontend/src/types/generated)
          if [ -n "$IF_DRIFT" ]; then
            echo "ERROR: Uncommitted TypeGen TypeScript contract changes detected!"
            echo "Modified contract files:"
            git status --porcelain frontend/src/types/generated
            echo "Please run 'npm run generate-contracts' locally and commit the generated interfaces."
            exit 1
          else
            echo "SUCCESS: TypeGen TypeScript contracts are 100% in sync with .NET 9 DTOs."
          fi
```

---

## 4. Hermetic Test Fixtures & Stryker Mutation Testing Guardrails

### 4.1 Hermetic Testing Architecture
Autonomous agents need tests that execute deterministically without ambient dependency failures.

Strategy combines:
1. **Testcontainers PostgreSQL 17**: ephemeral Docker containers instantiated per test fixture class.
2. **Respawn Table Resets**: high-performance PostgreSQL table truncation, sub-10ms between test cases.
3. **MSW 2.0 (Mock Service Worker)**: network-level HTTP mocking for React 19 integration tests, prevents flaky network dependencies.

```text
 +-----------------------------------------------------------------------------------+
 |                        HERMETIC INTEGRATION TESTING SUITE                         |
 +-----------------------------------------------------------------------------------+
 |                                                                                   |
 |  [Backend C# xUnit Test Suite]                [Frontend React 19 Vitest Suite]    |
 |          |                                                   |                    |
 |          v                                                   v                    |
 |  CustomWebApplicationFactory                      MSW 2.0 Mock Service Worker     |
 |  (Microsoft.AspNetCore.Mvc.Testing)               (Intersects fetch / XHR at network) |
 |          |                                                   |                    |
 |          +-----------------------+                           |                    |
 |          |                       |                           v                    |
 |          v                       v                   Isolated Component Tree      |
 |   Testcontainers PostgreSQL 17  Respawn Table Reset  (Zero real network calls)    |
 |   (Ephemeral Docker Container)  (Sub-10ms truncation)                             |
 |                                                                                   |
 +-----------------------------------------------------------------------------------+
```

### 4.2 C# Integration Test Fixture (`CustomWebApplicationFactory.cs`)
```csharp
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
        .WithDatabase("tradebook_integration_tests")
        .WithUsername("test_admin")
        .WithPassword("test_secret_pass")
        .Build();

    private NpgsqlConnection _dbConnection = default!;
    private Respawner _respawner = default!;

    public async Task InitializeAsync()
    {
        // 1. Start ephemeral PostgreSQL 17 container
        await _dbContainer.StartAsync();

        // 2. Open connection to container DB
        _dbConnection = new NpgsqlConnection(_dbContainer.GetConnectionString());
        await _dbConnection.OpenAsync();

        // 3. Execute initial DDL schema migrations
        await InitializeDatabaseSchemaAsync(_dbConnection);

        // 4. Initialize Respawn instance configured for Postgres
        _respawner = await Respawner.CreateAsync(_dbConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = new[] { "public" },
            WithReseed = true
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await _respawner.ResetAsync(_dbConnection);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", _dbContainer.GetConnectionString());
        builder.UseEnvironment("Testing");
    }

    private static async Task InitializeDatabaseSchemaAsync(NpgsqlConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE EXTENSION IF NOT EXISTS ""uuid-ossp"";
            CREATE TABLE IF NOT EXISTS trades (
                trade_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
                account_id UUID NOT NULL,
                symbol VARCHAR(20) NOT NULL,
                quantity NUMERIC(18,4) NOT NULL,
                price NUMERIC(18,4) NOT NULL,
                side VARCHAR(10) NOT NULL,
                executed_at TIMESTAMPTZ NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT CLOCK_TIMESTAMP()
            );
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    public new async Task DisposeAsync()
    {
        if (_dbConnection != null)
        {
            await _dbConnection.CloseAsync();
            await _dbConnection.DisposeAsync();
        }
        await _dbContainer.DisposeAsync();
    }
}
```

### 4.3 MSW 2.0 Network Mocking for React 19 (`frontend/src/mocks/handlers.ts`)
```typescript
import { http, HttpResponse } from 'msw';
import { CreateTradeRequest, CreateTradeResponse } from '../types/generated/create-trade-request';

export const handlers = [
  http.post('/api/v1/trades', async ({ request }) => {
    const body = (await request.json()) as CreateTradeRequest;

    if (!body.symbol || body.quantity <= 0) {
      return new HttpResponse(JSON.stringify({ error: 'Invalid payload' }), { status: 400 });
    }

    const mockResponse: CreateTradeResponse = {
      tradeId: '11111111-2222-3333-4444-555555555555',
      status: 'COMPLETED',
      createdAt: new Date().toISOString(),
    };

    return HttpResponse.json(mockResponse, { status: 200 });
  }),
];
```

### 4.4 Stryker Mutation Testing Configuration (`stryker-config.json`)
Stryker mutates source code binary trees (e.g. swapping `>` for `<`, `&&` for `||`) to verify tests actually fail when logic is altered.

```json
{
  "$schema": "https://raw.githubusercontent.com/stryker-mutator/stryker-net/master/src/Stryker.Core/Stryker.Core/Schema/stryker-config.json",
  "stryker-config": {
    "solution": "backend/Tradebook.sln",
    "project": "Tradebook.Api.csproj",
    "test-projects": [
      "../Tradebook.Tests/Tradebook.Tests.csproj"
    ],
    "mutation-level": "Standard",
    "concurrency": 4,
    "reporters": [
      "html",
      "progress",
      "cleartext",
      "json"
    ],
    "thresholds": {
      "high": 85,
      "low": 75,
      "break": 80
    },
    "mutate": [
      "Features/**/*.cs",
      "!Features/**/Dto/*.cs",
      "!Migrations/**/*.cs"
    ]
  }
}
```

### 4.5 Monorepo Mutation Threshold Matrix
Different architectural modules enforce strict mutation survival thresholds; a PR dropping below threshold blocks the build.

| Domain Module | Min. Line Coverage | Mutation Score Break Threshold | Rationale & Protection Target |
| :--- | :--- | :--- | :--- |
| **Audit Ledger & Accounting Engine** | **>= 95%** | **85%** | Double-entry accounting & bi-temporal audit trail. Zero tolerance for mutated boolean flags or calculation logic. |
| **Auth & Security Middleware** | **>= 90%** | **80%** | JWT evaluation, Role-Based Access Control, tenant boundary checks. |
| **CRUD Slices & REST Endpoints** | **>= 80%** | **75%** | Business validation rules and API endpoint handlers. |
| **Frontend UI Components** | **>= 75%** | **65%** | Component rendering, state updates, visual micro-interactions. |

---

## 5. Modular Component Boundaries & AI Context Map System

### 5.1 Monorepo Architecture Directory Map & Boundaries
To prevent agents cross-importing private module implementations, the repository enforces strict ESLint boundaries.

```text
c:\Users\LaxmananKrishnapilla\tradebook\
├── AGENTS.md                          # Master AI Navigation & Rulebook
├── GEMINI.md                          # Gemini System Instructions & Directives
├── .commitlintrc.json                 # Commit message validation rules
├── .releaserc.json                    # Monorepo semantic release pipeline
├── docker-compose.yml                 # Local developer/agent container stack
├── bin/
│   └── agent-commit.sh                # Executable agent commit wrapper
├── backend/
│   ├── AGENTS.md                      # Backend Feature Slice Guide
│   ├── Tradebook.sln
│   ├── stryker-config.json
│   └── src/Tradebook.Api/
│       └── Features/                  # FastEndpoints REPR Slices
│           ├── Auth/
│           ├── Ledger/
│           └── Trades/
├── frontend/
│   ├── AGENTS.md                      # Frontend Architectural Map
│   ├── .eslintrc.cjs                  # ESLint Module Boundary Rules
│   ├── tgconfig.json                  # TypeGen C# to TS Configuration
│   └── src/
│       ├── components/ui/             # Shared UI Component Library
│       ├── features/                  # Domain-Specific Feature Slices
│       │   ├── kanban/
│       │   └── trades/
│       └── types/generated/           # Auto-generated TypeGen DTO interfaces
└── infra/
    ├── AGENTS.md                      # Infrastructure Architecture Map
    ├── postgres/
    │   └── init-extensions.sql        # PostgreSQL 17 initialization script
    └── terraform/
        └── modules/
            └── database_pg/           # Modular PostgreSQL 17 IaC
```

### 5.2 Frontend ESLint Boundary Rules (`.eslintrc.cjs`)
```javascript
module.exports = {
  root: true,
  parser: '@typescript-eslint/parser',
  plugins: ['@typescript-eslint', 'boundaries'],
  extends: [
    'eslint:recommended',
    'plugin:@typescript-eslint/recommended',
    'plugin:boundaries/recommended'
  ],
  settings: {
    'boundaries/elements': [
      { type: 'ui', pattern: 'src/components/ui/*' },
      { type: 'feature', pattern: 'src/features/*' },
      { type: 'store', pattern: 'src/store/*' },
      { type: 'lib', pattern: 'src/lib/*' },
      { type: 'types', pattern: 'src/types/*' }
    ],
    'boundaries/ignore': ['**/*.test.ts', '**/*.test.tsx']
  },
  rules: {
    'boundaries/entry-point': [
      2,
      {
        default: 'disallow',
        rules: [
          { target: 'ui', allow: 'index.ts' },
          { target: 'feature', allow: 'index.ts' }
        ]
      }
    ],
    'boundaries/element-types': [
      2,
      {
        default: 'disallow',
        rules: [
          { from: 'feature', allow: ['ui', 'store', 'lib', 'types'] },
          { from: 'ui', allow: ['lib', 'types'] },
          { from: 'store', allow: ['lib', 'types'] },
          { from: 'lib', allow: ['types'] }
        ]
      }
    ]
  }
};
```

### 5.3 Root Master AI Context Map (`AGENTS.md`)
Root `AGENTS.md` acts as primary navigational context for all AI subagents:

```markdown
# Tradebook Master Agent Architecture & Navigation Map (`AGENTS.md`)

Welcome Autonomous AI Agent. This monorepo is engineered specifically for autonomous modification via Agent-First Ergonomics.

## Core Rules of Engagement

1. **Zero-Drift Type Safety**:
   - DO NOT manually edit TypeScript interfaces inside `frontend/src/types/generated/`.
   - Edit the backend C# record DTO in `backend/src/Tradebook.Api/Features/<Feature>/` first.
   - Run `dotnet build` followed by `cd frontend && npm run generate-contracts`.

2. **Backend Vertical Slices (REPR Pattern)**:
   - Backend APIs follow FastEndpoints REPR (Request-Endpoint-Response).
   - Each endpoint slice lives in `backend/src/Tradebook.Api/Features/<Domain>/<EndpointName>.cs`.
   - Do NOT create repository interfaces or cross-slice dependencies unless explicitly specified.

3. **Hermetic Test Execution**:
   - Integration tests MUST extend `CustomWebApplicationFactory`.
   - Tests spin up real PostgreSQL 17 containers via Testcontainers and perform sub-10ms table resets via Respawn.
   - Never assume a persistent local PostgreSQL instance exists during test runs.

4. **Structured Commit Protocol**:
   - Execute commits using `./bin/agent-commit.sh <type> <scope> <summary> [body]`.
   - Valid scopes: `backend/core`, `backend/ledger`, `frontend/ui`, `frontend/kanban`, `contracts`, `infra/tf`, etc.

## Agent Command Quick Reference

| Action | Command | Expected Result |
| :--- | :--- | :--- |
| **Build Backend** | `dotnet build backend/Tradebook.sln` | Compiles .NET 9 API & outputs assembly DLL |
| **Generate TS Contracts** | `cd frontend && npm run generate-contracts` | Runs TypeGen, updates `src/types/generated` |
| **Run Integration Tests** | `dotnet test backend/Tradebook.Tests/Tradebook.Tests.csproj` | Boots Testcontainers PG 17 and verifies endpoints |
| **Run Stryker Mutation** | `dotnet stryker --config-file backend/stryker-config.json` | Asserts >=80% mutation score |
| **Build Frontend SPA** | `cd frontend && npm run build` | Compiles Vite + React 19 bundle |
| **Validate Terraform IaC** | `terraform -chdir=infra/terraform validate` | Validates HCL syntax and module contracts |

## Repository Context File Index
- `architecture/master-architecture-blueprint.md`: Master architectural blueprint & DB DDL schema.
- `research/agent-readiness-framework.md`: Agent-Readiness research framework & CI specs.
- `backend/AGENTS.md`: Detailed C# FastEndpoints slice guide.
- `frontend/AGENTS.md`: React 19 component library & Zustand state machine guide.
- `infra/AGENTS.md`: Terraform IaC modules & Docker Compose stack guide.
```

### 5.4 Root Gemini AI Directives Template (`GEMINI.md`)
`GEMINI.md` defines explicit system-level instructions for Gemini-family agents interacting with the repository:

```markdown
# GEMINI System Directives & Engineering Protocol (`GEMINI.md`)

## Core System Directives

1. **Minimal Touch Principle**:
   - Make the exact code changes necessary to satisfy task requirements.
   - Do NOT perform unrequested formatting, stylistic rewrites, or "while-I-am-here" refactorings.

2. **Self-Verification Mandate**:
   - Always run the relevant build command (`dotnet build` or `npm run build`) immediately after editing files.
   - If a build fails, inspect line errors, apply targeted fixes, and re-test before reporting completion.

3. **Strict Boundary Compliance**:
   - Respect ESLint boundaries in `frontend/src/` (do not import across feature boundaries).
   - Maintain REPR slice boundaries in `backend/src/Tradebook.Api/Features/`.

4. **Integrity Preservation**:
   - Never hardcode test assertions or introduce dummy mocks to force tests to pass.
   - Ensure all database queries and business logic changes are fully verified using hermetic Testcontainers fixtures.
```

---

## 6. Deterministic Infrastructure as Code & Local Docker Compose Environment

### 6.1 Local Developer & Agent Docker Compose Stack (`docker-compose.yml`)
Autonomous agents need a deterministic local stack to execute tests and inspect runtime behavior.

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:17-alpine
    container_name: tradebook_postgres
    restart: unless-stopped
    environment:
      POSTGRES_DB: tradebook_dev
      POSTGRES_USER: tradebook_app
      POSTGRES_PASSWORD: dev_password_123
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./infra/postgres/init-extensions.sql:/docker-entrypoint-initdb.d/01-init-extensions.sql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U tradebook_app -d tradebook_dev"]
      interval: 5s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    container_name: tradebook_redis
    restart: unless-stopped
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 5

  localstack:
    image: localstack/localstack:latest
    container_name: tradebook_localstack
    ports:
      - "4566:4566"
    environment:
      - SERVICES=s3,sqs
      - AWS_DEFAULT_REGION=us-east-1
    volumes:
      - localstack_data:/var/lib/localstack

volumes:
  postgres_data:
  localstack_data:
```

### 6.2 PostgreSQL 17 Initialization Script (`infra/postgres/init-extensions.sql`)
```sql
-- Initialization Script for Tradebook PostgreSQL 17 Database Engine
-- Auto-mounted by docker-compose into /docker-entrypoint-initdb.d/

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "btree_gist";

-- Set timezone to UTC for deterministic timestamp handling across agents
ALTER DATABASE tradebook_dev SET timezone TO 'UTC';
```

### 6.3 Deterministic Terraform Module & Validation Protocol

#### Terraform Module Definition (`infra/terraform/modules/database_pg/main.tf`)
Terraform configs enforce fail-fast validation rules on all input parameters so agents cannot deploy invalid infrastructure configs.

```hcl
terraform {
  required_version = ">= 1.9.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.50"
    }
  }
}

variable "environment" {
  type        = string
  description = "Deployment environment name (dev, staging, production)"

  validation {
    condition     = contains(["dev", "staging", "production"], var.environment)
    error_message = "Environment must be one of: dev, staging, production."
  }
}

variable "db_instance_class" {
  type        = string
  description = "AWS RDS Compute Instance Type for PostgreSQL 17"
  default     = "db.t4g.medium"

  validation {
    condition     = can(regex("^db\\.", var.db_instance_class))
    error_message = "The db_instance_class variable must start with 'db.' (e.g. db.t4g.medium)."
  }
}

variable "allocated_storage_gb" {
  type        = number
  description = "Allocated storage volume size in GB"
  default     = 50

  validation {
    condition     = var.allocated_storage_gb >= 20 && var.allocated_storage_gb <= 2000
    error_message = "Allocated storage must be between 20 GB and 2000 GB."
  }
}

resource "aws_db_instance" "postgres" {
  identifier                  = "tradebook-pg17-${var.environment}"
  engine                      = "postgres"
  engine_version              = "17.0"
  instance_class              = var.db_instance_class
  allocated_storage           = var.allocated_storage_gb
  max_allocated_storage       = 1000
  storage_type                = "gp3"
  multi_az                    = var.environment == "production" ? true : false
  publicly_accessible         = false
  auto_minor_version_upgrade  = true
  deletion_protection         = var.environment == "production" ? true : false
  skip_final_snapshot         = var.environment == "production" ? false : true

  tags = {
    Environment = var.environment
    ManagedBy   = "Terraform"
    Project     = "Tradebook"
  }
}

output "db_endpoint" {
  value       = aws_db_instance.postgres.endpoint
  description = "Database connection endpoint URI"
}

output "db_instance_arn" {
  value       = aws_db_instance.postgres.arn
  description = "Amazon Resource Name for PostgreSQL RDS instance"
}
```

#### Terraform Module Validation Protocol for AI Agents
When an agent modifies/creates Terraform infrastructure modules, it MUST execute this 3-step verification sequence before proposing changes:

1. **Formatting Inspection**:
   ```bash
   terraform -chdir=infra/terraform fmt -check -recursive
   ```
2. **Syntax & Type Contract Validation**:
   ```bash
   terraform -chdir=infra/terraform init -backend=false
   terraform -chdir=infra/terraform validate
   ```
3. **Static Security & Linting Analysis (`tflint`)**:
   ```bash
   tflint --chdir=infra/terraform --recursive
   ```

---

## 7. Synthesis & Operational Workflow Summary

```text
 +-----------------------------------------------------------------------------------+
 |                    AUTONOMOUS AGENT WORKFLOW CYCLE IN MONOREPO                     |
 +-----------------------------------------------------------------------------------+
 |                                                                                   |
 | 1. CONTEXT INITIALIZATION                                                          |
 |    Read AGENTS.md & GEMINI.md for slice boundaries and command workflows.          |
 |                                                                                   |
 | 2. DTO & API IMPLEMENTATION                                                        |
 |    Modify/Create C# FastEndpoints REPR Endpoint in backend/src/Features/<Slice>     |
 |                                                                                   |
 | 3. AUTOMATED CONTRACT COMPILATION                                                  |
 |    Run 'dotnet build' -> 'cd frontend && npm run generate-contracts'               |
 |    Assert zero TypeScript diff drift in frontend/src/types/generated/             |
 |                                                                                   |
 | 4. HERMETIC TESTING & MUTATION VERIFICATION                                        |
 |    Run xUnit with CustomWebApplicationFactory (Testcontainers PG 17 + Respawn)     |
 |    Run Stryker mutation testing and confirm score exceeds module threshold (80%).  |
 |                                                                                   |
 | 5. STANDARDIZED AGENT COMMIT                                                      |
 |    Execute './bin/agent-commit.sh <type> <scope> <summary>' to trigger release.     |
 |                                                                                   |
 +-----------------------------------------------------------------------------------+
```

---
*End of Agent-Readiness Research & Engineering Framework Document.*
