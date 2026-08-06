# Task 09: Opaque-Box E2E Testing Harness, Playwright UI & k6 Performance Test Suite

> **DESCOPE NOTICE (2026-08-06 — applied to this spec)** — per [`architecture/decision-log.md`](../architecture/decision-log.md) **D10**: all absolute performance gates were removed (>35,000 req/sec, <50ms p99, <10ms query asserts, 5,000 msg/sec, 0.00%-failure tables, 60fps/16.6ms asserts, 512MB governor checks). NBomber was replaced by k6 (NBomber v5+ is commercial; k6 is OSS — this file keeps `nbomber` in its filename for link stability only). k6 records measured baselines (throughput, p99) on a documented reference machine into the committed `tests/performance/baseline.json`; later runs fail only on >20% regression vs that recorded baseline, and every load scenario treats any 4xx/5xx response as a failure. Offline-replay scenarios were removed (**D5** — the Dexie offline queue, `/api/v1/mutations/batch`, and `perform3WayMerge` no longer exist; the replacement concurrency test is the version-column OCC 409 flow). NATS integration checks were removed (**D2**) — realtime pipeline correctness is owned by Task 03's Testcontainers suite ([`task-03-signalr-realtime-and-nats.md`](task-03-signalr-realtime-and-nats.md), legacy filename kept); this suite exercises only the real SignalR hub. WebGL context-pool and client-memory-governor checks were removed (**D8** — those components were deleted).

- **Phase**: QA & Performance Engineering
- **Lead / Owner**: Quality Assurance & Performance Lead
- **Complexity**: High
- **Prerequisites**: Task 03 (in-process outbox dispatcher & SignalR real-time push — legacy filename, linked in the notice above), Task 05 (React 19 Keyboard-First Snappy CRUD UI), Task 07 (Infrastructure IaC Terraform & Docker Setup)
- **Target Files**:
  - `tests/e2e/playwright.config.ts`
  - `tests/e2e/package.json`
  - `tests/e2e/seed/seed-100k-deliveries.sql`
  - `tests/e2e/src/pages/BasePage.ts`
  - `tests/e2e/src/pages/DeliveriesPage.ts`
  - `tests/e2e/src/pages/DashboardPage.ts`
  - `tests/e2e/src/pages/CommandPalettePage.ts`
  - `tests/e2e/src/specs/tier1-feature-coverage.spec.ts`
  - `tests/e2e/src/specs/tier2-boundary-corner.spec.ts`
  - `tests/e2e/src/specs/tier3-cross-feature.spec.ts`
  - `tests/e2e/src/specs/tier4-real-world-workload.spec.ts`
  - `tests/performance/k6/api-delivery-ingestion.js`
  - `tests/performance/k6/deliveries-read.js`
  - `tests/performance/baseline.json`
  - `tests/performance/compare-baseline.mjs`
  - `.github/workflows/ci-e2e-performance.yml`

---

## 1. Detailed Scope & Feature Coverage

### 1.1 Objectives
Task 09 establishes an opaque-box, end-to-end (E2E) testing harness and performance benchmarking suite for Tradebook. The testing architecture validates system behavior from the outside without relying on internal implementation hooks, combining browser UI automation via **Playwright** and load benchmarking via **k6** (OSS).

The harness verifies these behaviors — functionally, with measured numbers recorded as data rather than asserted as absolute gates (D10):
1. **Snappy UI Mutations**: optimistic TanStack Query updates render immediately and reconcile with the server response; optimistic render latency is measured and attached to the test report as data.
2. **Optimistic Concurrency Integrity**: concurrent edits to the same entity resolve via the `version`-column OCC contract — the second writer receives HTTP 409 and the UI shows the conflict prompt; no silent overwrite (D5).
3. **Real-Time Push**: the browser receives `EntityChanged` pushes from the **real** SignalR hub at `/hubs/dashboard` (MessagePack protocol, JWT via `accessTokenFactory`). Dispatch-pipeline correctness (at-least-once, catch-up paging) is owned by Task 03's Testcontainers integration suite; Task 09 asserts the browser-visible outcome.
4. **Sustained Load Honesty**: k6 scenarios drive the real stack; any 4xx/5xx response is a failure, and valid-input scenarios must record zero failures. Throughput and p99 are compared against the committed baseline — a run regressing >20% fails.
5. **Point-In-Time Bi-Temporal Audit Integrity**: PostgreSQL 17 `TSTZRANGE` `audit_log` verification ensuring zero time-range overlaps or lost historical state snapshots.

