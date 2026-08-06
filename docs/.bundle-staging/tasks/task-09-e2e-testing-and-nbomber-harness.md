# Task 09: Opaque-Box E2E Testing Harness, Playwright UI & NBomber Performance Test Suite

- **Phase**: QA & Performance Engineering
- **Lead / Owner**: Quality Assurance & Performance Lead
- **Complexity**: High
- **Prerequisites**: Task 03 (SignalR Core & NATS JetStream Engine), Task 05 (React 19 Keyboard-First Snappy CRUD UI), Task 07 (Infrastructure IaC Terraform & Docker Setup)
- **Target Files**:
  - `tests/e2e/playwright.config.ts`
  - `tests/e2e/package.json`
  - `tests/e2e/src/mocks/browser.ts`
  - `tests/e2e/src/mocks/server.ts`
  - `tests/e2e/src/mocks/handlers/deliveries.ts`
  - `tests/e2e/src/mocks/handlers/market-prices.ts`
  - `tests/e2e/src/mocks/handlers/signalr.ts`
  - `tests/e2e/src/pages/BasePage.ts`
  - `tests/e2e/src/pages/DeliveriesPage.ts`
  - `tests/e2e/src/pages/DashboardPage.ts`
  - `tests/e2e/src/pages/CommandPalettePage.ts`
  - `tests/e2e/src/specs/tier1-feature-coverage.spec.ts`
  - `tests/e2e/src/specs/tier2-boundary-corner.spec.ts`
  - `tests/e2e/src/specs/tier3-cross-feature.spec.ts`
  - `tests/e2e/src/specs/tier4-real-world-workload.spec.ts`
  - `tests/performance/Tradebook.Benchmarks/Tradebook.Benchmarks.csproj`
  - `tests/performance/Tradebook.Benchmarks/Program.cs`
  - `tests/performance/Tradebook.Benchmarks/Scenarios/ApiDeliveryIngestionScenario.cs`
  - `tests/performance/Tradebook.Benchmarks/Scenarios/SignalRStreamLoadScenario.cs`
  - `tests/performance/Tradebook.Benchmarks/Scenarios/BiTemporalQueryScenario.cs`
  - `.github/workflows/ci-e2e-performance.yml`

---

## 1. Detailed Scope & Feature Coverage

### 1.1 Objectives
Task 09 establishes opaque-box, hermetic, end-to-end (E2E) testing harness and performance benchmarking suite for Tradebook. Testing architecture validates system behavior from outside without relying on internal implementation hooks, combining browser UI automation via **Playwright**, API network isolation via **MSW 2.0 (Mock Service Worker)**, ultra-high-throughput load/stress benchmarking using **NBomber 5.x** in C#.

Test harness guarantees Tradebook meets core operational SLAs:
1. **Snappy UI Mutations**: 0ms perceived latency via local optimistic TanStack Query / Dexie.js updates, rendering within <16.6ms (60 FPS).
2. **High-Throughput API Ingestion**: Sustained **>35,000 requests/second** REST ingestion throughput per node under <50ms p99 latency.
3. **Real-Time Binary Streaming**: SignalR Core binary MessagePack WebSocket broadcast handling up to **10,000 events/second** under client RxJS `bufferTime(50)` sliding window throttling.
4. **Resilient Local-First Synchronization**: offline mutation queueing via IndexedDB (Dexie.js), 3-way structural merge conflict resolution, deterministic outbox draining upon reconnect.
5. **Point-In-Time Bi-Temporal Audit Integrity**: PostgreSQL 17 `TSTZRANGE` audit log verification ensures zero time-range overlaps or lost historical state snapshots.

