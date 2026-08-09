import { execFileSync } from 'node:child_process';
import { test, expect } from '@playwright/test';
import { DeliveriesPage } from '../pages/DeliveriesPage.js';
import { contractId, login, loginToken } from '../support/environment.js';

test('real REST commit reaches browser through SignalR and has point-in-time audit state', async ({ page, request }, testInfo) => {
  await login(page); const deliveries = new DeliveriesPage(page); await deliveries.open(); const token = await loginToken(request); const instance = `E2E-PIPE-${Date.now()}`;
  const response = await request.post('/api/v1/deliveries', { headers: { Authorization: `Bearer ${token}` }, data: { contractId: contractId(), contractInstanceId: instance, bookType: 'Sourcing', supplyMonth: '2026-03-01', volumeRealisedMwh: 15 } });
  expect(response.status()).toBe(201); const created = await response.json() as { deliveryId: string };
  await expect(deliveries.row(instance)).toBeVisible({ timeout: 15_000 });
  const databaseUrl = process.env.DATABASE_URL; if (!databaseUrl) throw new Error('DATABASE_URL is required for the audit verification');
  const auditSql = "SELECT (get_entity_state_as_of('physical_deliveries', :'entity_id'::uuid, clock_timestamp()) IS NOT NULL)::text || '|' || (SELECT count(*) FROM audit_log newer JOIN audit_log older ON newer.entity_name = older.entity_name AND newer.entity_id = older.entity_id AND newer.id > older.id AND newer.system_time && older.system_time WHERE newer.entity_name = 'physical_deliveries')::text";
  const result = execFileSync('psql', [databaseUrl, '--set', `entity_id=${created.deliveryId}`, '--tuples-only', '--no-align', '--command', auditSql], { encoding: 'utf8' }).trim();
  expect(result).toBe('true|0'); testInfo.annotations.push({ type: 'signalr-aggregate', description: created.deliveryId });
});

test('disconnect mutation catch-up converges without a reload', async ({ page, request }) => {
  await login(page); const deliveries = new DeliveriesPage(page); await deliveries.open(); const token = await loginToken(request);
  await page.context().setOffline(true); const instance = `E2E-CATCHUP-${Date.now()}`;
  const response = await request.post('/api/v1/deliveries', { headers: { Authorization: `Bearer ${token}` }, data: { contractId: contractId(), contractInstanceId: instance, bookType: 'Sales', supplyMonth: '2026-03-01', volumeRealisedMwh: 7 } }); expect(response.status()).toBe(201);
  await page.context().setOffline(false); await expect(deliveries.row(instance)).toBeVisible({ timeout: 20_000 });
});
