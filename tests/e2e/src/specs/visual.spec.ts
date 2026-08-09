import { argosScreenshot } from '@argos-ci/playwright';
import { expect, test } from '@playwright/test';

test('AGUI-09 login visual is deterministic', async ({ page }) => {
  await page.goto('/login');
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await expect(page.getByRole('heading', { name: 'Sign in' })).toBeVisible();
  await page.evaluate(() => document.fonts.ready);
  await argosScreenshot(page, 'login', {
    viewports: [{ width: 390, height: 844 }, { width: 768, height: 1024 }, { width: 1440, height: 900 }],
    mask: [page.locator('[data-live-price]'), page.locator('time[data-live-timestamp]')],
  });
});