### 1.2 Scope & Functional Capabilities
- **Playwright Browser Automation**: multi-browser E2E suite across Chromium, Firefox, WebKit, testing virtualized grids (TanStack Table), command palette navigation (Kbar/cmdk), dynamic canvas charts (Apache ECharts, Tremor, Lightweight Charts), node-based workflows (React Flow + dnd-kit zoom translation).
- **MSW 2.0 Network Isolation Layer**: network interception layer running both in-browser (ServiceWorker) and in Node.js test scripts. Intercepts REST `/api/v1/*` endpoints and SignalR WebSocket channels, enables deterministic API fault injection (latency, 500 errors, partial network drops) without external server dependencies.
- **C# NBomber Performance Suite**: high-performance load testing suite in C# targeting Native AOT .NET 9 FastEndpoints and SignalR binary MessagePack hubs. Measures throughput (req/sec), p50/p95/p99 latency distribution, HTTP/WebSocket error rates, host RAM/CPU consumption curves under stress.
- **4-Tier Test Methodology Framework**:
  - **Tier 1 (Feature Coverage)**: Standard happy-path functional flows, optimistic UI updates, component rendering.
  - **Tier 2 (Boundary & Corner Cases)**: Network drop recovery, IndexedDB mutation queue replaying, 100,000-row virtualization scroll, 3-way merge conflict resolution, canvas WebGL context pool cap enforcement (max 8 contexts).
  - **Tier 3 (Cross-Feature Combinations)**: Multi-system workflow assertions (e.g. `Cmd+K` delivery creation -> FastEndpoints REST -> NATS Outbox -> SignalR WebSocket MessagePack broadcast -> ECharts WebGL update -> PostgreSQL `TSTZRANGE` audit verification).
  - **Tier 4 (Real-World Workload Testing)**: Peak market open simulation, 50 concurrent tabs, 35,000 req/sec API burst, 512MB client tab memory governor limit assertion over sustained 30-minute stress runs.

---

## 2. Key Deliverables & File Layout

```
c:\Users\LaxmananKrishnapilla\tradebook\
├── tests/
│   ├── e2e/
│   │   ├── package.json                          # Node dependencies (@playwright/test, msw, typescript)
│   │   ├── playwright.config.ts                  # Playwright multi-browser test runner configuration
│   │   └── src/
│   │       ├── mocks/
│   │       │   ├── browser.ts                    # MSW 2.0 ServiceWorker setup for browser runtime
│   │       │   ├── server.ts                     # MSW 2.0 Node.js server setup for headless specs
│   │       │   └── handlers/
│   │       │       ├── deliveries.ts           # MSW handlers for Physical Delivery REST & batch mutations
│   │       │       ├── market-prices.ts        # MSW handlers for daily market price index reads
│   │       │       └── signalr.ts                # MSW mock WebSocket handler for binary MessagePack
│   │       ├── pages/
│   │       │   ├── BasePage.ts                   # Abstract POM with common waits, locators & cmdk hooks
│   │       │   ├── DeliveriesPage.ts             # Page Object for Virtualized Grid & Optimistic Cells
│   │       │   ├── DashboardPage.ts              # Page Object for ECharts/Tremor Dynamic Visualizations
│   │       │   └── CommandPalettePage.ts         # Page Object for Kbar/cmdk Keyboard Navigation
│   │       └── specs/
│   │           ├── tier1-feature-coverage.spec.ts # Tier 1 Happy Path & Feature Specs
│   │           ├── tier2-boundary-corner.spec.ts  # Tier 2 Boundary, Offline Queue & Conflict Specs
│   │           ├── tier3-cross-feature.spec.ts    # Tier 3 End-to-End Cross-System Pipeline Specs
│   │           └── tier4-real-world-workload.spec.ts # Tier 4 Real-World Market Burst & Memory Specs
│   └── performance/
│       └── Tradebook.Benchmarks/
│           ├── Tradebook.Benchmarks.csproj       # C# .NET 9 performance project file (NBomber, SignalR)
│           ├── Program.cs                        # NBomber runner entry point & CLI scenario selector
│           └── Scenarios/
│               ├── ApiDeliveryIngestionScenario.cs # NBomber REST scenario (>35,000 req/sec benchmark)
│               ├── SignalRStreamLoadScenario.cs   # NBomber SignalR MessagePack binary push scenario
│               └── BiTemporalQueryScenario.cs    # NBomber Point-In-Time bi-temporal query scenario
└── .github/
    └── workflows/
        └── ci-e2e-performance.yml                # CI/CD workflow running Playwright & NBomber suites
```

---

## 3. Architecture & Code Contract Blueprints

### 3.1 Playwright E2E UI Testing Suite Setup & Page Object Models

