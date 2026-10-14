import { expect, test } from '@playwright/test';
import { API, apiAdjust, apiCreateBin, createVariant } from '../support';

/**
 * Sprint 6 (Warehouse), cycle counts: a blind count of chosen bins (the screen never shows what the system expects), then a review
 * where each difference needs a reason, then confirm. Every difference becomes an adjustment; nothing is silently overwritten.
 */
test.describe.configure({ timeout: 180_000 });

test.describe('Warehouse: cycle counts', () => {
  test('count a bin blind, review the difference, give a reason, confirm: the stock is corrected', async ({ page, request }) => {
    const variant = await createVariant(request);
    const bin = await apiCreateBin(request);
    await apiAdjust(request, { gtin: variant.gtin, binCode: bin.binCode, change: 10 });

    await page.goto('/warehouse/cycle-counts');
    await expect(page.getByRole('heading', { level: 1, name: 'Cycle counts' })).toBeVisible();
    await page.getByRole('searchbox', { name: 'Filter the bins by zone' }).fill(bin.binCode.slice(0, 3));
    await page.getByRole('checkbox', { name: new RegExp(bin.binCode) }).check();
    await page.getByPlaceholder('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: /Start a count of 1 bin/ }).click();

    await expect(page.getByRole('heading', { level: 1, name: /Count CC-\d{4}-\d{4}/ })).toBeVisible();
    await expect(page.getByRole('status').filter({ hasText: 'started for 1 bin' })).toBeVisible();
    const pageText = (await page.locator('main').textContent())!.toLowerCase();
    expect(pageText).not.toContain('system'); // the blind count: nothing about what the system expects

    await page.getByLabel('Bin', { exact: true }).selectOption(bin.binCode);
    await page.getByLabel('Barcode (13 digits)').fill(variant.gtin);
    await page.getByLabel('How many you counted').fill('8');
    await page.getByLabel('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: 'Save counts for this bin' }).click();
    await expect(page.getByRole('status').filter({ hasText: 'Counts saved.' })).toBeVisible();
    await expect(page.getByRole('row').filter({ hasText: variant.skuCode })).toContainText('8');

    await page.getByRole('button', { name: 'Review the differences' }).click();
    await expect(page.getByRole('heading', { name: /Review CC-/ })).toBeVisible();
    const line = page.getByRole('row').filter({ hasText: variant.skuCode });
    await expect(line).toContainText('10'); // the system quantity appears here, and only here
    await expect(line).toContainText('-2');

    const confirm = page.getByRole('button', { name: 'Confirm the count' });
    await expect(confirm).toBeDisabled(); // a reason is required for every difference
    await line.getByRole('combobox').selectOption({ label: 'Miscount' });
    await expect(confirm).toBeEnabled();
    await confirm.click();

    await expect(page.getByRole('status').filter({ hasText: 'is closed' })).toBeVisible();
    const stock = await (await request.get(`${API}/api/warehouse/stock?search=${variant.skuCode}`)).json();
    expect(stock.items[0].onHand).toBe(8);
  });

  test('a bin that is already in an open count cannot be picked for another', async ({ page, request }) => {
    const bin = await apiCreateBin(request);
    await page.goto('/warehouse/cycle-counts');
    await page.getByRole('searchbox', { name: 'Filter the bins by zone' }).fill(bin.binCode.slice(0, 3));
    await page.getByRole('checkbox', { name: new RegExp(bin.binCode) }).check();
    await page.getByPlaceholder('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: /Start a count of 1 bin/ }).click();
    await expect(page.getByRole('heading', { level: 1, name: /Count CC-/ })).toBeVisible();

    await page.goto('/warehouse/cycle-counts');
    await page.getByRole('searchbox', { name: 'Filter the bins by zone' }).fill(bin.binCode.slice(0, 3));
    await expect(page.getByRole('checkbox', { name: new RegExp(bin.binCode) })).toBeDisabled();
    await expect(page.getByText(`${bin.binCode} (in an open count)`)).toBeVisible();
  });

  test('cancelling a count changes no stock and frees the bin', async ({ page, request }) => {
    const variant = await createVariant(request);
    const bin = await apiCreateBin(request);
    await apiAdjust(request, { gtin: variant.gtin, binCode: bin.binCode, change: 6 });

    await page.goto('/warehouse/cycle-counts');
    await page.getByRole('searchbox', { name: 'Filter the bins by zone' }).fill(bin.binCode.slice(0, 3));
    await page.getByRole('checkbox', { name: new RegExp(bin.binCode) }).check();
    await page.getByPlaceholder('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: /Start a count of 1 bin/ }).click();
    await page.getByLabel('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: 'Cancel this count' }).click();

    await expect(page.getByRole('status').filter({ hasText: 'No stock changed.' })).toBeVisible();
    const stock = await (await request.get(`${API}/api/warehouse/stock?search=${variant.skuCode}`)).json();
    expect(stock.items[0].onHand).toBe(6);
  });
});
