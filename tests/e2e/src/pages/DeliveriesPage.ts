import { expect, type Page } from '@playwright/test';
import { BasePage } from './BasePage.js';

export class DeliveriesPage extends BasePage {
  readonly grid = this.page.getByTestId('virtual-deliveries-grid');
  constructor(page: Page) { super(page); }
  async open() { await this.page.goto('/deliveries'); await expect(this.grid).toBeVisible(); }
  async create(contractId: string, instance: string) {
    await this.page.getByTestId('btn-create-delivery').click();
    const dialog = this.page.getByRole('dialog', { name: 'Create physical delivery' });
    await dialog.getByLabel('Contract ID').fill(contractId);
    await dialog.getByLabel('Contract instance (optional)').fill(instance);
    await dialog.getByLabel('Book type').selectOption('Sourcing');
    await dialog.getByLabel('Supply month').fill('2026-03-01');
    await dialog.getByLabel('Realised volume MWh').fill('11840');
    const latency = await this.measureOptimisticMutationLatency(async () => dialog.getByRole('button', { name: 'Create', exact: true }).click());
    await expect(this.grid.getByText(instance)).toBeVisible();
    await expect(this.page.locator('[data-optimistic="true"]')).toHaveCount(0);
    return latency;
  }
  row(instance: string) { return this.grid.getByRole('row').filter({ hasText: instance }); }
}