#### Playwright Configuration (`tests/e2e/playwright.config.ts`)
```typescript
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './src/specs',
  timeout: 30 * 1000,
  expect: {
    timeout: 5000,
    toHaveScreenshot: { maxDiffPixelRatio: 0.02 },
  },
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 4 : undefined,
  reporter: [
    ['html', { outputFolder: '../../reports/playwright-report' }],
    ['junit', { outputFile: '../../reports/playwright-results.xml' }],
    ['list'],
  ],
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL || 'http://localhost:5173',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    viewport: { width: 1280, height: 720 },
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'firefox',
      use: { ...devices['Desktop Firefox'] },
    },
    {
      name: 'webkit',
      use: { ...devices['Desktop Safari'] },
    },
  ],
  webServer: process.env.CI ? undefined : {
    command: 'npm run dev --prefix ../../src/Frontend',
    url: 'http://localhost:5173',
    reuseExistingServer: true,
    timeout: 120 * 1000,
  },
});
```

#### Base Page Object Model (`tests/e2e/src/pages/BasePage.ts`)
```typescript
import { Page, Locator, expect } from '@playwright/test';

export abstract class BasePage {
  readonly page: Page;
  readonly commandPaletteTrigger: Locator;
  readonly globalToast: Locator;
  readonly offlineSyncBadge: Locator;

  constructor(page: Page) {
    this.page = page;
    this.commandPaletteTrigger = page.locator('[data-testid="cmdk-trigger"]');
    this.globalToast = page.locator('[data-testid="toast-container"]');
    this.offlineSyncBadge = page.locator('[data-testid="sync-status-badge"]');
  }

  async goto(path: string = '/') {
    await this.page.goto(path);
    await this.page.waitForLoadState('domcontentloaded');
  }

  async openCommandPalette() {
    await this.page.keyboard.press('ControlOrMeta+k');
    await expect(this.page.locator('[data-testid="cmdk-dialog"]')).toBeVisible();
  }

  async executeCommand(commandText: string) {
    await this.openCommandPalette();
    const input = this.page.locator('[data-testid="cmdk-input"]');
    await input.fill(commandText);
    const item = this.page.locator(`[data-testid="cmdk-item"]:has-text("${commandText}")`).first();
    await item.click();
  }

  async assertSyncStatus(expectedStatus: 'synced' | 'pending' | 'offline') {
    await expect(this.offlineSyncBadge).toHaveAttribute('data-status', expectedStatus);
  }

  async measureOptimisticMutationLatency(mutationTriggerFn: () => Promise<void>): Promise<number> {
    const start = performance.now();
    await mutationTriggerFn();
    // Assert immediate UI feedback rendered in DOM
    await expect(this.page.locator('[data-optimistic="true"]').first()).toBeVisible({ timeout: 50 });
    const duration = performance.now() - start;
    return duration;
  }
}
```

#### Deliveries Page Object Model (`tests/e2e/src/pages/DeliveriesPage.ts`)
```typescript
import { Page, Locator, expect } from '@playwright/test';
import { BasePage } from './BasePage';

export class DeliveriesPage extends BasePage {
  readonly deliveriesGrid: Locator;
  readonly createDeliveryBtn: Locator;
  readonly contractInstanceInput: Locator;
  readonly volumeNominatedInput: Locator;
  readonly priceEurMwhInput: Locator;
  readonly bookTypeSelect: Locator;
  readonly submitDeliveryBtn: Locator;

  constructor(page: Page) {
    super(page);
    this.deliveriesGrid = page.locator('[data-testid="virtual-deliveries-grid"]');
    this.createDeliveryBtn = page.locator('[data-testid="btn-create-delivery"]');
    this.contractInstanceInput = page.locator('input[name="contractInstanceId"]');
    this.volumeNominatedInput = page.locator('input[name="volumeNominatedMwh"]');
    this.priceEurMwhInput = page.locator('input[name="priceEurMwh"]');
    this.bookTypeSelect = page.locator('select[name="bookType"]');
    this.submitDeliveryBtn = page.locator('button[type="submit"]');
  }

  async navigateToDeliveries() {
    await this.goto('/deliveries');
    await expect(this.deliveriesGrid).toBeVisible();
  }

  async createDeliveryOptimistic(contractInstanceId: string, volumeMwh: number, priceEurMwh: number, bookType: 'Sourcing' | 'Sales'): Promise<number> {
    await this.createDeliveryBtn.click();
    await this.contractInstanceInput.fill(contractInstanceId);
    await this.volumeNominatedInput.fill(volumeMwh.toString());
    await this.priceEurMwhInput.fill(priceEurMwh.toString());
    await this.bookTypeSelect.selectOption(bookType);

    return await this.measureOptimisticMutationLatency(async () => {
      await this.submitDeliveryBtn.click();
    });
  }

  async getGridRowCount(): Promise<number> {
    const rows = this.deliveriesGrid.locator('[role="row"]');
    return await rows.count();
  }

  async editGridCellOptimistic(rowIndex: number, columnId: string, newValue: string) {
    const cell = this.deliveriesGrid.locator(`[data-row-index="${rowIndex}"][data-col-id="${columnId}"]`);
    await cell.dblclick();
    const cellInput = cell.locator('input');
    await cellInput.fill(newValue);
    await cellInput.press('Enter');
  }
}
```

