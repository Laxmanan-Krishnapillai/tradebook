import { test, expect } from '@playwright/test';
import { DeliveriesPage } from '../pages/DeliveriesPage.js';
import { contractId, login } from '../support/environment.js';

test('interactive CRUD remains live while the external sustained profile runs @nightly', async ({ page }, testInfo) => {
  await login(page); const deliveries = new DeliveriesPage(page); await deliveries.open(); const instance = `E2E-LOAD-${Date.now()}`;
  const started = Date.now(); await deliveries.create(contractId(), instance); const elapsed = Date.now() - started;
  testInfo.annotations.push({ type: 'interactive-create-ms', description: String(elapsed) });
  await expect(deliveries.row(instance)).toBeVisible();
});
