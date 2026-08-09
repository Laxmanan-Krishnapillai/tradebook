import { expect, type Page } from '@playwright/test';
import { BasePage } from './BasePage.js';
export class DashboardPage extends BasePage { constructor(page: Page) { super(page); } async open() { await this.page.goto('/dashboard'); await expect(this.page.getByRole('heading', { name: /dashboard/i })).toBeVisible(); } async expectChart() { await expect(this.page.locator('canvas, [data-chart-type], table').first()).toBeVisible(); } }