---

### 3.2 MSW 2.0 API Network Isolation Layer

#### MSW Delivery Handlers (`tests/e2e/src/mocks/handlers/deliveries.ts`)
```typescript
import { http, HttpResponse, delay } from 'msw';

export interface DeliveryPayload {
  deliveryId?: string;
  contractId: string;
  contractInstanceId: string;
  bookType: 'Sourcing' | 'Sales' | 'Intercompany';
  supplyMonth: string;
  volumeNominatedMwh?: number;
  volumeRealisedMwh?: number;
  priceEurMwh?: number;
}

const mockDeliveriesDb = new Map<string, DeliveryPayload>();

export const deliveryHandlers = [
  // GET /api/v1/deliveries - Paginated Delivery Fetch
  http.get('/api/v1/deliveries', async ({ request }) => {
    const url = new URL(request.url);
    const limit = parseInt(url.searchParams.get('limit') || '50', 10);
    const deliveries = Array.from(mockDeliveriesDb.values()).slice(0, limit);

    return HttpResponse.json({
      items: deliveries,
      totalCount: mockDeliveriesDb.size,
      page: 1,
      pageSize: limit,
    }, { status: 200 });
  }),

  // POST /api/v1/deliveries - Create Delivery Mutation
  http.post('/api/v1/deliveries', async ({ request }) => {
    const body = (await request.json()) as DeliveryPayload;
    const deliveryId = body.deliveryId || crypto.randomUUID();
    const newDelivery = { ...body, deliveryId, status: 'Pending - No Invoice', createdAt: new Date().toISOString() };
    mockDeliveriesDb.set(deliveryId, newDelivery);

    // Support artificial network delay testing via header flag
    const simulatedDelay = request.headers.get('x-simulated-delay');
    if (simulatedDelay) {
      await delay(parseInt(simulatedDelay, 10));
    }

    return HttpResponse.json(newDelivery, { status: 201 });
  }),

  // POST /api/v1/mutations/batch - Batch Sync Offline Dexie Mutations
  http.post('/api/v1/mutations/batch', async ({ request }) => {
    const body = (await request.json()) as { mutations: Array<{ id: string; payload: DeliveryPayload }> };
    const processedIds: string[] = [];

    for (const item of body.mutations) {
      const deliveryId = item.payload.deliveryId || crypto.randomUUID();
      mockDeliveriesDb.set(deliveryId, { ...item.payload, deliveryId });
      processedIds.push(item.id);
    }

    return HttpResponse.json({
      status: 'SUCCESS',
      processedCount: processedIds.length,
      processedIds,
    }, { status: 200 });
  }),
];
```

#### MSW Browser Setup (`tests/e2e/src/mocks/browser.ts`)
```typescript
import { setupWorker } from 'msw/browser';
import { deliveryHandlers } from './handlers/deliveries';
import { marketPriceHandlers } from './handlers/market-prices';
import { signalrHandlers } from './handlers/signalr';

export const worker = setupWorker(
  ...deliveryHandlers,
  ...marketPriceHandlers,
  ...signalrHandlers
);
```

---

### 3.3 NBomber Load & Performance Testing Scenario Suite (>35,000 req/sec Target)

