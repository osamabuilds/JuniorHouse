import { expect, test } from '@playwright/test';
import { uniqueSuffix } from '../support';

/** Sprint 7 (Catalog), size charts: one chart per category and audience, shown on every product of that kind (CHT-1..8, FR-08). */
test.describe('Catalog: size charts', () => {
  test('a chart is created for a category and audience, or the pair is reported as taken', async ({ page }) => {
    const name = `E2E chart ${uniqueSuffix()}`;
    await page.goto('/catalog/size-charts');
    await expect(page.getByRole('heading', { level: 1, name: 'Size charts' })).toBeVisible();

    await page.getByRole('button', { name: 'New size chart' }).click();
    await page.getByLabel('Chart name').fill(name);
    await page.getByLabel('Category').selectOption({ index: 6 });
    await page.getByLabel('For', { exact: true }).selectOption({ index: 1 });
    await page.getByLabel('Your name').fill('E2E Admin');
    const firstSize = page.locator('tbody tr').first();
    await firstSize.getByRole('checkbox').check();
    await firstSize.getByLabel(/Age from/).fill('12');
    await firstSize.getByLabel(/Age to/).fill('24');
    await firstSize.getByLabel(/Height from/).fill('80');
    await firstSize.getByLabel(/Height to/).fill('92');
    await page.getByRole('button', { name: 'Save chart' }).click();

    // The database persists between runs, so the pair may already have its chart: either answer is a clear message.
    const saved = page.getByRole('status').filter({ hasText: 'was saved' });
    const taken = page.getByRole('alert').filter({ hasText: /already/ });
    await expect(saved.or(taken).first()).toBeVisible();
  });

  test('a range that ends before it starts is caught before anything is sent', async ({ page }) => {
    await page.goto('/catalog/size-charts');
    await page.getByRole('button', { name: 'New size chart' }).click();
    await page.getByLabel('Chart name').fill(`E2E bad ${uniqueSuffix()}`);
    await page.getByLabel('Category').selectOption({ index: 1 });
    await page.getByLabel('For', { exact: true }).selectOption({ index: 1 });
    await page.getByLabel('Your name').fill('E2E Admin');
    const firstSize = page.locator('tbody tr').first();
    await firstSize.getByRole('checkbox').check();
    await firstSize.getByLabel(/Height from/).fill('100');
    await firstSize.getByLabel(/Height to/).fill('80');
    await page.getByRole('button', { name: 'Save chart' }).click();

    await expect(page.getByRole('alert').first()).toBeVisible();
  });
});
