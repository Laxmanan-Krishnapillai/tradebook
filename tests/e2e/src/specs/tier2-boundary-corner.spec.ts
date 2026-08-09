import { test, expect } from '@playwright/test';
import { DeliveriesPage } from '../pages/DeliveriesPage.js';
import { contractId, login, loginToken } from '../support/environment.js';

test('stale second writer receives 409, shows conflict, and cannot overwrite', async ({ browser, request }) => {
  const token = await loginToken(request); const instance = `E2E-OCC-${Date.now()}`;
  const createdResponse = await request.post('/api/v1/deliveries', { headers: { Authorization: `Bearer ${token}` }, data: { contractId: contractId(), contractInstanceId: instance, bookType: 'Sourcing', supplyMonth: '2026-03-01', volumeRealisedMwh: 10 } });
  expect(createdResponse.status()).toBe(201); const created = await createdResponse.json() as { deliveryId: string };
  const a = await browser.newContext(); const b = await browser.newContext();
  try {
    const pa = await a.newPage(); const pb = await b.newPage(); await login(pa); await login(pb);
    const da = new DeliveriesPage(pa); const db = new DeliveriesPage(pb); await da.open(); await db.open();
    await pa.getByTestId(`delivery-volume-${created.deliveryId}`).fill('21');
    await Promise.all([pa.waitForResponse(r => r.url().endsWith(`/deliveries/${created.deliveryId}`) && r.request().method() === 'PUT' && r.status() === 200), pa.getByTestId(`btn-save-${created.deliveryId}`).click()]);
    await pb.getByTestId(`delivery-volume-${created.deliveryId}`).fill('34');
    const conflict = pb.waitForResponse(r => r.url().endsWith(`/deliveries/${created.deliveryId}`) && r.request().method() === 'PUT');
    await pb.getByTestId(`btn-save-${created.deliveryId}`).click(); expect((await conflict).status()).toBe(409); await db.expectConflictPrompt();
    const current = await request.get(`/api/v1/deliveries/${created.deliveryId}`, { headers: { Authorization: `Bearer ${token}` } });
    expect((await current.json() as { volumeRealisedMwh: number }).volumeRealisedMwh).toBe(21);
  } finally { await a.close(); await b.close(); }
});

test('validator rejects a non-PascalCase enum and required fields stay enforced in UI', async ({ page, request }) => {
  await login(page); const token = await loginToken(request);
  const invalid = await request.post('/api/v1/deliveries', { headers: { Authorization: `Bearer ${token}` }, data: { contractId: contractId(), contractInstanceId: `E2E-BAD-${Date.now()}`, bookType: 'sourcing', supplyMonth: '2026-03-01' } });
  expect(invalid.status()).toBe(400);
  await page.goto('/deliveries'); await page.getByTestId('btn-create-delivery').click();
  const contract = page.getByRole('dialog').getByLabel('Contract ID'); await contract.fill('');
  await page.getByRole('dialog').getByRole('button', { name: 'Create', exact: true }).click();
  expect(await contract.evaluate((input: HTMLInputElement) => input.validationMessage.length > 0)).toBeTruthy();
});

test('100k seed remains virtualized with bounded DOM and frame timing data', async ({ page }, testInfo) => {
  await login(page); const historyResponse = page.waitForResponse(r => r.url().includes('/api/v1/deliveries?page=1&pageSize=100')); await page.goto('/deliveries'); const grid = page.getByTestId('virtual-deliveries-grid'); await expect(grid).toBeVisible();
  const requestUrl = (await historyResponse).url();
  expect(requestUrl).toContain('pageSize=100');
  const timings = await grid.evaluate(async element => { const values: number[] = []; let previous = performance.now(); for (let i = 0; i < 12; i++) { element.scrollTop += 300; await new Promise<void>(resolve => requestAnimationFrame(now => { values.push(now - previous); previous = now; resolve(); })); } return values; });
  testInfo.attachments.push({ name: 'virtual-scroll-frame-ms.json', contentType: 'application/json', body: Buffer.from(JSON.stringify(timings)) });
  expect(await grid.getByRole('row').count()).toBeLessThanOrEqual(25);
});
