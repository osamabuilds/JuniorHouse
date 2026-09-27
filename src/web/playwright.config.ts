import { defineConfig, devices } from '@playwright/test';

/**
 * SCRUM-175: end-to-end coverage for the Sprint 1 procurement flow. Runs against the admin app +
 * API started by `docker compose up` (README "Try Sprint 1") - this config doesn't start either
 * itself, since the API needs a real Postgres (Testcontainers/CLAUDE.md's own rule for anything
 * touching the real database applies here too).
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  retries: 0,
  reporter: 'list',
  use: {
    baseURL: process.env['ADMIN_BASE_URL'] ?? 'http://localhost:4201',
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