#### NBomber Benchmark Project (`tests/performance/Tradebook.Benchmarks/Tradebook.Benchmarks.csproj`)
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <ServerGarbageCollection>true</ServerGarbageCollection>
    <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NBomber" Version="5.5.0" />
    <PackageReference Include="NBomber.Http" Version="5.0.1" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="9.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Protocols.MessagePack" Version="9.0.0" />
  </ItemGroup>
</Project>
```

#### NBomber Ingestion Scenario (`tests/performance/Tradebook.Benchmarks/Scenarios/ApiDeliveryIngestionScenario.cs`)
```csharp
using System.Text;
using System.Text.Json;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace Tradebook.Benchmarks.Scenarios;

public static class ApiDeliveryIngestionScenario
{
    public static IScenario Build(string baseUrl)
    {
        var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        
        var step = Step.Create("post_delivery_ingestion", clientFactory: HttpClientFactory.Create(), execute: async context =>
        {
            var payload = new
            {
                contractId = Guid.NewGuid(),
                contractInstanceId = $"BFEX45.BT.2301.CO2E-{Random.Shared.Next(1, 12)}-2026",
                bookType = "Sourcing",
                supplyMonth = new DateTime(2026, Random.Shared.Next(1, 12), 1),
                volumeNominatedMwh = 12000.0m,
                volumeRealisedMwh = 11840.0m,
                priceEurMwh = 34.50m
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            var request = Http.CreateRequest("POST", "/api/v1/deliveries")
                .WithHeader("Content-Type", "application/json")
                .WithBody(jsonContent);

            var response = await Http.Send(httpClient, request);
            return response;
        });

        return ScenarioBuilder.CreateScenario("api_delivery_ingestion_throughput", step)
            .WithWarmUpDuration(TimeSpan.FromSeconds(5))
            .WithLoadSimulations(
                // Ramp up to high concurrency to achieve >35,000 req/sec benchmark target
                Simulation.RampConstant(copies: 200, during: TimeSpan.FromSeconds(10)),
                Simulation.KeepConstant(copies: 500, during: TimeSpan.FromSeconds(30))
            );
    }
}
```

#### NBomber Main Entry (`tests/performance/Tradebook.Benchmarks/Program.cs`)
```csharp
using NBomber.CSharp;
using Tradebook.Benchmarks.Scenarios;

namespace Tradebook.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        var baseUrl = args.Length > 0 ? args[0] : "http://localhost:5000";

        var ingestionScenario = ApiDeliveryIngestionScenario.Build(baseUrl);
        var signalRScenario = SignalRStreamLoadScenario.Build(baseUrl);