### 1.2 Scope & Functional Capabilities
- **Playwright Browser Automation**: Multi-browser E2E suite executing across Chromium, Firefox, and WebKit, testing virtualized grids (TanStack Table), command palette navigation (Kbar/cmdk), and chart views rendered through Task 06's `ChartAdapter` engines (Apache ECharts, TradingView Lightweight Charts).
- **Real-Stack Execution**: the full-pipeline E2E tier runs against the real root `docker-compose.yml` stack (`postgres:17` + api only) with **no mocking**. MSW is permitted solely in component tests (§3.2).
- **k6 Performance Suite**: JavaScript load scenarios targeting the .NET 9 FastEndpoints REST API. Measures throughput (req/sec) and latency distribution (p95/p99); results feed the baseline-regression model (§3.3).
- **4-Tier Test Methodology Framework**:
  - **Tier 1 (Feature Coverage)**: Standard happy-path functional flows, optimistic UI updates, component rendering.
  - **Tier 2 (Boundary & Corner Cases)**: concurrent-edit 409 conflict flow, validation-rejection paths, 100,000-row virtualization scroll (frame timings recorded as data).
  - **Tier 3 (Cross-Feature Combinations)**: multi-system workflow assertions (e.g. `Cmd+K` delivery creation -> FastEndpoints REST -> outbox dispatcher -> SignalR MessagePack push -> grid/chart update -> PostgreSQL `TSTZRANGE` audit verification).
  - **Tier 4 (Real-World Workload Testing)**: nightly 10-minute sustained k6 run against the compose stack with a concurrent interactive Playwright session.

All request payloads in every tier and scenario MUST satisfy Task 02's FluentValidation rules: required fields present, enum values as verbatim PascalCase strings (Task 08 §4.7). No spec or scenario may reference `/api/v1/orders` or any batch-mutation endpoint — neither exists.

---

## 2. Key Deliverables & File Layout

```
c:\Users\LaxmananKrishnapilla\tradebook\
├── docker-compose.yml                            # Root stack: postgres:17 + api — the E2E system under test
├── tests/
│   ├── e2e/
│   │   ├── package.json                          # Node dependencies (@playwright/test, typescript)
│   │   ├── playwright.config.ts                  # Playwright multi-browser test runner configuration
│   │   ├── seed/
│   │   │   └── seed-100k-deliveries.sql          # Committed SQL seed: 100,000 delivery rows for virtualization specs
│   │   └── src/
│   │       ├── pages/
│   │       │   ├── BasePage.ts                   # Abstract POM with common waits, locators & cmdk hooks
│   │       │   ├── DeliveriesPage.ts             # Page Object for Virtualized Grid & Optimistic Cells
│   │       │   ├── DashboardPage.ts              # Page Object for ChartAdapter-driven visualizations
│   │       │   └── CommandPalettePage.ts         # Page Object for Kbar/cmdk Keyboard Navigation
│   │       └── specs/
│   │           ├── tier1-feature-coverage.spec.ts # Tier 1 Happy Path & Feature Specs
│   │           ├── tier2-boundary-corner.spec.ts  # Tier 2 Boundary, 409 Conflict & Virtualization Specs
│   │           ├── tier3-cross-feature.spec.ts    # Tier 3 End-to-End Cross-System Pipeline Specs
│   │           └── tier4-real-world-workload.spec.ts # Tier 4 Sustained-Load Interactive Specs
│   └── performance/
│       ├── baseline.json                         # Committed measured baseline (reference machine documented inside)
│       ├── compare-baseline.mjs                  # Fails the run on >20% regression vs baseline
│       └── k6/
│           ├── api-delivery-ingestion.js         # k6 REST write scenario (POST /api/v1/deliveries)
│           └── deliveries-read.js                # k6 REST read scenario (paginated GET /api/v1/deliveries)
└── .github/
    └── workflows/
        └── ci-e2e-performance.yml                # CI/CD workflow running Playwright & k6 suites
```

