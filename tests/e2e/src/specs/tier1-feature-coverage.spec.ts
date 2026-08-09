import { test, expect } from '@playwright/test';
import { CommandPalettePage } from '../pages/CommandPalettePage.js';
import { DashboardPage } from '../pages/DashboardPage.js';
import { DeliveriesPage } from '../pages/DeliveriesPage.js';
import { contractId, login } from '../support/environment.js';

test.beforeEach(async ({ page }) => login(page));
test('optimistic delivery renders before server reconciliation and records latency', async ({ page }, testInfo) => {
  const deliveries = new DeliveriesPage(page); await deliveries.open();
  const instance = `E2E-OPT-${Date.now()}`; const latency = await deliveries.create(contractId(), instance);
  testInfo.annotations.push({ type: 'optimistic-render-ms', description: String(latency) });
  await expect(deliveries.row(instance)).toBeVisible();
});
test('command palette navigates and opens ChartAdapter dashboard', async ({ page }) => {
  const palette = new CommandPalettePage(page); await palette.navigate('Go to Dashboard', /\/dashboard$/);
  const dashboard = new DashboardPage(page); await dashboard.expectChart();
});