        NBomberRunner
            .RegisterScenarios(ingestionScenario, signalRScenario)
            .WithReportFileName("tradebook_performance_report")
            .WithReportFolder("../../../reports/nbomber-report")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Md, ReportFormat.Txt, ReportFormat.Csv)
            .Run();
    }
}
```

---

### 3.4 4-Tier Test Case Methodology

```
+---------------------------------------------------------------------------------------------------+
|                                   4-TIER TEST CASE METHODOLOGY                                    |
+---------------------------------------------------------------------------------------------------+
|                                                                                                   |
|  TIER 1: FEATURE COVERAGE (Happy Paths & Standard UX)                                            |
|  - Validates optimistic UI mutations (perceived 0ms latency, DOM render <16.6ms).                 |
|  - Tests CRUD operations on Physical Deliveries, Contracts, Capacity Bookings, and Custom Visualizations. |
|  - Verifies Kbar/cmdk command palette keyboard shortcuts (`Cmd+K`, `g p`, `c o`).                 |
|                                                                                                   |
|  TIER 2: BOUNDARY & CORNER CASES (Stress, Offline & State Conflicts)                             |
|  - Simulates network disconnects, storing mutations in Dexie.js IndexedDB offline queue.          |
|  - Asserts automatic background batch replay (`POST /api/v1/mutations/batch`) on reconnect.       |
|  - Verifies 3-way structural merge conflict resolution using stable ULIDs.                        |
|  - Validates virtualized TanStack Table smooth 60 FPS scroll under 100,000 records.               |
|  - Enforces WebGL GPU canvas context cap: max 8 active contexts, deferring 9th context allocation.|
|                                                                                                   |
|  TIER 3: CROSS-FEATURE COMBINATIONS (End-to-End System Pipelines)                                 |
|  - Full System Execution Flow:                                                                    |
|    1. User issues `Cmd+K` delivery creation via Command Palette.                                  |
|    2. Optimistic UI instantly renders row (<16.6ms).                                              |
|    3. FastEndpoints REST API receives payload -> NATS JetStream Outbox worker drains.              |
|    4. SignalR WebSocket broadcasts MessagePack payload to connected clients.                      |
|    5. ECharts WebGL updates candlestick chart off-main-thread via Web Worker.                     |
|    6. Bi-Temporal query function `get_entity_state_as_of` asserts PostgreSQL audit log.          |
|                                                                                                   |
|  TIER 4: REAL-WORLD WORKLOAD TESTING (Peak Load & Resource Limits)                                |
|  - Simulates Market Open Burst: 50 concurrent browser tabs receiving 5,000 ticks/sec.             |
|  - Verifies client RxJS `bufferTime(50)` window bounds React re-renders to max 20 FPS.             |
|  - Executes 35,000 req/sec NBomber REST load for 30 minutes; asserts p99 latency <50ms.            |
|  - Asserts `ClientMemoryGovernor` caps single-tab browser heap at <512MB RAM without crashes.     |
|                                                                                                   |
+---------------------------------------------------------------------------------------------------+
```

---

## 4. Subagent Implementation Step-by-Step Workflow

### Step 1: Initialize Playwright & MSW 2.0 Test Directory Structure
1. Create `tests/e2e/package.json` with required dependencies: `@playwright/test`, `msw`, `typescript`, `@types/node`.
2. Install npm packages: `cd tests/e2e && npm install`.
3. Generate Playwright config `tests/e2e/playwright.config.ts`.
4. Install browser binaries: `npx playwright install --with-deps`.

### Step 2: Implement MSW Network Interceptors & Page Object Models
1. Author MSW handlers for deliveries (`tests/e2e/src/mocks/handlers/deliveries.ts`), market prices, mock SignalR WebSocket server.
2. Construct `BasePage.ts` providing locator helpers, wait conditions, command palette triggers, optimistic latency timing tools.
3. Build specialized POMs: `DeliveriesPage.ts`, `DashboardPage.ts`, `CommandPalettePage.ts`.

### Step 3: Implement TypeScript Playwright Specs Across Tiers 1–4
1. **Tier 1 Spec (`tests/e2e/src/specs/tier1-feature-coverage.spec.ts`)**:
   - Test optimistic delivery creation: assert UI updates instantly (<16.6ms) before MSW network response completes.
   - Test Kbar command palette navigation and visual chart rendering.
2. **Tier 2 Spec (`tests/e2e/src/specs/tier2-boundary-corner.spec.ts`)**:
   - Simulate `context.setOffline(true)`: perform 5 delivery mutations; assert status transitions to `'pending'` in Dexie IndexedDB.
   - Re-enable network (`context.setOffline(false)`): assert batch sync triggers `/api/v1/mutations/batch` and status transitions to `'synced'`.
   - Scroll virtualized table with 100,000 mock rows; assert no DOM node accumulation or frame drops below 55 FPS.
   - Instantiation of 9 chart widgets: verify 9th widget defers canvas creation per `WebGLContextPoolManager` limit (max 8).
3. **Tier 3 Spec (`tests/e2e/src/specs/tier3-cross-feature.spec.ts`)**:
   - Execute end-to-end integration test spanning UI creation, MSW interception, SignalR binary push parsing, and database point-in-time state checks.
4. **Tier 4 Spec (`tests/e2e/src/specs/tier4-real-world-workload.spec.ts`)**:
   - Execute high-density tick stream push test (5,000 msgs/sec); measure tab RAM usage via `performance.memory.usedJSHeapSize`; verify RAM remains <512MB.

### Step 4: Setup C# NBomber Performance Test Suite
1. Create `tests/performance/Tradebook.Benchmarks/Tradebook.Benchmarks.csproj` targeting `.NET 9.0`.
2. Add NBomber 5.x NuGet packages (`NBomber`, `NBomber.Http`, `Microsoft.AspNetCore.SignalR.Client`).
3. Author `ApiDeliveryIngestionScenario.cs` configuring constant & ramp load simulations.
4. Author `SignalRStreamLoadScenario.cs` testing binary MessagePack hub connections under 10,000 msgs/sec stream push.
5. Compile and run: `dotnet run --project tests/performance/Tradebook.Benchmarks -c Release`.

### Step 5: Author GitHub Actions CI/CD Pipeline
Create `.github/workflows/ci-e2e-performance.yml` running unit tests, Playwright multi-browser specs, NBomber baseline load checks.

---

## 5. Independent Verification Steps & Acceptance Workflow

To verify Task 09 independently, run following automated terminal commands, validate against quantitative acceptance criteria.

### 5.1 Verification Commands

```bash
# 1. Install E2E Test Suite Dependencies
cd tests/e2e
npm install
npx playwright install --with-deps

