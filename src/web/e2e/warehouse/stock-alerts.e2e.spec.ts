import { expect, test } from '@playwright/test';
import { API, apiAdjust, apiCreateBin, createVariant, poll } from '../support';

/**
 * Sprint 6 (Warehouse), stock alerts: a variant that falls to its threshold raises one low-stock alert (stock-out at zero), the menu
 * badge counts the open ones, an alert can be acknowledged, and it closes by itself when stock recovers or the threshold is lowered.
 */
test.describe.configure({ timeout: 180_000 });

interface AlertRow {
  id: number;
  skuCode: string;
  levelCode: string;
}

async function waitForAlert(request: import('@playwright/test').APIRequestContext, sku: string, present = true): Promise<AlertRow | true> {
  return poll(async () => {
    const page = (await (await request.get(`${API}/api/warehouse/alerts?pageSize=100`)).json()) as { items: AlertRow[] };
    const found = page.items.find((a) => a.skuCode === sku);
    return present ? (found ?? null) : found ? null : true;
  }, `the alert for ${sku} to be ${present ? 'raised' : 'gone'}`, 30_000);
}

test.describe('Warehouse: stock alerts', () => {
  test('falling to the threshold raises a low-stock alert; it is shown, counted in the menu, and acknowledged by name', async ({ page, request }) => {
    const variant = await createVariant(request);
    const bin = await apiCreateBin(request);
    await apiAdjust(request, { gtin: variant.gtin, binCode: bin.binCode, change: 4 }); // the default threshold is 5

    await waitForAlert(request, variant.skuCode);
    await page.goto('/warehouse/alerts');
    await expect(page.getByRole('heading', { level: 1, name: 'Stock alerts' })).toBeVisible();
    const row = page.getByRole('row').filter({ hasText: variant.skuCode }).first();
    await expect(row).toContainText('Low stock');
    await expect(row).toContainText('Not yet');

    await expect(page.getByRole('navigation', { name: 'Primary' }).getByRole('status')).toBeVisible();

    await page.getByPlaceholder('Your name').fill('E2E Admin');
    await row.getByRole('button', { name: 'Acknowledge' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'acknowledged by E2E Admin' })).toBeVisible();
    await expect(row).toContainText('E2E Admin');
    await expect(row.getByRole('button', { name: 'Acknowledged' })).toBeDisabled();
  });

  test('running out turns the same alert into a stock-out; restocking closes it by itself', async ({ page, request }) => {
    const variant = await createVariant(request);
    const bin = await apiCreateBin(request);
    await apiAdjust(request, { gtin: variant.gtin, binCode: bin.binCode, change: 3 });
    const low = (await waitForAlert(request, variant.skuCode)) as AlertRow;
    expect(low.levelCode).toBe('LOW');

    await apiAdjust(request, { gtin: variant.gtin, binCode: bin.binCode, change: -3, reasonId: 3 });
    await poll(async () => {
      const alerts = (await (await request.get(`${API}/api/warehouse/alerts?pageSize=100`)).json()) as { items: AlertRow[] };
      return alerts.items.find((a) => a.skuCode === variant.skuCode && a.levelCode === 'OUT') ?? null;
    }, 'the alert to become a stock-out', 30_000);

    await page.goto('/warehouse/alerts');
    await expect(page.getByRole('row').filter({ hasText: variant.skuCode }).first()).toContainText('Stock-out');

    await apiAdjust(request, { gtin: variant.gtin, binCode: bin.binCode, change: 30, reconfirmed: true });
    await waitForAlert(request, variant.skuCode, false);
    await page.getByRole('button', { name: 'Refresh' }).click();
    await expect(page.getByRole('row').filter({ hasText: variant.skuCode })).toHaveCount(0);
  });

  test('lowering a threshold to zero clears a low-stock alert straight away; a bad number is stopped', async ({ page, request }) => {
    const variant = await createVariant(request);
    const bin = await apiCreateBin(request);
    await apiAdjust(request, { gtin: variant.gtin, binCode: bin.binCode, change: 4 });
    await waitForAlert(request, variant.skuCode);

    await page.goto('/warehouse/alerts');
    await page.getByPlaceholder('Your name').fill('E2E Admin');
    await page.getByRole('searchbox', { name: 'Search variants by SKU or barcode' }).fill(variant.skuCode);
    const threshold = page.getByLabel(`Low-stock threshold for ${variant.skuCode}`);
    await threshold.fill('-1');
    await expect(page.getByRole('alert').filter({ hasText: 'Enter a whole number from 0 to 100000.' })).toBeVisible();
    await expect(page.getByRole('row').filter({ hasText: variant.skuCode }).last().getByRole('button', { name: 'Save' })).toBeDisabled();

    await threshold.fill('0');
    await page.getByRole('row').filter({ hasText: variant.skuCode }).last().getByRole('button', { name: 'Save' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'threshold for' })).toBeVisible();
    await waitForAlert(request, variant.skuCode, false);
  });
});
