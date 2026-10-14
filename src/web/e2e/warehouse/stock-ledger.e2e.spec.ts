import { expect, test } from '@playwright/test';
import { apiAdjust, apiCreateBin, createVariant } from '../support';

/**
 * Sprint 6 (Warehouse), the stock ledger: what is on the shelves, in which bin and in which grade, with every change kept in a movement
 * history that shows who did it and the balance before and after. The screen is read only.
 */
test.describe.configure({ timeout: 180_000 });

test.describe('Warehouse: stock', () => {
  test('stock shows on hand, where it is, and the history of how it got there', async ({ page, request }) => {
    const variant = await createVariant(request);
    const binA = await apiCreateBin(request);
    const binB = await apiCreateBin(request);
    await apiAdjust(request, { gtin: variant.gtin, binCode: binA.binCode, change: 12 });
    await apiAdjust(request, { gtin: variant.gtin, binCode: binB.binCode, change: 5 });
    await apiAdjust(request, { gtin: variant.gtin, binCode: binA.binCode, change: -2, reasonId: 1 });

    await page.goto('/warehouse/stock');
    await expect(page.getByRole('heading', { level: 1, name: 'Stock' })).toBeVisible();
    await page.getByRole('searchbox', { name: 'Search by SKU or barcode' }).fill(variant.skuCode);

    const row = page.getByRole('row').filter({ hasText: variant.skuCode });
    await expect(row).toContainText('15 accepted, 0 concession');
    await expect(row).toContainText(`${binA.binCode}: 10`);
    await expect(row).toContainText(`${binB.binCode}: 5`);

    await row.getByRole('button', { name: 'Movements' }).click();
    const history = page.getByRole('region', { name: `Movements for ${variant.skuCode}` });
    await expect(history.getByRole('row')).toHaveCount(4); // header + three movements
    await expect(history.getByRole('row').nth(1)).toContainText('-2');
    await expect(history.getByRole('row').nth(1)).toContainText('12 → 10');
    await expect(history.getByRole('row').nth(1)).toContainText('E2E Buyer');
    await expect(history.getByRole('textbox')).toHaveCount(0); // nothing here can be edited
    await history.getByRole('button', { name: 'Close' }).click();
    await expect(history).toHaveCount(0);
  });
});