# 2. Run Playwright E2E UI Test Suite Across Multi-Browser Projects
npx playwright test --config=playwright.config.ts

# 3. Build & Run NBomber Performance Load Benchmark Suite
cd ../performance/Tradebook.Benchmarks
dotnet build -c Release
dotnet run -c Release -- "http://localhost:5000"

# 4. Inspect Test Reports
# HTML Playwright Report: reports/playwright-report/index.html
# NBomber Benchmark Report: reports/nbomber-report/tradebook_performance_report.html
```

### 5.2 Quantitative Acceptance Criteria

| Metric / Dimension | Target Threshold / Pass Criteria | Verification Method | Status |
| :--- | :--- | :--- | :--- |
| **Playwright Spec Pass Rate** | 100% Green across Chromium, Firefox, WebKit | `npx playwright test` test run summary | Required |
| **Optimistic UI Perceived Latency** | **0ms perceived latency**, DOM render **<16.6ms** (60 FPS) | `measureOptimisticMutationLatency()` assertion | Required |
| **NBomber API Ingestion Throughput** | **>35,000 requests/second** sustained | NBomber `tradebook_performance_report.html` | Required |
| **API Latency Distribution** | p50 < 15ms, p95 < 30ms, **p99 < 50ms** | NBomber latency percentiles report | Required |
| **HTTP/WebSocket Error Rate** | **0.00%** failed requests under load | NBomber error count summary | Required |
| **SignalR Broadcast Load** | **10,000 msgs/sec** binary push handled without buffer overflow | SignalR NBomber scenario assertion | Required |
| **Virtualized Grid Scroll Performance** | **≥55 FPS** scrolling performance under 100,000 rows | Playwright Chrome Trace Performance audit | Required |
| **WebGL Context Pool Governance** | Max **8 active WebGL canvas contexts** per browser tab | Tier 2 Playwright canvas context cap spec | Required |
| **Client Tab Memory Cap** | Browser Heap Memory **<512MB RAM** under 30-min burst | `performance.memory.usedJSHeapSize` audit | Required |

---

## 6. Anti-Cheating & Mandatory Integrity Guardrails

MANDATORY INTEGRITY WARNING: DO NOT CHEAT. All test implementations, Page Object Models, MSW handlers, NBomber performance scenarios must be 100% genuine and fully functional.

1. **No Fake / Hardcoded Pass Returns**:
   - Playwright spec assertions MUST check genuine DOM elements, attributes (`data-status`, `data-optimistic`), real text mutations. Hardcoding `expect(true).toBe(true)` or empty test functions strictly prohibited.
2. **No Mock Latency Bypass in Benchmarks**:
   - NBomber scenarios MUST send real HTTP POST payloads over `HttpClient` to active backend endpoints or hermetic testcontainers. Hardcoding artificial `Response.Ok()` without network transmission is explicit integrity violation.
3. **Genuine Offline Replay Assertion**:
   - Offline mutation specs MUST use Playwright's `browserContext.setOffline(true)`, verify actual IndexedDB (Dexie.js) state storage and network batch replay upon reconnection.
4. **No Suppressed Performance Bottlenecks**:
   - NBomber benchmarks MUST NOT ignore HTTP status codes or swallow exceptions. Any request returning 4xx or 5xx MUST be flagged as `Response.Fail()`.

A specialized `teamwork_preview_auditor` subagent independently verifies all source code, test execution logs, benchmark reports. Integrity violations result in immediate rejection of task submission.

---
*End of Task 09 Specification.*
