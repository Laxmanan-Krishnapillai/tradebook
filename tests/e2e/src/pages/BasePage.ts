import { expect, type Page } from '@playwright/test';

export abstract class BasePage {
  constructor(readonly page: Page) {}
  async login() {
    await this.page.goto('/login');
    await this.page.getByLabel('Username').fill(process.env.E2E_USERNAME ?? 'e2e-trader');
    await this.page.getByLabel('Password').fill(process.env.E2E_PASSWORD ?? 'Tradebook-E2E-Only-2026!');
    await Promise.all([this.page.waitForURL(/\/deliveries$/), this.page.getByRole('button', { name: 'Sign in' }).click()]);
  }
  async openCommandPalette() {
    await this.page.keyboard.press('ControlOrMeta+k');
    await expect(this.page.getByRole('dialog', { name: 'Command palette' })).toBeVisible();
  }
  async expectConflictPrompt() { await expect(this.page.getByTestId('conflict-prompt')).toBeVisible(); }
  async measureOptimisticMutationLatency(trigger: () => Promise<void>) {
    const started = Date.now();
    await trigger();
    await expect(this.page.locator('[data-optimistic="true"]')).toBeVisible();
    return Date.now() - started;
  }
}
