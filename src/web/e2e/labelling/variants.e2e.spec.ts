import { expect, test } from '@playwright/test';
import { createAcceptedStock, waitForWorkItem } from '../support';

/**
 * Sprint 5 (Labelling), the SKU register: every variant has one readable SKU and one barcode number; a barcode can be voided with a
 * reason and is then retired for good. The test prefix banner shows because the development stack uses the GS1 test range.
 */
test.describe.configure({ timeout: 180_000 }); // setting stock up goes through QC and the event dispatcher, which takes several seconds

test.describe('Labelling: the SKU register', () => {
  test('a new SKU is found by its code or its barcode, with the capacity and test-prefix banners', async ({ page, request }) => {
    const stock = await createAcceptedStock(request, 30);
    const item = await waitForWorkItem(request, stock.run.po.id);

    await page.goto('/labelling/register');
    await expect(page.getByRole('heading', { level: 1, name: 'SKU register' })).toBeVisible();
    await expect(page.getByText(/Barcode numbers used: \d+ of 100000/)).toBeVisible();
    await expect(page.getByText(/Test barcode prefix 0200000 is in use/)).toBeVisible();

    await page.getByRole('searchbox', { name: 'Search by SKU or barcode' }).fill(item.skuCode!);
    const row = page.getByRole('row').filter({ hasText: item.skuCode! });
    await expect(row).toContainText(item.gtin!);

    await page.getByRole('searchbox', { name: 'Search by SKU or barcode' }).fill(item.gtin!);
    await expect(page.getByRole('row').filter({ hasText: item.skuCode! })).toBeVisible();
  });

  test('voiding a barcode retires it for good, keeps the history, and removes the reprint and void actions', async ({ page, request }) => {
    const stock = await createAcceptedStock(request, 30);
    const item = await waitForWorkItem(request, stock.run.po.id);

    await page.goto('/labelling/register');
    await page.getByRole('searchbox', { name: 'Search by SKU or barcode' }).fill(item.skuCode!);
    const row = page.getByRole('row').filter({ hasText: item.skuCode! });
    await row.getByRole('button', { name: 'Void barcode' }).click();

    await expect(page.getByRole('heading', { name: `Void the barcode for ${item.skuCode}` })).toBeVisible();
    await expect(page.getByText('never used again')).toBeVisible();
    await page.getByLabel('Why').selectOption({ label: 'Assigned in error' });
    await page.getByLabel('Note (optional)').fill('e2e void');
    await page.getByLabel('Your name').fill('E2E Admin');
    await page.getByRole('button', { name: 'Void this barcode' }).click();

    await expect(page.getByRole('status').filter({ hasText: 'will never be used again' })).toBeVisible();
    await expect(row).toContainText('Voided: none in use');
    await expect(row).toContainText('1 retired number on record');
    await expect(row.getByRole('button', { name: 'Void barcode' })).toHaveCount(0);
    await expect(row.getByRole('link', { name: 'Reprint' })).toHaveCount(0);

    await row.getByRole('button', { name: 'Barcode history' }).click();
    const history = page.getByRole('region', { name: `Barcode history for ${item.skuCode}` }).or(page.locator('section', { hasText: `Barcode history for ${item.skuCode}` }));
    await expect(history).toContainText(item.gtin!);
    await expect(history).toContainText('Retired');
    await expect(history).toContainText('Reason: Error (e2e void)');
    await expect(history).toContainText('E2E Admin');
  });

  test('a voided barcode cannot be used for a new print job, even by calling the API directly', async ({ request }) => {
    const stock = await createAcceptedStock(request, 30);
    const item = await waitForWorkItem(request, stock.run.po.id);
    const voided = await request.post(`http://localhost:8080/api/labelling/variants/${item.variantId}/void`, {
      data: { reasonId: 2, actingPerson: 'E2E Admin', note: null },
    });
    expect(voided.status()).toBe(200);

    const queue = await request.post(`http://localhost:8080/api/labelling/work-items/${item.id}/print-jobs`, {
      data: { qty: 1, printerCode: 'FAKE-PDF', actingPerson: 'E2E API' },
    });
    expect(queue.status()).toBe(409);
    const reprint = await request.post(`http://localhost:8080/api/labelling/variants/${item.variantId}/reprints`, {
      data: { qty: 1, reasonId: 1, actingPerson: 'E2E API' },
    });
    expect(reprint.status()).toBe(409);
  });
});