---

## 3. Architecture & Code Contract Blueprints

### 3.1 Playwright E2E UI Testing Suite Setup & Page Object Models

#### Playwright Configuration (`tests/e2e/playwright.config.ts`)

Precondition for every E2E run: the real backend stack is up via the root compose file (`docker compose up -d --wait` boots `postgres:17` + api). Playwright's `webServer` starts only the frontend dev server; the API and database are never mocked or substituted.

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
  // Frontend dev server only — API + DB come from the root docker compose stack.
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
  readonly conflictPrompt: Locator;

  constructor(page: Page) {
    this.page = page;
    this.commandPaletteTrigger = page.locator('[data-testid="cmdk-trigger"]');
    this.globalToast = page.locator('[data-testid="toast-container"]');
    this.conflictPrompt = page.locator('[data-testid="conflict-prompt"]');
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

  // Measures optimistic render latency and returns it as DATA for the test report
  // (attach via test.info().annotations). No absolute latency assertion — D10.
  // The only hard assertion is that an optimistic row appears before server reconciliation.
  async measureOptimisticMutationLatency(mutationTriggerFn: () => Promise<void>): Promise<number> {
    const start = performance.now();
    await mutationTriggerFn();
    await expect(this.page.locator('[data-optimistic="true"]').first()).toBeVisible();
    return performance.now() - start;
  }

  async expectConflictPrompt() {
    await expect(this.conflictPrompt).toBeVisible();
  }
}
```

#### Deliveries Page Object Model (`tests/e2e/src/pages/DeliveriesPage.ts`)

All form values submitted through this POM satisfy Task 02's validators: required fields filled, `bookType` as a verbatim PascalCase enum string.

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

  async createDeliveryOptimistic(contractInstanceId: string, volumeMwh: number, priceEurMwh: number, bookType: 'Sourcing' | 'Sales' | 'Intercompany'): Promise<number> {
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

### 3.2 Mocking Policy: MSW for Component Tests Only — E2E Runs Unmocked

- **MSW is allowed only in component tests** (Vitest, `src/Frontend/src/mocks` — owned by Task 08). It never appears in `tests/e2e`.
- If component tests intercept WebSocket traffic, **pin `msw >= 2.6`** (the first release with WebSocket interception support).
- The full-pipeline E2E tier runs against the real root `docker-compose.yml` stack (`postgres:17` + api) with **no mocking of any kind**: no request interception, no mock WebSocket server, no stubbed SignalR.
- Real-time in E2E means the real hub — `/hubs/dashboard`, MessagePack protocol, JWT delivered via `accessTokenFactory` (browsers cannot set Authorization headers on WebSocket upgrade):

```typescript
import { HubConnectionBuilder } from '@microsoft/signalr';
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';

const connection = new HubConnectionBuilder()
  .withUrl('/hubs/dashboard', { accessTokenFactory: () => getAccessToken() })
  .withHubProtocol(new MessagePackHubProtocol())
  .withAutomaticReconnect()
  .build();
```

- Dispatch-pipeline correctness (outbox claim semantics, at-least-once delivery, catch-up endpoint paging) is owned by **Task 03's Testcontainers integration suite** and is not re-tested here. Task 09 asserts only the browser-visible outcome: a committed mutation produces an `EntityChanged` push that updates the UI.

---

### 3.3 k6 Load & Performance Testing Suite (Baseline-Regression Model)

The previous harness's absolute throughput/latency gates are gone (D10). The model is:

1. **Record**: k6 runs on a **documented reference machine** record measured throughput (req/sec) and p99 latency per scenario into `tests/performance/baseline.json`, which is **committed** along with the machine description (CPU, RAM, OS, Docker version, commit SHA, date).
2. **Regress**: every subsequent run compares against the committed baseline. A run fails only when it regresses **>20%** — throughput below 80% of baseline, or p99 above 120% of baseline.
3. **Fail honestly**: any 4xx/5xx response is a failure. Valid-input scenarios assert **zero failures** (`http_req_failed: rate==0`).
4. **Durations**: CI smoke run = **60 seconds**; nightly sustained run = **10 minutes**.

#### k6 Ingestion Scenario (`tests/performance/k6/api-delivery-ingestion.js`)
```javascript
import http from 'k6/http';
import { check } from 'k6';
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const PROFILE = __ENV.PROFILE || 'smoke'; // smoke = 60s (CI) | sustained = 10m (nightly)

