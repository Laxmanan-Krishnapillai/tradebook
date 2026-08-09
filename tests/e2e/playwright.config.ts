import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './src/specs', timeout: 45_000, expect: { timeout: 10_000 }, fullyParallel: false,
  forbidOnly: Boolean(process.env.CI), retries: process.env.CI ? 2 : 0,
  reporter: [...(process.env.ARGOS_TOKEN ? [['@argos-ci/playwright/reporter'] as const] : []), ['html', { outputFolder: '../../reports/playwright-report', open: 'never' }], ['junit', { outputFile: '../../reports/playwright-results.xml' }], ['list']],
  use: { baseURL: process.env.PLAYWRIGHT_BASE_URL ?? 'http://127.0.0.1:5173', trace: 'on-first-retry', screenshot: 'only-on-failure', video: 'retain-on-failure' },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'], launchOptions: { args: ['--disable-lcd-text', '--font-render-hinting=none'] } } },
    { name: 'firefox', use: { ...devices['Desktop Firefox'] } },
    { name: 'webkit', use: { ...devices['Desktop Safari'] } },
  ],
  webServer: process.env.CI ? undefined : { command: 'npm run dev --prefix ../../src/Frontend -- --host 127.0.0.1', url: 'http://127.0.0.1:5173', reuseExistingServer: true, timeout: 120_000 },
});
