import { expect, type Page } from '@playwright/test';
import { BasePage } from './BasePage.js';
export class CommandPalettePage extends BasePage {
  constructor(page: Page) { super(page); }
  async navigate(command: string, path: RegExp) { await this.openCommandPalette(); await this.page.getByRole('option', { name: command }).click(); await expect(this.page).toHaveURL(path); }
  async createDelivery() { await this.openCommandPalette(); await this.page.getByRole('option', { name: 'Create New Physical Delivery' }).click(); await expect(this.page.getByRole('dialog', { name: 'Create physical delivery' })).toBeVisible(); }
}
