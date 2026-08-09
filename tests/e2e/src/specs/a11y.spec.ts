import { AxeBuilder } from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

test('AGUI-08 login flow has no detectable accessibility violations', async ({ page }) => {
  await page.goto('/login');
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
  const results = await new AxeBuilder({ page }).analyze();
  expect(results.violations).toEqual([]);
});