export const options = {
  scenarios: {
    load: {
      executor: 'constant-vus',
      vus: Number(__ENV.VUS || 50),
      duration: PROFILE === 'sustained' ? '10m' : '60s',
    },
  },
  summaryTrendStats: ['avg', 'p(95)', 'p(99)'],
  thresholds: {
    // Any 4xx/5xx is a failure; valid-input scenarios must record zero failures.
    http_req_failed: ['rate==0'],
    checks: ['rate==1.0'],
  },
};

export default function () {
  // Payload satisfies Task 02's FluentValidation rules: required fields present,
  // enum strings verbatim PascalCase (Task 08 §4.7).
  const payload = JSON.stringify({
    contractId: uuidv4(),
    contractInstanceId: `BFEX45.BT.2301.CO2E-${(__ITER % 12) + 1}-2026`,
    bookType: 'Sourcing',
    supplyMonth: '2026-03-01T00:00:00Z',
    volumeNominatedMwh: 12000.0,
    volumeRealisedMwh: 11840.0,
    priceEurMwh: 34.5,
  });

  const res = http.post(`${BASE_URL}/api/v1/deliveries`, payload, {
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${__ENV.API_JWT}`, // JWT required on every endpoint (D11)
    },
  });

  check(res, { 'created (201)': (r) => r.status === 201 });
}

export function handleSummary(data) {
  return {
    '../last-run.api-delivery-ingestion.json': JSON.stringify({
      scenario: 'api-delivery-ingestion',
      throughputReqPerSec: data.metrics.http_reqs.values.rate,
      p99Ms: data.metrics.http_req_duration.values['p(99)'],
    }, null, 2),
  };
}
```

`tests/performance/k6/deliveries-read.js` follows the same pattern for the read path: paginated `GET /api/v1/deliveries` with an explicit page size, same thresholds, same summary output (`last-run.deliveries-read.json`).

#### Committed Baseline (`tests/performance/baseline.json`)
```json
{
  "referenceMachine": "documented on first recording: CPU, RAM, OS, Docker version, commit SHA, date",
  "scenarios": {
    "api-delivery-ingestion": { "throughputReqPerSec": null, "p99Ms": null },
    "deliveries-read": { "throughputReqPerSec": null, "p99Ms": null }
  }
}
```

Values start `null`; the first recorded run on the reference machine fills them and is committed as part of this task. The comparison script refuses to pass while the baseline is unrecorded.

#### Regression Gate (`tests/performance/compare-baseline.mjs`)
```javascript
// Usage: node compare-baseline.mjs <scenario>
// Fails (exit 1) on >20% regression vs the committed baseline (D10):
// throughput below 80% of baseline, or p99 above 120% of baseline.
import { readFileSync } from 'node:fs';

const scenario = process.argv[2];
const baseline = JSON.parse(readFileSync(new URL('./baseline.json', import.meta.url), 'utf8')).scenarios[scenario];
const run = JSON.parse(readFileSync(new URL(`./last-run.${scenario}.json`, import.meta.url), 'utf8'));

if (!baseline || baseline.throughputReqPerSec == null || baseline.p99Ms == null) {
  console.error(`No recorded baseline for '${scenario}'. Record one on the documented reference machine and commit baseline.json.`);
  process.exit(1);
}

const failures = [];
if (run.throughputReqPerSec < 0.8 * baseline.throughputReqPerSec) {
  failures.push(`throughput ${run.throughputReqPerSec.toFixed(1)}/s < 80% of baseline ${baseline.throughputReqPerSec}/s`);
}
if (run.p99Ms > 1.2 * baseline.p99Ms) {
  failures.push(`p99 ${run.p99Ms.toFixed(1)}ms > 120% of baseline ${baseline.p99Ms}ms`);
}

if (failures.length > 0) {
  console.error(`REGRESSION vs baseline: ${failures.join('; ')}`);
  process.exit(1);
}
console.log(`'${scenario}' within baseline band (throughput ${run.throughputReqPerSec.toFixed(1)}/s, p99 ${run.p99Ms.toFixed(1)}ms).`);
```

---

### 3.4 4-Tier Test Case Methodology

```
+---------------------------------------------------------------------------------------------------+
|                                   4-TIER TEST CASE METHODOLOGY                                    |
+---------------------------------------------------------------------------------------------------+
|                                                                                                   |
|  TIER 1: FEATURE COVERAGE (Happy Paths & Standard UX)                                             |
|  - Validates optimistic UI mutations: optimistic row appears, then reconciles with the server     |
|    response; render latency is recorded as data (no absolute latency assertion — D10).            |
|  - Tests CRUD operations on Physical Deliveries, Contracts, Capacity Bookings.                    |
|  - Verifies Kbar/cmdk command palette keyboard shortcuts (`Cmd+K`, `g p`, `c o`).                 |
|                                                                                                   |
|  TIER 2: BOUNDARY & CORNER CASES (Conflicts, Validation & Scale)                                  |
|  - Concurrent-edit OCC flow (D5): two browser contexts edit the same delivery; the second         |
|    writer's PATCH returns HTTP 409 (stale `version`), the UI shows the conflict prompt, and       |
|    no data is silently overwritten.                                                               |
|  - Validation rejection: a payload with a non-PascalCase enum string or missing required field    |
|    receives HTTP 400 and the form shows the validation error.                                     |
|  - Virtualization at scale: seed 100,000 rows via the committed SQL script in tests/e2e/seed/,    |
|    request an explicit page size, scroll the virtualized TanStack Table, and record scroll        |
|    frame timings as data attached to the report (no hard frame-rate gate). Assert no DOM node     |
|    accumulation (virtualized row count stays bounded).                                            |
|                                                                                                   |
|  TIER 3: CROSS-FEATURE COMBINATIONS (End-to-End System Pipelines)                                 |
|  - Full System Execution Flow (real stack, zero mocks):                                           |
|    1. User issues `Cmd+K` delivery creation via Command Palette.                                  |
|    2. Optimistic UI renders the row immediately (latency recorded as data).                       |
|    3. FastEndpoints REST API commits entity + audit_log + outbox row in one transaction.          |
|    4. The in-process outbox dispatcher (Task 03) fans out to the SignalR hub.                     |
|    5. The browser receives EntityChanged on /hubs/dashboard (MessagePack) and the grid/chart      |
|       updates.                                                                                    |
|    6. Bi-temporal query (`get_entity_state_as_of`) asserts the PostgreSQL audit_log record.       |
|  - Catch-up: force a WebSocket disconnect, commit a mutation from a second context, reconnect,    |
|    and assert the UI converges (client catch-up via GET /api/v1/events?afterSequence=N).          |
|                                                                                                   |
|  TIER 4: REAL-WORLD WORKLOAD TESTING (Sustained Load)                                             |
|  - Nightly: 10-minute sustained k6 run against the compose stack while one Playwright session     |
|    performs interactive CRUD in parallel.                                                         |
|  - Asserts: zero failed valid-input requests; SignalR pushes keep arriving during load; UI        |
|    timing metrics recorded as data.                                                               |
|  - k6 results compared against the committed baseline (>20% regression fails).                    |
|                                                                                                   |
+---------------------------------------------------------------------------------------------------+
```

---

## 4. Subagent Implementation Step-by-Step Workflow

### Step 1: Initialize Playwright Test Directory Structure
1. Create `tests/e2e/package.json` with required dependencies: `@playwright/test`, `typescript`, `@types/node`. (No MSW here — E2E is unmocked; §3.2.)
2. Install npm packages: `cd tests/e2e && npm install`.
3. Generate Playwright config `tests/e2e/playwright.config.ts`.
4. Install browser binaries: `npx playwright install --with-deps`.
5. Author the committed seed script `tests/e2e/seed/seed-100k-deliveries.sql` (100,000 validator-compliant delivery rows via `generate_series`).

### Step 2: Implement Page Object Models
1. Construct `BasePage.ts` providing locator helpers, wait conditions, command palette triggers, conflict-prompt assertions, and optimistic latency measurement (data-only).
2. Build specialized POMs: `DeliveriesPage.ts`, `DashboardPage.ts`, `CommandPalettePage.ts`.

### Step 3: Implement TypeScript Playwright Specs Across Tiers 1–4
1. **Tier 1 Spec (`tests/e2e/src/specs/tier1-feature-coverage.spec.ts`)**:
   - Test optimistic delivery creation: assert the optimistic row appears and reconciles with the committed server row; record the measured latency as an annotation.
   - Test Kbar command palette navigation and chart rendering via the `ChartAdapter` views.
2. **Tier 2 Spec (`tests/e2e/src/specs/tier2-boundary-corner.spec.ts`)**:
   - Concurrent-edit 409: open two browser contexts on the same delivery; save in context A, then save a conflicting edit in context B; assert B receives HTTP 409, `expectConflictPrompt()` passes, and A's committed value survives.
   - Validation: submit an invalid enum string; assert HTTP 400 and a visible form error.
   - Virtualization: apply `tests/e2e/seed/seed-100k-deliveries.sql` (`psql "$DATABASE_URL" -f tests/e2e/seed/seed-100k-deliveries.sql`), load the grid with an explicit page size, scroll, record frame timings as data, and assert bounded DOM node count.
3. **Tier 3 Spec (`tests/e2e/src/specs/tier3-cross-feature.spec.ts`)**:
   - Execute the end-to-end pipeline spec spanning UI creation, real REST commit, real SignalR push on `/hubs/dashboard`, and database point-in-time state checks; plus the disconnect/catch-up convergence spec.
4. **Tier 4 Spec (`tests/e2e/src/specs/tier4-real-world-workload.spec.ts`)**:
   - Interactive session that runs during the nightly sustained k6 run; asserts UI responsiveness signals are recorded and pushes continue to arrive.

### Step 4: Setup k6 Performance Test Suite
1. Install the k6 OSS binary (locally and via `grafana/setup-k6-action` in CI).
2. Author `tests/performance/k6/api-delivery-ingestion.js` and `tests/performance/k6/deliveries-read.js` with the §3.3 thresholds.
3. Author `tests/performance/compare-baseline.mjs` and the initial `tests/performance/baseline.json`.
4. Record the baseline on the documented reference machine and commit `baseline.json` with the machine description filled in.
5. Verify locally: `k6 run --env PROFILE=smoke k6/api-delivery-ingestion.js && node compare-baseline.mjs api-delivery-ingestion`.

### Step 5: Author GitHub Actions CI/CD Pipeline
Create `.github/workflows/ci-e2e-performance.yml`:

```yaml
name: CI E2E & Performance

on:
  pull_request:
  schedule:
    - cron: '0 2 * * *'   # nightly sustained run

jobs:
  e2e:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: docker compose up -d --wait          # root compose: postgres:17 + api
      - run: npm ci && npx playwright install --with-deps
        working-directory: tests/e2e
      - run: npx playwright test
        working-directory: tests/e2e

  performance:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: docker compose up -d --wait
      - uses: grafana/setup-k6-action@v1
      - name: k6 (smoke 60s on PR, sustained 10m nightly) + baseline regression gate
        working-directory: tests/performance
        run: |
          PROFILE=smoke
          if [ "${{ github.event_name }}" = "schedule" ]; then PROFILE=sustained; fi
          k6 run --env PROFILE=$PROFILE k6/api-delivery-ingestion.js
          node compare-baseline.mjs api-delivery-ingestion
          k6 run --env PROFILE=$PROFILE k6/deliveries-read.js
          node compare-baseline.mjs deliveries-read
```

---

## 5. Independent Verification Steps & Acceptance Workflow

To verify Task 09 independently, run the following automated terminal commands and validate against the acceptance criteria.

### 5.1 Verification Commands

```bash
# 1. Boot the real system under test (root compose: postgres:17 + api)
docker compose up -d --wait

# 2. Install E2E Test Suite Dependencies
cd tests/e2e
npm install
npx playwright install --with-deps

# 3. Run Playwright E2E UI Test Suite Across Multi-Browser Projects
npx playwright test --config=playwright.config.ts

# 4. Run k6 smoke suite and the baseline regression gate
cd ../performance
k6 run --env PROFILE=smoke k6/api-delivery-ingestion.js
node compare-baseline.mjs api-delivery-ingestion
k6 run --env PROFILE=smoke k6/deliveries-read.js
node compare-baseline.mjs deliveries-read

# 5. Inspect Test Reports
# HTML Playwright Report: reports/playwright-report/index.html
# k6 summaries: tests/performance/last-run.<scenario>.json
```

### 5.2 Acceptance Criteria

| Metric / Dimension | Pass Criteria | Verification Method | Status |
| :--- | :--- | :--- | :--- |
| **Playwright Spec Pass Rate** | 100% green across Chromium, Firefox, WebKit | `npx playwright test` run summary | Required |
| **Optimistic UI Behavior** | Optimistic row renders before server reconciliation; measured latency attached as report data (no absolute gate) | Tier 1 spec annotations | Required |
| **OCC Conflict Flow** | Second writer receives HTTP 409; conflict prompt visible; no silent overwrite | Tier 2 concurrent-edit spec | Required |
| **Validation Contract** | All scenario payloads pass Task 02 validators; invalid enum/missing field rejected with HTTP 400 | Tier 2 validation spec | Required |
| **Real SignalR Delivery** | `EntityChanged` received from `/hubs/dashboard` (MessagePack, JWT) and UI updates; catch-up converges after disconnect | Tier 3 specs | Required |
| **k6 Failure Honesty** | Zero 4xx/5xx across valid-input scenarios (`http_req_failed: rate==0`) | k6 threshold summary | Required |
| **k6 Baseline Regression** | Throughput ≥ 80% of committed baseline AND p99 ≤ 120% of committed baseline | `compare-baseline.mjs` exit code | Required |
| **Virtualized Grid at Scale** | 100,000 rows seeded from `tests/e2e/seed/`; explicit page size requested; scroll frame timings recorded as artifact; DOM node count bounded | Tier 2 virtualization spec | Required |
| **Baseline Provenance** | `baseline.json` committed with reference machine documented (CPU, RAM, OS, Docker version, commit SHA, date) | Repo inspection | Required |

---

## 6. Anti-Cheating & Mandatory Integrity Guardrails

MANDATORY INTEGRITY WARNING: DO NOT CHEAT. All test implementations, Page Object Models, and k6 performance scenarios must be 100% genuine and fully functional.

1. **No Fake / Hardcoded Pass Returns**:
   - Playwright spec assertions MUST check genuine DOM elements, attributes (`data-optimistic`, conflict prompt), and real text mutations. Hardcoding `expect(true).toBe(true)` or empty test functions is strictly prohibited.
2. **No Mocked Load Targets**:
   - k6 scenarios MUST send real HTTP requests to the running compose stack. Pointing scenarios at stub servers or loopback mocks is an explicit integrity violation.
3. **Genuine Concurrency Assertion**:
   - The 409 conflict spec MUST use two real browser contexts and land both writes against the real API; simulating the 409 client-side is forbidden.
4. **No Suppressed Failures**:
   - k6 scenarios MUST NOT relax `http_req_failed`/`checks` thresholds or allowlist unexpected status codes. Any 4xx/5xx in a valid-input scenario is a failure.
5. **Baseline Integrity**:
   - `baseline.json` may only be re-recorded on the documented reference machine, committed together with the run summary. Editing baseline numbers to make a failing run pass is an explicit integrity violation.

A specialized `teamwork_preview_auditor` subagent will independently verify all source code, test execution logs, and benchmark reports. Any integrity violations will result in immediate rejection of the task submission.

---
*End of Task 09 Specification.*
